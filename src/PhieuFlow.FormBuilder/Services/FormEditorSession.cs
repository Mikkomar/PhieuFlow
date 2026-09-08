using Microsoft.Extensions.Logging;
using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Validation;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// Owns one form's edit lifecycle for the <c>FormBuilder</c> page: load, autosave, fork
/// reconciliation, publish. It uses no Blazor types, so the page drives it through
/// <see cref="Changed"/> and returned outcomes, and tests use a fake <see cref="IFormsService"/>.
/// </summary>
public sealed class FormEditorSession : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<Guid, Guid> NoForkRemap = new Dictionary<Guid, Guid>();

    private readonly IFormsService _forms;
    private readonly AutosaveController _autosave;
    private readonly ILogger<FormEditorSession>? _logger;
    private readonly IFormPublishValidator _publishValidator;

    private FormEditModel? _form;

    // The form already in memory. Stops OpenAsync from re-fetching and losing unsaved edits
    // on a spurious re-parametrization.
    private Guid? _loadedFormId;

    public FormEditorSession(
        IFormsService forms,
        ILogger<AutosaveController>? autosaveLogger = null,
        ILogger<FormEditorSession>? logger = null,
        IFormPublishValidator? publishValidator = null)
    {
        _forms = forms;
        _logger = logger;
        _publishValidator = publishValidator ?? new FormPublishValidator();
        _autosave = new AutosaveController(SaveCoreAsync, CanSave, TimeSpan.FromMilliseconds(800), autosaveLogger);
        _autosave.StateChanged += () => Changed?.Invoke();
    }

    /// <summary>The tree being edited. Only valid once <see cref="LoadState"/> is <see cref="FormLoadState.Loaded"/>.</summary>
    public FormEditModel Form => _form!;

    public FormLoadState LoadState { get; private set; } = FormLoadState.Loading;

    public string? LoadError { get; private set; }

    public bool Publishing { get; private set; }

    public string? PublishError { get; private set; }

    public string? PublishNotice { get; private set; }

    public SaveState SaveState => _autosave.State;

    public DateTimeOffset? LastSavedAt => _autosave.LastSavedAt;

    /// <summary>Raised whenever anything the page renders from this session changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised after a save forked a new draft and node ids were re-keyed. Maps old page id
    /// to new page id, so the page keeps the same page selected.
    /// </summary>
    public event Action<IReadOnlyDictionary<Guid, Guid>>? ForkReconciled;

    /// <summary>
    /// Opens <paramref name="formId"/>. <c>null</c> creates a blank draft and asks the page
    /// to redirect to it by id. An id already held is a no-op.
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
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "Creating a new form on the Hub failed.");
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
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Loading form {FormId} from the Hub failed.", formId.Value);
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

    /// <summary>Record an edit to <see cref="Form"/> and re-arm the debounced autosave.</summary>
    public void NotifyEdited()
    {
        PublishNotice = null;
        _autosave.NotifyEdited();
    }

    /// <summary>Persist any pending edit now. Callers flush before navigating away or publishing.</summary>
    public Task<AutosaveFlushResult> FlushAsync() => _autosave.FlushAsync();

    /// <summary>
    /// Runs the local pre-publish gate, then flushes and calls the Hub, which re-validates.
    /// Blocked cases return before any round-trip.
    /// </summary>
    public async Task<PublishOutcome> PublishAsync()
    {
        var form = Form;

        if (form.Status == FormVersionStatus.Published)
        {
            return new PublishOutcome(PublishOutcomeKind.AlreadyPublished);
        }

        // An untitled form was never autosaved, so there is nothing on the server to
        // validate. Report the missing title like a blocked save.
        if (string.IsNullOrWhiteSpace(form.Title))
        {
            return new PublishOutcome(PublishOutcomeKind.MissingTitle);
        }

        // Local gate with the same rules the Hub runs, so an invalid form never leaves the
        // browser. The Hub re-validates on publish, catching any race or drift.
        var localDto = FormEditMapper.ToDto(form);
        if (!_publishValidator.Validate(localDto))
        {
            FormEditMapper.ApplyIssues(form, localDto);
            var localRows = PrePublishRow.From(FormEditMapper.ToEditModel(localDto));
            return new PublishOutcome(PublishOutcomeKind.NeedsFixes, LocalGateResult(localDto, form), localRows);
        }

        // Do not publish on a stale server copy. Stop if the flush cannot reach the server.
        // The header keeps its Retry action.
        var flush = await _autosave.FlushAsync();
        if (flush is AutosaveFlushResult.Failed or AutosaveFlushResult.Blocked)
        {
            return new PublishOutcome(PublishOutcomeKind.SaveFailed);
        }

        // The flush ran out of retries and is still behind. Publishing now would drop recent
        // edits, so show a one-line notice in the header instead of the dialog.
        if (flush is AutosaveFlushResult.Incomplete)
        {
            PublishNotice = "Some edits haven't reached the server yet. Publishing again in a moment will include them.";
            return new PublishOutcome(PublishOutcomeKind.Incomplete);
        }

        PublishNotice = null;
        Publishing = true;
        PublishError = null;
        Changed?.Invoke();

        try
        {
            var result = await _forms.PublishAsync(form.FormId);
            // Best-effort inline annotation. The dialog rows come from the returned tree, so
            // they are right even if a fork changed the server's node ids.
            FormEditMapper.ApplyIssues(form, result.Form);

            if (result.Published)
            {
                form.VersionNumber = result.VersionNumber;
                form.Revision = result.Revision;
                form.Status = MapStatus(result.Status);
                form.LastModifiedAt = result.LastModifiedAt;
                form.PublishedAt = result.PublishedAt;
                form.LiveVersionNumber = result.VersionNumber;
                _autosave.SeedSaved(result.PublishedAt ?? result.LastModifiedAt);
                return new PublishOutcome(PublishOutcomeKind.Published, result);
            }

            var rows = PrePublishRow.From(FormEditMapper.ToEditModel(result.Form));
            return new PublishOutcome(PublishOutcomeKind.NeedsFixes, result, rows);
        }
        catch (FormRevisionConflictException ex)
        {
            _logger?.LogInformation(ex, "Publish of form {FormId} hit an optimistic-concurrency conflict.", form.FormId);
            _autosave.MarkConflict();
            return new PublishOutcome(PublishOutcomeKind.Conflict);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Publishing form {FormId} failed.", form.FormId);
            PublishError = "Couldn't publish this form.";
            return new PublishOutcome(PublishOutcomeKind.RequestFailed);
        }
        finally
        {
            Publishing = false;
            Changed?.Invoke();
        }
    }

    // Stands in for the Hub's PublishResultDto on the local-gate path, so the pre-publish
    // dialog renders the same as for a 422.
    private static PublishResultDto LocalGateResult(FormDto annotated, FormEditModel form) => new()
    {
        Published = false,
        Form = annotated,
        VersionNumber = form.VersionNumber,
        LiveVersionNumber = form.LiveVersionNumber,
        IsFirstPublish = form.LiveVersionNumber is null,
    };

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _autosave.FlushAsync();
        }
        catch (Exception ex)
        {
            // best-effort flush on teardown. A final unsaved edit can be lost.
            _logger?.LogDebug(ex, "Best-effort autosave flush on session teardown failed.");
        }

        _autosave.Dispose();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(_form?.Title);

    /// <summary>The round-trip behind the autosave: save, absorb the returned state, reconcile a fork.</summary>
    private async Task<DateTimeOffset> SaveCoreAsync(CancellationToken token)
    {
        var form = Form;
        var previousVersion = form.VersionNumber;

        var result = await _forms.SaveAsync(form, token);
        form.VersionNumber = result.VersionNumber;
        form.Revision = result.Revision;
        form.Status = MapStatus(result.Status);
        form.LastModifiedAt = result.LastModifiedAt;
        form.PublishedAt = result.PublishedAt;

        // Editing a published version forks a new draft with fresh node ids. Re-key the
        // in-memory tree to them, or the next save collides on insert.
        if (result.VersionNumber != previousVersion && !token.IsCancellationRequested)
        {
            var pageIdRemap = await _forms.ReconcileForkAsync(form, token);
            if (pageIdRemap.Count > 0)
            {
                ForkReconciled?.Invoke(pageIdRemap);
            }
        }

        return result.LastModifiedAt;
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
