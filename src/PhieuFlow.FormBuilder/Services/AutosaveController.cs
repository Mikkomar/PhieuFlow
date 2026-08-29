using PhieuFlow.FormBuilder.Enums;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>How a <see cref="AutosaveController.FlushAsync"/> ended.</summary>
public enum AutosaveFlushResult
{
    /// <summary>Nothing left to persist — the server holds the latest edit.</summary>
    UpToDate,

    /// <summary>There is unsaved work but the gate (<c>canSave</c>) is closed, so nothing was sent.</summary>
    Blocked,

    /// <summary>A save was attempted and the server could not be reached.</summary>
    Failed,
}

/// <summary>
/// The builder's debounced autosave, lifted out of the page so the coalescing and the
/// flush-with-retry race handling can be reasoned about (and unit-tested) on their own.
/// <para>
/// It owns only the scheduling: a monotonic edit counter vs. the counter last persisted (they
/// diverge whenever there is unsaved work, including an edit that lands mid-save), a single
/// in-flight <see cref="CancellationTokenSource"/>, and the <see cref="SaveState"/> the header
/// renders. The actual round-trip is the <c>saveAsync</c> delegate; <c>canSave</c> is the gate
/// (an untitled form has nothing to persist).
/// </para>
/// </summary>
public sealed class AutosaveController : IDisposable
{
    // A user who keeps typing straight through a flush would loop it forever; give up after this
    // many saves and let the caller proceed (a publish re-flushes anyway).
    private const int MaxFlushAttempts = 4;

    private readonly Func<CancellationToken, Task> _saveAsync;
    private readonly Func<bool> _canSave;
    private readonly TimeSpan _debounce;

    private int _pendingSeq;
    private int _savedSeq;
    private CancellationTokenSource? _cts;

    public AutosaveController(Func<CancellationToken, Task> saveAsync, Func<bool> canSave, TimeSpan debounce)
    {
        _saveAsync = saveAsync;
        _canSave = canSave;
        _debounce = debounce;
    }

    public SaveState State { get; private set; } = SaveState.Idle;

    public DateTimeOffset? LastSavedAt { get; private set; }

    /// <summary>The persisted counter is behind the edit counter — an edit is waiting to be saved.</summary>
    public bool HasUnsavedWork => _pendingSeq != _savedSeq;

    /// <summary>Raised on every <see cref="State"/> / <see cref="LastSavedAt"/> change so the owner can re-render.</summary>
    public event Action? StateChanged;

    /// <summary>Record an edit and (re)arm the debounce. A no-op past the gate beyond marking state idle.</summary>
    public void NotifyEdited()
    {
        _pendingSeq++;
        _cts?.Cancel();

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

    /// <summary>Drive the server up to the latest edit now, bypassing the debounce.</summary>
    public async Task<AutosaveFlushResult> FlushAsync()
    {
        // Loops because a save can race a fresh keystroke that bumps the pending counter past
        // what that save captured.
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

            if (State == SaveState.Error)
            {
                return AutosaveFlushResult.Failed;
            }
        }

        return AutosaveFlushResult.UpToDate;
    }

    /// <summary>Seed the controller as "everything saved" — used right after a fresh load or a publish.</summary>
    public void SeedSaved(DateTimeOffset savedAt)
    {
        _pendingSeq = 0;
        _savedSeq = 0;
        LastSavedAt = savedAt;
        SetState(SaveState.Saved);
    }

    /// <summary>Seed the controller as "nothing to save yet" — used after loading an untitled draft.</summary>
    public void Reset()
    {
        _cts?.Cancel();
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
        SetState(SaveState.Saving);

        try
        {
            await _saveAsync(token);
            _savedSeq = seq;

            if (_pendingSeq == seq)
            {
                LastSavedAt = DateTimeOffset.Now;
                SetState(SaveState.Saved);
            }
            else
            {
                // A keystroke landed while this save was in flight: it is not in what we just
                // persisted, so stay Pending and let the queued debounce (or a flush) catch it.
                SetState(SaveState.Pending);
            }
        }
        catch (TaskCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer edit or flush, which owns the state from here.
        }
        catch (HttpRequestException)
        {
            SetState(SaveState.Error);
        }
    }

    private void SetState(SaveState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}
