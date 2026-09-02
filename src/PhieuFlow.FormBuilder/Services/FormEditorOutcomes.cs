using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>Where the form load landed. Drives the builder's loading / not-found / editor views.</summary>
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
    /// <summary>A form was fetched and is now the session's <see cref="FormEditorSession.Form"/> — (re)initialise view state.</summary>
    Opened,

    /// <summary>The same form was already in memory; nothing was re-fetched and view state must be left alone.</summary>
    Reopened,

    /// <summary>A blank draft was minted; the page must navigate to <see cref="OpenOutcome.NewFormId"/>.</summary>
    RedirectToNew,

    /// <summary>The load failed; inspect <see cref="FormEditorSession.LoadState"/> / <see cref="FormEditorSession.LoadError"/>.</summary>
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
    /// Blocked because the autosave flush exhausted its retry budget while edits were still
    /// unsaved. See <see cref="FormEditorSession.PublishNotice"/>.
    /// </summary>
    Incomplete,

    /// <summary>The publish request itself failed; see <see cref="FormEditorSession.PublishError"/>.</summary>
    RequestFailed,
}

/// <inheritdoc cref="PublishOutcomeKind"/>
public sealed record PublishOutcome(
    PublishOutcomeKind Kind,
    PublishResultDto? Result = null,
    IReadOnlyList<PrePublishRow>? Rows = null);
