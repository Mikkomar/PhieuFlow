using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Hub.Submissions;
using PhieuFlow.Persistence;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <see cref="SubmissionMessageHandler"/> against the real database — the persistence half
/// of the RabbitMQ consumer (ADR 0009), exercised with no broker. Covers the typed-answer
/// mapping, the <c>ProcessedMessages</c> inbox dedup, and the poison classifications the
/// consumer turns into a dead-letter. The transport itself (ack / nack / dead-letter wiring)
/// is left to a manual check via the management plugin.
/// </summary>
public sealed class SubmissionConsumeTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task TestHandleAsync_When_SubmissionIsNew_Should_PersistTypedAnswerRowsAndInboxRow()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "New submission");

        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();
        var checkbox = form.Pages[0].Questions.OfType<CheckboxQuestionDto>().Single();
        var radio = form.Pages[0].Questions.OfType<RadioButtonQuestionDto>().Single();

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id,
            FormVersionNumber = form.VersionNumber,
            Answers =
            [
                new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "Some prose" },
                new BooleanAnswerDto { QuestionId = checkbox.Id, QuestionText = checkbox.Text, Order = 1, Checked = true },
                new OptionAnswerDto
                {
                    QuestionId = radio.Id, QuestionText = radio.Text, Order = 2, OptionId = radio.Options[0].Id,
                },
            ],
        };

        var result = await HandleAsync(request, messageId);

        result.Should().Be(SubmissionProcessingResult.Persisted);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        var expectedVersionId = await db.FormVersions
            .Where(v => v.FormId == form.Id && v.VersionNumber == form.VersionNumber)
            .Select(v => v.Id)
            .SingleAsync();

        var submission = await db.FormSubmissions
            .Include(s => s.Answers)
            .SingleAsync(s => s.FormId == form.Id);

        submission.FormVersionId.Should().Be(expectedVersionId);
        submission.FormVersionNumber.Should().Be(form.VersionNumber);
        submission.SubmittedAt.Should().NotBe(default);

        submission.Answers.OfType<ValueSubmissionAnswer>().Single()
            .Should().BeEquivalentTo(new { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "Some prose" });
        submission.Answers.OfType<BooleanSubmissionAnswer>().Single()
            .Should().BeEquivalentTo(new { QuestionId = checkbox.Id, Order = 1, Checked = true });
        submission.Answers.OfType<OptionSubmissionAnswer>().Single()
            .Should().BeEquivalentTo(new { QuestionId = radio.Id, Order = 2, OptionId = radio.Options[0].Id });

        (await db.ProcessedMessages.SingleAsync(m => m.MessageId == messageId))
            .ProcessedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task TestHandleAsync_When_MessageIdAlreadyProcessed_Should_IgnoreAndKeepExactlyOneSubmission()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Redelivered");
        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();

        var messageId = Guid.NewGuid();
        FormSubmissionRequest Request() => new()
        {
            FormId = form.Id,
            FormVersionNumber = form.VersionNumber,
            Answers = [new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "once" }],
        };

        (await HandleAsync(Request(), messageId)).Should().Be(SubmissionProcessingResult.Persisted);
        (await HandleAsync(Request(), messageId)).Should().Be(SubmissionProcessingResult.DuplicateIgnored);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        (await db.FormSubmissions.CountAsync(s => s.FormId == form.Id)).Should().Be(1);
        (await db.ProcessedMessages.CountAsync(m => m.MessageId == messageId)).Should().Be(1);
    }

    [Fact]
    public async Task TestHandleAsync_When_FormVersionNumberUnknown_Should_ReturnPoisonAndPersistNothing()
    {
        using var client = CreateClient();
        var form = await PublishAllTypesFormAsync(client, "Unknown version");
        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id,
            FormVersionNumber = 999,
            Answers = [new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "x" }],
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Poison);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        (await db.FormSubmissions.AnyAsync(s => s.FormId == form.Id)).Should().BeFalse();
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleAsync_When_FormIdUnknown_Should_ReturnPoisonAndPersistNothing()
    {
        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = Guid.NewGuid(),
            FormVersionNumber = 1,
            Answers = [],
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Poison);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        (await db.FormSubmissions.AnyAsync(s => s.FormId == request.FormId)).Should().BeFalse();
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleAsync_When_CheckBoxGroupHasMultipleSelections_Should_PersistOneOptionRowPerSelection()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "Multi select");
        var group = form.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single();

        var request = new FormSubmissionRequest
        {
            FormId = form.Id,
            FormVersionNumber = form.VersionNumber,
            Answers =
            [
                new OptionAnswerDto
                {
                    QuestionId = group.Id, QuestionText = group.Text, Order = 0, OptionId = group.Options[0].Id,
                },
                new OptionAnswerDto
                {
                    QuestionId = group.Id, QuestionText = group.Text, Order = 1, OptionId = group.Options[2].Id,
                },
            ],
        };

        (await HandleAsync(request, Guid.NewGuid())).Should().Be(SubmissionProcessingResult.Persisted);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        var options = await db.FormSubmissions
            .Where(s => s.FormId == form.Id)
            .SelectMany(s => s.Answers)
            .OfType<OptionSubmissionAnswer>()
            .ToListAsync();

        options.Should().HaveCount(2);
        options.Select(o => o.OptionId).Should().BeEquivalentTo([group.Options[0].Id, group.Options[2].Id]);
        options.Select(o => o.Order).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TestHandleAsync_When_AnswerListIsEmpty_Should_PersistSubmissionWithNoAnswerRows()
    {
        using var client = CreateClient();
        var form = await PublishOptionalFormAsync(client, "No answers");

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id,
            FormVersionNumber = form.VersionNumber,
            Answers = [],
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Persisted);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        var submission = await db.FormSubmissions.Include(s => s.Answers).SingleAsync(s => s.FormId == form.Id);
        submission.Answers.Should().BeEmpty();
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeTrue();
    }

    [Fact]
    public async Task TestHandleAsync_When_RequiredAnswerMissing_Should_ReturnPoisonAndPersistNothing()
    {
        using var client = CreateClient();
        var form = await PublishAllTypesFormAsync(client, "Missing required");
        var text = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single();

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id,
            FormVersionNumber = form.VersionNumber,
            // Only the text answer; the required checkbox / radio / group / date are absent.
            Answers = [new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "Some prose" }],
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Poison);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        (await db.FormSubmissions.AnyAsync(s => s.FormId == form.Id)).Should().BeFalse();
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleAsync_When_NumberAnswerOutOfRange_Should_ReturnPoisonAndPersistNothing()
    {
        using var client = CreateClient();
        var form = await PublishAllTypesFormAsync(client, "Number out of range");
        var number = form.Pages[0].Questions.OfType<NumberQuestionDto>().Single();

        var answers = ValidAnswersForAllTypes(form);
        answers[answers.FindIndex(a => a.QuestionId == number.Id)] =
            new ValueAnswerDto { QuestionId = number.Id, QuestionText = number.Text, Order = 5, Value = "5" }; // Min is 35.1234

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id, FormVersionNumber = form.VersionNumber, Answers = answers,
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Poison);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();
        (await db.FormSubmissions.AnyAsync(s => s.FormId == form.Id)).Should().BeFalse();
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleAsync_When_OptionIdNotInPublishedForm_Should_ReturnPoisonAndPersistNothing()
    {
        using var client = CreateClient();
        var form = await PublishAllTypesFormAsync(client, "Bogus option");
        var radio = form.Pages[0].Questions.OfType<RadioButtonQuestionDto>().Single();

        var answers = ValidAnswersForAllTypes(form);
        answers[answers.FindIndex(a => a.QuestionId == radio.Id)] =
            new OptionAnswerDto { QuestionId = radio.Id, QuestionText = radio.Text, Order = 3, OptionId = Guid.NewGuid() };

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id, FormVersionNumber = form.VersionNumber, Answers = answers,
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Poison);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();
        (await db.FormSubmissions.AnyAsync(s => s.FormId == form.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleAsync_When_EveryRequiredAnswerPresentAndValid_Should_Persist()
    {
        using var client = CreateClient();
        var form = await PublishAllTypesFormAsync(client, "Fully valid");

        var messageId = Guid.NewGuid();
        var request = new FormSubmissionRequest
        {
            FormId = form.Id, FormVersionNumber = form.VersionNumber, Answers = ValidAnswersForAllTypes(form),
        };

        (await HandleAsync(request, messageId)).Should().Be(SubmissionProcessingResult.Persisted);

        using var read = Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<HubDbContext>();

        var submission = await db.FormSubmissions.Include(s => s.Answers).SingleAsync(s => s.FormId == form.Id);
        submission.Answers.Should().HaveCount(7);
        (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId)).Should().BeTrue();
    }

    private async Task<SubmissionProcessingResult> HandleAsync(FormSubmissionRequest request, Guid messageId)
    {
        using var scope = Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SubmissionMessageHandler>();
        return await handler.HandleAsync(request, messageId, CancellationToken.None);
    }

    private static Task<PublishedFormDto> PublishAllTypesFormAsync(HttpClient client, string title) =>
        PublishFormAsync(client, title, TestForms.AllQuestionTypes);

    private static Task<PublishedFormDto> PublishOptionalFormAsync(HttpClient client, string title) =>
        PublishFormAsync(client, title, TestForms.AllOptionalQuestions);

    private static async Task<PublishedFormDto> PublishFormAsync(
        HttpClient client, string title, Func<Guid, string, FormDto> build)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        (await client.PutAsJsonAsync($"/forms/{id}", build(id, title)))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/forms/{id}/publish", content: null))
            .EnsureSuccessStatusCode();

        return (await client.GetFromJsonAsync<PublishedFormDto>($"/forms/published/{id}"))!;
    }

    // A complete, in-bounds answer for every question on TestForms.AllQuestionTypes.
    private static List<SubmissionAnswerDto> ValidAnswersForAllTypes(PublishedFormDto form)
    {
        var q = form.Pages[0].Questions;
        var text = q.OfType<TextAreaQuestionDto>().Single();
        var checkbox = q.OfType<CheckboxQuestionDto>().Single();
        var dropDown = q.OfType<DropDownQuestionDto>().Single();
        var radio = q.OfType<RadioButtonQuestionDto>().Single();
        var group = q.OfType<CheckBoxGroupQuestionDto>().Single();
        var number = q.OfType<NumberQuestionDto>().Single();
        var calendar = q.OfType<CalendarQuestionDto>().Single();

        return
        [
            new ValueAnswerDto { QuestionId = text.Id, QuestionText = text.Text, Order = 0, Value = "Some prose" },
            new BooleanAnswerDto { QuestionId = checkbox.Id, QuestionText = checkbox.Text, Order = 1, Checked = true },
            new OptionAnswerDto { QuestionId = dropDown.Id, QuestionText = dropDown.Text, Order = 2, OptionId = dropDown.Options[0].Id },
            new OptionAnswerDto { QuestionId = radio.Id, QuestionText = radio.Text, Order = 3, OptionId = radio.Options[0].Id },
            new OptionAnswerDto { QuestionId = group.Id, QuestionText = group.Text, Order = 4, OptionId = group.Options[0].Id },
            new ValueAnswerDto { QuestionId = number.Id, QuestionText = number.Text, Order = 5, Value = "40" },
            new ValueAnswerDto { QuestionId = calendar.Id, QuestionText = calendar.Text, Order = 6, Value = "2026-06-15" },
        ];
    }
}
