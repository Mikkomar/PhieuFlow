using Microsoft.Extensions.Logging;
using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// Owns one form's edit lifecycle — load, autosave, published-version fork reconciliation and
/// publish — for the <c>FormBuilder</c> page. Everything here is free of Blazor rendering and
/// navigation types: the page constructs a session, subscribes to <see cref="Changed"/> to
/// re-render, and acts on the returned outcomes (navigating, opening dialogs). That keeps the
/// state machine unit-testable against a fake <see cref="IFormsService"/>.
/// </summary>
public sealed class FormEditorSession : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<Guid, Guid> NoForkRemap = new Dictionary<Guid, Guid>();

    private readonly IFormsService _forms;
    private readonly AutosaveController _autosave;

    private FormEditModel? _form;

    // The form already held in memory. Guards OpenAsync from re-fetching (and clobbering unsaved
    // edits) on a spurious re-parametrization.
    private Guid? _loadedFormId;

    public FormEditorSession(IFormsService forms, ILogger<AutosaveController>? autosaveLogger = null)
    {
        _forms = forms;
        _autosave = new AutosaveController(SaveCoreAsync, CanSave, TimeSpan.FromMilliseconds(800), autosaveLogger);
        _autosave.StateChanged += () => Changed?.Invoke();
    }

    /// <summary>The tree being edited. Only valid once <see cref="LoadState"/> is <see cref="FormLoadState.Loaded"/>.</summary>
    public FormEditModel Form => _form!;

    public FormLoadState LoadState { get; private set; } = FormLoadState.Loading;

    public string? LoadError { get; private set; }

    public bool Publishing { get; private set; }

    public string? PublishError { get; private set; }

    public SaveState SaveState => _autosave.State;

    public DateTimeOffset? LastSavedAt => _autosave.LastSavedAt;

    /// <summary>Raised whenever anything the page renders from this session changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised after a save forked a new draft server-side and node ids were re-keyed. The map is
    /// old page id → new page id, so the page can keep the same page selected.
    /// </summary>
    public event Action<IReadOnlyDictionary<Guid, Guid>>? ForkReconciled;

    /// <summary>
    /// Opens <paramref name="formId"/>. <c>null</c> mints a blank draft and asks the page to
    /// redirect to it by id (so a reload re-opens it). An id already held is a no-op.
    /// </summary>
    public async Task<OpenOutcome> OpenAsync(Guid? formId)
    {
        if (formId is null)
        {
            EnterLoading();

            try
            {
                return OpenOutcome.RedirectToNew(await _forms.CreateNewAsync());
            }
            catch (HttpRequestException)
            {
                return Fail(FormLoadState.Error, "Couldn't start a new form.");
            }
        }

        if (_loadedFormId == formId)
        {
            LoadState = FormLoadState.Loaded;
            return OpenOutcome.Reopened;
        }

        EnterLoading();

        FormEditModel? loaded;
        try
        {
            loaded = await _forms.GetByIdAsync(formId.Value);
        }
        catch (HttpRequestException)
        {
            return Fail(FormLoadState.Error, "Couldn't load this form from the server.");
        }

        if (loaded is null)
        {
            return Fail(FormLoadState.NotFound, "This form couldn't be found.");
        }

        _form = loaded;
        _loadedFormId = loaded.FormId;
        LoadState = FormLoadState.Loaded;

        if (string.IsNullOrWhiteSpace(loaded.Title))
        {
            _autosave.Reset();
        }
        else
        {
            _autosave.SeedSaved(loaded.LastModifiedAt == default ? DateTimeOffset.Now : loaded.LastModifiedAt);
        }

        return OpenOutcome.Opened;
    }

    /// <summary>Record an edit to <see cref="Form"/> and (re)arm the debounced autosave.</summary>
    public void NotifyEdited() => _autosave.NotifyEdited();

    /// <summary>Persist any pending edit now. Callers flush before navigating away or publishing.</summary>
    public Task<AutosaveFlushResult> FlushAsync() => _autosave.FlushAsync();

    /// <summary>
    /// Flushes, then runs the pre-publish gate. Blocked cases (no title, already published, save
    /// failed) return before any publish round-trip.
    /// </summary>
    public async Task<PublishOutcome> PublishAsync()
    {
        var form = Form;

        if (form.Status == FormVersionStatus.Published)
        {
            return new PublishOutcome(PublishOutcomeKind.AlreadyPublished);
        }

        // An untitled form has never been autosaved, so there is nothing on the server to
        // validate — surface the title requirement the way a blocked save does.
        if (string.IsNullOrWhiteSpace(form.Title))
        {
            return new PublishOutcome(PublishOutcomeKind.MissingTitle);
        }

        // Publish must not proceed on a stale server copy: bail if the flush can't reach the
        // server (the header keeps its Retry affordance).
        var flush = await _autosave.FlushAsync();
        if (flush is AutosaveFlushResult.Failed or AutosaveFlushResult.Blocked)
        {
            return new PublishOutcome(PublishOutcomeKind.SaveFailed);
        }

        // The flush retried until its budget ran out and is still behind — publishing now would
        // ship a version missing whatever the user typed during the flush. No round-trip; tell
        // the user via the same dialog a real validation failure would use.
        if (flush is AutosaveFlushResult.Incomplete)
        {
            var pendingResult = new PublishResultDto
            {
                Published = false,
                Form = FormEditMapper.ToDto(form),
                VersionNumber = form.VersionNumber,
                LiveVersionNumber = form.LiveVersionNumber,
                IsFirstPublish = form.LiveVersionNumber is null,
            };
            var pendingRows = new[]
            {
                new PrePublishRow(
                    string.Empty,
                    "Some edits haven't reached the server yet. Wait a moment and publish again.",
                    string.Empty,
                    new JumpTarget(null, null)),
            };
            return new PublishOutcome(PublishOutcomeKind.Incomplete, pendingResult, pendingRows);
        }

        Publishing = true;
        PublishError = null;
        Changed?.Invoke();

        try
        {
            var result = await _forms.PublishAsync(form.FormId);
            // Best-effort inline annotation; the dialog rows come from the returned tree so they
            // are correct even if a fork just changed the server's node ids.
            FormEditMapper.ApplyIssues(form, result.Form);

            if (result.Published)
            {
                form.VersionNumber = result.VersionNumber;
                form.Revision = result.Revision;
                form.Status = MapStatus(result.Status);
                form.LastModifiedAt = result.LastModifiedAt;
                form.PublishedAt = result.PublishedAt;
                form.LiveVersionNumber = result.VersionNumber;
                _autosave.SeedSaved(DateTimeOffset.Now);
                return new PublishOutcome(PublishOutcomeKind.Published, result);
            }

            var rows = PrePublishRow.From(FormEditMapper.ToEditModel(result.Form));
            return new PublishOutcome(PublishOutcomeKind.NeedsFixes, result, rows);
        }
        catch (HttpRequestException)
        {
            PublishError = "Couldn't publish this form.";
            return new PublishOutcome(PublishOutcomeKind.RequestFailed);
        }
        finally
        {
            Publishing = false;
            Changed?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _autosave.FlushAsync();
        }
        catch
        {
            // best-effort flush on teardown
        }

        _autosave.Dispose();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(_form?.Title);

    /// <summary>The round-trip behind the autosave: save, absorb the returned state, reconcile a fork.</summary>
    private async Task SaveCoreAsync(CancellationToken token)
    {
        var form = Form;
        var previousVersion = form.VersionNumber;

        var result = await _forms.SaveAsync(form, token);
        form.VersionNumber = result.VersionNumber;
        form.Revision = result.Revision;
        form.Status = MapStatus(result.Status);
        form.LastModifiedAt = result.LastModifiedAt;
        form.PublishedAt = result.PublishedAt;

        // Editing a published version forks a new draft server-side with fresh node ids. Re-key
        // the in-memory tree to those ids (content untouched) — otherwise the next save reconciles
        // the stale published ids and collides on insert. Only on a fork.
        if (result.VersionNumber != previousVersion && !token.IsCancellationRequested)
        {
            var pageIdRemap = await _forms.ReconcileForkAsync(form, token);
            if (pageIdRemap.Count > 0)
            {
                ForkReconciled?.Invoke(pageIdRemap);
            }
        }
    }

    private void EnterLoading()
    {
        LoadState = FormLoadState.Loading;
        LoadError = null;
        Changed?.Invoke();
    }

    private OpenOutcome Fail(FormLoadState state, string message)
    {
        LoadState = state;
        LoadError = message;
        Changed?.Invoke();
        return OpenOutcome.Failed;
    }

    private static FormVersionStatus MapStatus(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Published => FormVersionStatus.Published,
        _ => FormVersionStatus.Draft,
    };
}
