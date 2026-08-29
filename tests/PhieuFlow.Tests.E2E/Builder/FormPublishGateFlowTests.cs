using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Builder;

/// <summary>
/// End-to-end coverage of the pre-publish gate: a form with problems can't go live, the
/// dialog lists them with jump links, and fixing them lets the publish through.
/// </summary>
public sealed class FormPublishGateFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestPublish_When_QuestionTextBlank_Should_ShowFailureDialogAndNotPublish()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Gate {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        // Add a question, then clear its text so the form is invalid.
        await builder.AddQuestionAsync("Text area", "temp");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").FillAsync("");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").BlurAsync();
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();

        await Assertions.Expect(builder.PublishDialog).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("1 problem blocks publishing")).ToBeVisibleAsync();

        await builder.PublishCloseButton.ClickAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Status.Should().Be(Hub.Contracts.FormVersionStatusDto.Draft);
    }

    [Fact]
    public async Task TestPublish_When_JumpLinkClicked_Should_ExpandOwningQuestion()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Jump {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "temp");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").FillAsync("");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").BlurAsync();
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();
        await Assertions.Expect(builder.PublishDialog).ToBeVisibleAsync();
        await builder.DialogJumpLink().First.ClickAsync();

        await Assertions.Expect(builder.PublishDialog).ToBeHiddenAsync();
        await Assertions.Expect(Page.GetByPlaceholder("e.g. Which department are you joining?")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task TestPublish_When_ValidationFailsOnNewForm_Should_KeepQuestionsAcrossReload()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Reload-safe {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "First");
        await builder.AddQuestionAsync("Number", "Second");
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();
        await Assertions.Expect(builder.PublishDialog).ToBeVisibleAsync();
        await builder.PublishCloseButton.ClickAsync();

        // Hard reload: the URL now carries the form id, so the latest version loads back.
        await Page.ReloadAsync();
        await Assertions.Expect(builder.TitleInput).ToHaveValueAsync(title);
        await Assertions.Expect(builder.QuestionRows).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task TestPublish_When_ValidationFailsAfterEditingPublishedForm_Should_KeepThoseEdits()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"No-loss {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Original question");
        await WaitForSavedAsync();
        await ClickPublishAsync();

        var id = await GetFormIdByTitleAsync(title);

        // First edit after publish forks v2; then add a dropdown with a duplicate label so
        // the next publish fails validation.
        await builder.AddQuestionAsync("Dropdown", "Pick one");
        await builder.SetOptionsAsync("Same", "Same");
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();
        await Assertions.Expect(builder.PublishDialog).ToBeVisibleAsync();
        await builder.PublishCloseButton.ClickAsync();

        // A reload must show the forked draft with both the new question and its options.
        var form = await GetFormAsync(id);
        form.VersionNumber.Should().Be(2);
        form.Status.Should().Be(Hub.Contracts.FormVersionStatusDto.Draft);
        form.Pages[0].Questions.Should().HaveCount(2);
        form.Pages[0].Questions.OfType<Hub.Contracts.DropDownQuestionDto>()
            .Single().Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task TestPublish_When_ValidationFailsAfterEditingAcrossTheFork_Should_KeepTheLastEdit()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Across-fork {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Number", "Age");
        await builder.SetNumberRangeAsync("0", "40");
        await WaitForSavedAsync();
        await ClickPublishAsync();

        var id = await GetFormIdByTitleAsync(title);

        // First edit after publish forks v2 (and re-keys the tree in place).
        await builder.AddQuestionAsync("Text area", "Notes");
        await WaitForSavedAsync();

        // Post-fork edits: make the number range invalid, then change the description.
        await Page.Locator("div[aria-keyshortcuts='Alt+ArrowUp Alt+ArrowDown']").Nth(0).ClickAsync();
        await Page.GetByRole(AriaRole.Spinbutton).First.WaitForAsync();
        await builder.SetNumberRangeAsync("40", "10");
        await builder.SetDescriptionAsync("last change survives");
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();
        await Assertions.Expect(builder.PublishDialog).ToBeVisibleAsync();
        await builder.PublishCloseButton.ClickAsync();

        var form = await GetFormAsync(id);
        form.VersionNumber.Should().Be(2);
        form.Status.Should().Be(Hub.Contracts.FormVersionStatusDto.Draft);
        form.Description.Should().Be("last change survives");
        form.Pages[0].Questions.Should().HaveCount(2);
        var number = form.Pages[0].Questions.OfType<Hub.Contracts.NumberQuestionDto>().Single();
        number.Min.Should().Be(40m);
        number.Max.Should().Be(10m);
    }

    [Fact]
    public async Task TestPublish_When_IssueFixed_Should_PublishOnRetry()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Retry {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "temp");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").FillAsync("");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").BlurAsync();
        await WaitForSavedAsync();

        await builder.PublishButton.ClickAsync();
        await builder.PublishCloseButton.ClickAsync();

        // Fix the blank question, then publish for real.
        await Page.GetByPlaceholder("e.g. Which department are you joining?").FillAsync("How did you hear about us?");
        await Page.GetByPlaceholder("e.g. Which department are you joining?").BlurAsync();
        await WaitForSavedAsync();
        await ClickPublishAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Status.Should().Be(Hub.Contracts.FormVersionStatusDto.Published);
    }
}
