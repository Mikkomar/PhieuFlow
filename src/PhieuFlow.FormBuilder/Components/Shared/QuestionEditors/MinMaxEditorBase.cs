using Microsoft.AspNetCore.Components;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Components.Shared.QuestionEditors;

/// <summary>Shared plumbing for the Min/Max range editors: a jump focuses the Min or Max input.</summary>
public abstract class MinMaxEditorBase : JumpFocusComponent
{
    [Parameter]
    public EventCallback OnChanged { get; set; }

    protected ElementReference MinRef;
    protected ElementReference MaxRef;

    /// <summary>The question node this editor edits — carries the issues the Hub validator hung on it.</summary>
    protected abstract IHasIssues Node { get; }

    private IReadOnlyList<ValidationIssue> Issues => Node.Issues;

    protected override Task FocusJumpTargetAsync()
    {
        var target = Issues.FirstOrDefault()?.Field == ValidationField.Max ? MaxRef : MinRef;
        return target.FocusAsync().AsTask();
    }

    /// <summary>Applies <paramref name="apply"/> to the question, clears its now-stale issues, and notifies.</summary>
    protected void Edit(Action apply)
    {
        Node.Edit(apply);
        OnChanged.InvokeAsync();
    }
}
