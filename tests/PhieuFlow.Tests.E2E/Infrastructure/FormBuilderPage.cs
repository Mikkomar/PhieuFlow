using Microsoft.Playwright;

namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// Thin page object over <c>FormBuilder.razor</c> so builder and versioning specs share
/// one set of selectors. All locators are role/label/placeholder based, mirroring the
/// component's accessibility attributes.
/// </summary>
public sealed class FormBuilderPage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator TitleInput => Page.GetByLabel("Form title");

    public ILocator DescriptionTextarea => Page.Locator("#description");

    public ILocator PublishButton => Page.GetByRole(AriaRole.Button, new() { Name = "Publish" });

    public ILocator PublishedNotice => Page.GetByText("This version is published");

    public ILocator TitleRequiredMessage => Page.GetByText("Title is required.");

    public ILocator QuestionRows => Page.Locator("div[aria-keyshortcuts='Alt+ArrowUp Alt+ArrowDown']");

    public async Task SetTitleAsync(string title)
    {
        await TitleInput.FillAsync(title);
        await TitleInput.BlurAsync();
    }

    public async Task SetDescriptionAsync(string description)
    {
        await DescriptionTextarea.FillAsync(description);
        await DescriptionTextarea.BlurAsync();
    }

    public async Task AddPageAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add page" }).ClickAsync();
    }

    public async Task SelectPageAsync(int index)
    {
        await Page.GetByRole(AriaRole.Tab).Nth(index).ClickAsync();
    }

    /// <summary>
    /// Opens the "Add question" menu, picks <paramref name="type"/> (menu item label, e.g.
    /// "Text area", "Number", "Radio buttons"), and types the question text into the
    /// freshly-expanded card.
    /// </summary>
    public async Task AddQuestionAsync(string type, string text)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add question" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = type, Exact = false }).ClickAsync();
        var textField = QuestionTextField;
        await textField.WaitForAsync();
        await textField.FillAsync(text);
        await textField.BlurAsync();
        // Blazor Server commits @oninput over the wire; give it a beat before the next
        // action re-renders the list. NOTE: the builder can still drop this text if the
        // card is collapsed immediately afterwards (one-way value= binding, no debounce),
        // so assertions that must survive that should key off question type, not text.
        await Page.WaitForTimeoutAsync(300);
    }

    /// <summary>
    /// Sets the option labels of the currently-expanded choice question. A new choice
    /// question already has one (empty) option, so the first label reuses it and each
    /// further label clicks "Add option" first.
    /// </summary>
    public async Task SetOptionsAsync(params string[] labels)
    {
        var inputs = Page.GetByPlaceholder("Option label");
        for (var i = 0; i < labels.Length; i++)
        {
            if (i > 0)
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "Add option" }).ClickAsync();
            }

            await inputs.Nth(i).FillAsync(labels[i]);
            await inputs.Nth(i).BlurAsync();
        }
    }

    public async Task SetNumberRangeAsync(string min, string max)
    {
        var spin = Page.GetByRole(AriaRole.Spinbutton);
        await spin.Nth(0).FillAsync(min);
        await spin.Nth(1).FillAsync(max);
        await spin.Nth(1).BlurAsync();
    }

    public async Task ToggleRequiredAsync()
    {
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Required" }).ClickAsync();
    }

    private ILocator QuestionTextField => Page.GetByPlaceholder("e.g. Which department are you joining?");

    /// <summary>Expands the question card whose label is <paramref name="text"/> (no-op if already open).</summary>
    public async Task OpenQuestionAsync(string text)
    {
        if (await QuestionTextField.IsVisibleAsync() && await QuestionTextField.InputValueAsync() == text)
        {
            return;
        }

        await QuestionRows.Filter(new LocatorFilterOptions { HasTextString = text }).First.ClickAsync();
        await QuestionTextField.WaitForAsync();
    }

    public async Task DeleteQuestionAsync(string text)
    {
        await OpenQuestionAsync(text);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete question" }).ClickAsync();
    }

    /// <summary>Deletes the currently-expanded question (e.g. the one just added).</summary>
    public async Task DeleteExpandedQuestionAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete question" }).ClickAsync();
    }

    public async Task DeleteActivePageAsync()
    {
        // A page with questions raises a confirm() dialog; accept it.
        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete page" }).ClickAsync();
    }

    /// <summary>
    /// Keyboard-reorders the question labelled <paramref name="text"/> by <paramref name="delta"/>
    /// positions (Alt+Arrow, see <c>QuestionEditCard.OnRowKeyDown</c>). Re-resolves the row
    /// between presses because the list re-renders after each move.
    /// </summary>
    public async Task MoveQuestionAsync(string text, int delta)
    {
        var key = delta < 0 ? "Alt+ArrowUp" : "Alt+ArrowDown";
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            var row = QuestionRows.Filter(new LocatorFilterOptions { HasTextString = text }).First;
            await row.FocusAsync();
            await row.PressAsync(key);
            // Let the list re-render (and the autosave state flip) before the next hop.
            await Page.WaitForTimeoutAsync(400);
        }
    }

    /// <summary>
    /// Keyboard-reorders the question at 0-based <paramref name="fromIndex"/> by
    /// <paramref name="delta"/> positions, re-resolving by position between hops.
    /// </summary>
    public async Task MoveQuestionByPositionAsync(int fromIndex, int delta)
    {
        var pos = fromIndex;
        var step = delta < 0 ? -1 : 1;
        var key = delta < 0 ? "Alt+ArrowUp" : "Alt+ArrowDown";
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            var row = QuestionRows.Nth(pos);
            await row.FocusAsync();
            await row.PressAsync(key);
            await Page.WaitForTimeoutAsync(400);
            pos += step;
        }
    }
}
