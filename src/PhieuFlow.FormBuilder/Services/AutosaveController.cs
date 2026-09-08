using Microsoft.Extensions.Logging;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Enums;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>How a <see cref="AutosaveController.FlushAsync"/> ended.</summary>
public enum AutosaveFlushResult
{
    /// <summary>Nothing left to persist. The server holds the latest edit.</summary>
    UpToDate,

    /// <summary>There is unsaved work but the gate (<c>canSave</c>) is closed, so nothing was sent.</summary>
    Blocked,

    /// <summary>A save was attempted and the server could not be reached.</summary>
    Failed,

    /// <summary>The retry budget ran out while edits kept arriving. The server is still behind.</summary>
    Incomplete,
}

/// <summary>
/// The builder's debounced autosave, kept separate from the page so its coalescing and
/// retry logic can be unit-tested. It only schedules: an edit counter versus the last
/// persisted counter, one in-flight save, and the header's <see cref="SaveState"/>.
/// </summary>
public sealed class AutosaveController : IDisposable
{
    // Continuous typing could loop a flush forever. After this many saves, stop and report
    // Incomplete rather than "saved".
    private const int MaxFlushAttempts = 4;

    private readonly Func<CancellationToken, Task<DateTimeOffset>> _saveAsync;
    private readonly Func<bool> _canSave;
    private readonly TimeSpan _debounce;
    private readonly ILogger<AutosaveController>? _logger;

    private int _pendingSeq;
    private int _savedSeq;
    private int _generation;
    private CancellationTokenSource? _cts;

    public AutosaveController(
        Func<CancellationToken, Task<DateTimeOffset>> saveAsync,
        Func<bool> canSave,
        TimeSpan debounce,
        ILogger<AutosaveController>? logger = null)
    {
        _saveAsync = saveAsync;
        _canSave = canSave;
        _debounce = debounce;
        _logger = logger;
    }

    public SaveState State { get; private set; } = SaveState.Idle;

    public DateTimeOffset? LastSavedAt { get; private set; }

    /// <summary>The persisted counter is behind the edit counter, so an edit is unsaved.</summary>
    public bool HasUnsavedWork => _pendingSeq != _savedSeq;

    /// <summary>Raised on every <see cref="State"/> / <see cref="LastSavedAt"/> change so the owner can re-render.</summary>
    public event Action? StateChanged;

    /// <summary>Record an edit and re-arm the debounce. Past the gate it only marks state Idle.</summary>
    public void NotifyEdited()
    {
        _pendingSeq++;
        _cts?.Cancel();

        if (State == SaveState.Conflict)
        {
            // A conflict is terminal until reload. Record the edit but do not re-arm the
            // debounce. Every attempt would 409 again.
            return;
        }

        if (!_canSave())
        {
            SetState(SaveState.Idle);
            return;
        }

        SetState(SaveState.Pending);

        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = DebounceThenSaveAsync(cts.Token);
    }

    /// <summary>Save the latest edit now, without waiting for the debounce.</summary>
    public async Task<AutosaveFlushResult> FlushAsync()
    {
        // A conflict is terminal. Report Failed so callers do not proceed on a rejected copy.
        if (State == SaveState.Conflict)
        {
            return AutosaveFlushResult.Failed;
        }

        // Loop: a save can race a keystroke that bumps the pending counter past it.
        for (var attempt = 0; attempt < MaxFlushAttempts; attempt++)
        {
            if (!HasUnsavedWork && State != SaveState.Error)
            {
                return AutosaveFlushResult.UpToDate;
            }

            _cts?.Cancel();

            if (!_canSave())
            {
                return AutosaveFlushResult.Blocked;
            }

            var cts = new CancellationTokenSource();
            _cts = cts;
            await SaveAsync(cts.Token);

            if (State is SaveState.Error or SaveState.Conflict)
            {
                return AutosaveFlushResult.Failed;
            }
        }

        // Loop ended with work still queued or an error. Never report UpToDate while
        // HasUnsavedWork is true.
        if (HasUnsavedWork || State == SaveState.Error)
        {
            _logger?.LogWarning(
                "Autosave flush exhausted {MaxFlushAttempts} attempts with unsaved work still pending (state={State}).",
                MaxFlushAttempts, State);
            return AutosaveFlushResult.Incomplete;
        }

        return AutosaveFlushResult.UpToDate;
    }

    /// <summary>
    /// Marks the controller conflicted from outside, for when a publish attempt 409s. This
    /// copy is then as stale as a 409'd autosave: stop retrying, the header offers Reload.
    /// </summary>
    public void MarkConflict()
    {
        _cts?.Cancel();
        _generation++;
        SetState(SaveState.Conflict);
    }

    /// <summary>Seed the controller as "everything saved", after a fresh load or a publish.</summary>
    public void SeedSaved(DateTimeOffset savedAt)
    {
        _cts?.Cancel();
        _generation++;
        _pendingSeq = 0;
        _savedSeq = 0;
        LastSavedAt = savedAt;
        SetState(SaveState.Saved);
    }

    /// <summary>Seed the controller as "nothing to save yet", after loading an untitled draft.</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _generation++;
        _pendingSeq = 0;
        _savedSeq = 0;
        LastSavedAt = null;
        SetState(SaveState.Idle);
    }

    public void Dispose() => _cts?.Cancel();

    private async Task DebounceThenSaveAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounce, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await SaveAsync(token);
    }

    private async Task SaveAsync(CancellationToken token)
    {
        var seq = _pendingSeq;
        var generation = _generation;
        SetState(SaveState.Saving);

        try
        {
            var savedAt = await _saveAsync(token);

            if (generation != _generation)
            {
                // SeedSaved/Reset/MarkConflict ran during this save and already reset the
                // counters. A stale seq here would wedge HasUnsavedWork.
                return;
            }

            _savedSeq = seq;

            if (_pendingSeq == seq)
            {
                LastSavedAt = savedAt;
                SetState(SaveState.Saved);
            }
            else
            {
                // A keystroke arrived during this save, so it is not yet persisted. Stay
                // Pending and let the queued debounce or a flush handle it.
                SetState(SaveState.Pending);
            }
        }
        catch (TaskCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer edit or flush, which owns the state from here.
        }
        catch (FormRevisionConflictException ex)
        {
            // Another session advanced the form and the server refused this save. Terminal
            // until reload. NotifyEdited and FlushAsync stop trying.
            _logger?.LogInformation(ex, "Autosave hit an optimistic-concurrency conflict; the form is now read-only until reload.");
            if (generation == _generation)
            {
                SetState(SaveState.Conflict);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Autosave round-trip to the Hub failed.");
            if (generation == _generation)
            {
                SetState(SaveState.Error);
            }
        }
        catch (Exception ex)
        {
            // SaveAsync runs fire-and-forget, so an empty save body, a JSON fault or a mapper
            // failure would otherwise be an unobserved exception.
            _logger?.LogError(ex, "Autosave failed with an unexpected error.");
            if (generation == _generation)
            {
                SetState(SaveState.Error);
            }
        }
    }

    private void SetState(SaveState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}
