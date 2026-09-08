using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Hub.Submissions;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>GET /forms/{id}/submissions</c> reads back persisted submissions: a keyset-paged
/// batch whose answers are display strings, option ids resolved to labels. The
/// <c>submissions:read</c> scope gate is in <see cref="HubAuthorizationTests"/>.
/// </summary>
public sealed class SubmissionListTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task TestGetSubmissions_When_FormHasResponses_Should_ReturnAnswersWithResolvedLabels()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Readable responses");
        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();
        var radio = form.Pages[0].Questions.OfType<RadioButtonQuestionDto>().Single();
        var group = form.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single();
        var checkbox = form.Pages[0].Questions.OfType<CheckboxQuestionDto>().Single();

        await PersistAsync(form, [
            new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "Some prose" },
            new BooleanAnswerDto { QuestionId = checkbox.Id, QuestionText = checkbox.Text, Order = 1, Checked = true },
            new OptionAnswerDto { QuestionId = radio.Id, QuestionText = radio.Text, Order = 2, OptionId = radio.Options[1].Id },
            new OptionAnswerDto { QuestionId = group.Id, QuestionText = group.Text, Order = 3, OptionId = group.Options[0].Id },
            new OptionAnswerDto { QuestionId = group.Id, QuestionText = group.Text, Order = 4, OptionId = group.Options[2].Id },
        ]);

        var batch = await client.GetFromJsonAsync<SubmissionBatchResponse>($"/forms/{form.Id}/submissions");

        batch!.Items.Should().ContainSingle();
        var submission = batch.Items[0];
        submission.FormVersionNumber.Should().Be(form.VersionNumber);

        var answers = submission.Answers.ToDictionary(a => a.QuestionText, a => a.Value);
        answers[text.Text].Should().Be("Some prose");
        answers[checkbox.Text].Should().Be("Yes");
        answers[radio.Text].Should().Be(radio.Options[1].Label);
        // A checkbox group's per-selection rows collapse into one comma-joined entry.
        answers[group.Text].Should().Be($"{group.Options[0].Label}, {group.Options[2].Label}");
    }

    [Fact]
    public async Task TestGetSubmissions_When_MoreThanTake_Should_PageByStartId()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Paged responses");
        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();

        for (var i = 0; i < 3; i++)
        {
            await PersistAsync(form, [
                new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = $"answer {i}" },
            ]);
        }

        var first = await client.GetFromJsonAsync<SubmissionBatchResponse>($"/forms/{form.Id}/submissions?take=2");
        first!.Items.Should().HaveCount(2);
        first.NextStartId.Should().NotBeNull();

        var second = await client.GetFromJsonAsync<SubmissionBatchResponse>(
            $"/forms/{form.Id}/submissions?take=2&startId={first.NextStartId}");
        second!.Items.Should().ContainSingle();
        second.NextStartId.Should().BeNull();

        first.Items.Concat(second.Items).Select(i => i.Id).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [Fact]
    public async Task TestGetSubmissions_When_FormHasNoResponses_Should_ReturnEmptyBatch()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Quiet form");

        var batch = await client.GetFromJsonAsync<SubmissionBatchResponse>($"/forms/{form.Id}/submissions");

        batch!.Items.Should().BeEmpty();
        batch.NextStartId.Should().BeNull();
    }

    [Fact]
    public async Task TestGetSubmissions_When_FormUnknown_Should_Return404()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/forms/{Guid.NewGuid()}/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestGetSubmissions_When_TakeOutOfRange_Should_Return400()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Bad take");

        (await client.GetAsync($"/forms/{form.Id}/submissions?take=0")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/forms/{form.Id}/submissions?take=101")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task PersistAsync(PublishedFormDto form, List<SubmissionAnswerDto> answers)
    {
        using var scope = Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SubmissionMessageHandler>();
        var result = await handler.HandleAsync(
            new FormSubmissionRequest
            {
                FormId = form.Id,
                FormVersionNumber = form.VersionNumber,
                Answers = answers,
            },
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().Be(SubmissionProcessingResult.Persisted);
    }

    private static async Task<PublishedFormDto> PublishOptionalFormAsync(HttpClient client, string title)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        (await client.PutAsJsonAsync($"/forms/{id}", TestForms.AllOptionalQuestions(id, title)))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/forms/{id}/publish", content: null))
            .EnsureSuccessStatusCode();

        return (await client.GetFromJsonAsync<PublishedFormDto>($"/forms/published/{id}"))!;
    }
}
