using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>The result of a form load. Selects the loading, not-found, or editor view.</summary>
public enum FormLoadState
{
    Loading,
    Loaded,
    NotFound,
    Error,
}

/// <summary>What <see cref="FormEditorSession.OpenAsync"/> wants the page to do next.</summary>
public enum OpenOutcomeKind
{
    /// <summary>A form was fetched into <see cref="FormEditorSession.Form"/>. Re-initialise view state.</summary>
    Opened,

    /// <summary>The same form was already in memory. Nothing was re-fetched. Leave view state alone.</summary>
    Reopened,

    /// <summary>A blank draft was created. Navigate to <see cref="OpenOutcome.NewFormId"/>.</summary>
    RedirectToNew,

    /// <summary>The load failed. See <see cref="FormEditorSession.LoadState"/> and <see cref="FormEditorSession.LoadError"/>.</summary>
    Failed,
}

/// <inheritdoc cref="OpenOutcomeKind"/>
public sealed record OpenOutcome(OpenOutcomeKind Kind, Guid NewFormId = default)
{
    public static readonly OpenOutcome Opened = new(OpenOutcomeKind.Opened);
    public static readonly OpenOutcome Reopened = new(OpenOutcomeKind.Reopened);
    public static readonly OpenOutcome Failed = new(OpenOutcomeKind.Failed);

    public static OpenOutcome RedirectToNew(Guid newFormId) => new(OpenOutcomeKind.RedirectToNew, newFormId);
}

/// <summary>The result of <see cref="FormEditorSession.PublishAsync"/>.</summary>
public enum PublishOutcomeKind
{
    /// <summary>The version is now live. <see cref="PublishOutcome.Result"/> carries the server state.</summary>
    Published,

    /// <summary>The publish gate found problems. <see cref="PublishOutcome.Rows"/> are the dialog rows.</summary>
    NeedsFixes,

    /// <summary>Blocked before any round-trip: the form has no title.</summary>
    MissingTitle,

    /// <summary>Blocked before any round-trip: this version is already published.</summary>
    AlreadyPublished,

    /// <summary>Blocked because pending edits could not be flushed to the server first.</summary>
    SaveFailed,

    /// <summary>
    /// Blocked because the autosave flush ran out of retries with edits unsaved. See
    /// <see cref="FormEditorSession.PublishNotice"/>.
    /// </summary>
    Incomplete,

    /// <summary>The publish request itself failed. See <see cref="FormEditorSession.PublishError"/>.</summary>
    RequestFailed,

    /// <summary>
    /// The Hub rejected the publish with 409: another session's save arrived mid-publish.
    /// <see cref="FormEditorSession.SaveState"/> is now <c>Conflict</c>; the header offers reload.
    /// </summary>
    Conflict,
}

/// <inheritdoc cref="PublishOutcomeKind"/>
public sealed record PublishOutcome(
    PublishOutcomeKind Kind,
    PublishResultDto? Result = null,
    IReadOnlyList<PrePublishRow>? Rows = null);
