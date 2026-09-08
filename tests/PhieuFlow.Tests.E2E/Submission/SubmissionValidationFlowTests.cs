using Microsoft.Playwright;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Submission;

/// <summary>
/// The respondent app validates answers before publishing, because the submission crosses
/// an async queue and the Hub cannot reject it in-band. A required question left blank
/// must block the message, and clear once the respondent answers.
/// </summary>
public sealed class SubmissionValidationFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestSubmit_When_RequiredQuestionLeftBlank_Should_BlockSubmissionUntilAnswered()
    {
        // Context A: publish a one-page form with a single required question.
        var builder = new FormBuilderPage(Page);
        var title = $"Validate-required {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Your feedback");
        await builder.ToggleRequiredAsync();
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Context B: the respondent submits without answering.
        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

        // The message is never sent. The respondent stays on the form with an error shown.
        await Assertions.Expect(filler.GetByText("An answer is required.")).ToBeVisibleAsync();
        await Assertions.Expect(filler.GetByText("received")).Not.ToBeVisibleAsync();

        // Answering clears the error and lets the submission through.
        await filler.GetByRole(AriaRole.Textbox).FillAsync("It went well.");
        await Assertions.Expect(filler.GetByText("An answer is required.")).Not.ToBeVisibleAsync();
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();
    }
}
