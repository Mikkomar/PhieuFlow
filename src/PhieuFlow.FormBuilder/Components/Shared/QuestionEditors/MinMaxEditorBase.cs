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

    /// <summary>The issues the Hub validator hung on this question.</summary>
    protected abstract IReadOnlyList<ValidationIssue> Issues { get; }

    protected override Task FocusJumpTargetAsync()
    {
        var target = Issues.FirstOrDefault()?.Field == ValidationField.Max ? MaxRef : MinRef;
        return target.FocusAsync().AsTask();
    }
}
