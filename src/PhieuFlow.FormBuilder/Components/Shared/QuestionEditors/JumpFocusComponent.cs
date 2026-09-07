using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace PhieuFlow.FormBuilder.Components.Shared.QuestionEditors;

/// <summary>
/// A sub-editor that a pre-publish dialog jump can land on. The owning card sets <see cref="FocusIssue"/>;
/// once the editor has rendered, <see cref="FocusJumpTargetAsync"/> moves focus to the exact
/// offending control and <see cref="OnJumpApplied"/> clears the request.
/// </summary>
public abstract class JumpFocusComponent : ComponentBase
{
    [Parameter]
    public bool FocusIssue { get; set; }

    [Parameter]
    public EventCallback OnJumpApplied { get; set; }

    [Inject]
    protected ILogger<JumpFocusComponent> Logger { get; set; } = default!;

    private bool _handled;

    protected override void OnParametersSet()
    {
        if (!FocusIssue)
        {
            _handled = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!FocusIssue || _handled)
        {
            return;
        }

        _handled = true;
        try
        {
            await FocusJumpTargetAsync();
        }
        catch (Exception ex)
        {
            // element not in the DOM yet
            Logger.LogDebug(ex, "Focusing a pre-publish jump target failed; the element is not in the DOM yet.");
        }

        await OnJumpApplied.InvokeAsync();
    }

    protected abstract Task FocusJumpTargetAsync();
}
