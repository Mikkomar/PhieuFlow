using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>POST /forms/{id}/duplicate</c> deep-copies a form's latest version into a new draft
/// in one transaction, so a failure leaves no orphan. 404s for an unknown source id. The
/// <c>forms:write</c> scope gate is in <see cref="HubAuthorizationTests"/>.
/// </summary>
public sealed class FormDuplicateTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient WriteClient => CreateClient();

    [Fact]
    public async Task TestDuplicate_Should_CreateANewFormWithCopiedContentAndResetVersionState()
    {
        using var client = WriteClient;
        var sourceId = await CreateFormAsync(client, "Original");

        var response = await client.PostAsync($"/forms/{sourceId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<FormCreatedDto>();
        created!.Id.Should().NotBe(Guid.Empty).And.NotBe(sourceId);

        var copy = await client.GetFromJsonAsync<FormDto>($"/forms/{created.Id}");
        copy!.Title.Should().Be("Copy of Original");
        copy.VersionNumber.Should().Be(1);
        copy.Revision.Should().Be(1);
        copy.Status.Should().Be(FormVersionStatusDto.Draft);

        var sourcePage = (await client.GetFromJsonAsync<FormDto>($"/forms/{sourceId}"))!.Pages.Single();
        var copyPage = copy.Pages.Should().ContainSingle().Subject;
        copyPage.Id.Should().NotBe(sourcePage.Id);
        copyPage.Title.Should().Be(sourcePage.Title);

        var sourceQuestion = sourcePage.Questions.Should().ContainSingle().Subject;
        var copyQuestion = copyPage.Questions.Should().ContainSingle().Subject;
        copyQuestion.Id.Should().NotBe(sourceQuestion.Id);
        copyQuestion.Text.Should().Be(sourceQuestion.Text);
    }

    [Fact]
    public async Task TestDuplicate_When_SourceDoesNotExist_Should_Return404()
    {
        using var client = WriteClient;

        var response = await client.PostAsync($"/forms/{Guid.NewGuid()}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestDuplicate_When_SourceHasNoTitle_Should_NameTheCopyCopyOfUntitledForm()
    {
        using var client = WriteClient;
        // POST /forms creates a blank draft, so Title is empty and never set.
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();

        var response = await client.PostAsync($"/forms/{created!.Id}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var copyId = (await response.Content.ReadFromJsonAsync<FormCreatedDto>())!.Id;
        var copy = await client.GetFromJsonAsync<FormDto>($"/forms/{copyId}");
        copy!.Title.Should().Be("Copy of untitled form");
    }

    [Fact]
    public async Task TestDuplicate_When_SourceHasEveryQuestionType_Should_DeepCopyEachWithFreshIds()
    {
        using var client = WriteClient;
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var sourceId = created!.Id;
        (await client.PutAsJsonAsync($"/forms/{sourceId}", TestForms.AllQuestionTypes(sourceId, "Master")))
            .EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/forms/{sourceId}/duplicate", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var copyId = (await response.Content.ReadFromJsonAsync<FormCreatedDto>())!.Id;

        var source = await client.GetFromJsonAsync<FormDto>($"/forms/{sourceId}");
        var copy = await client.GetFromJsonAsync<FormDto>($"/forms/{copyId}");

        copy!.Title.Should().Be("Copy of Master");
        var sourceQuestions = source!.Pages.Single().Questions;
        var copyQuestions = copy.Pages.Should().ContainSingle().Subject.Questions;

        copyQuestions.Select(q => q.GetType()).Should().Equal(sourceQuestions.Select(q => q.GetType()));
        copyQuestions.Select(q => q.Id).Should().NotIntersectWith(sourceQuestions.Select(q => q.Id));
        copyQuestions.Select(q => q.Text).Should().Equal(sourceQuestions.Select(q => q.Text));

        var sourceOptionIds = sourceQuestions.OfType<ChoiceQuestionDto>().SelectMany(c => c.Options).Select(o => o.Id);
        var copyChoices = copyQuestions.OfType<ChoiceQuestionDto>().ToList();
        copyChoices.SelectMany(c => c.Options).Select(o => o.Id).Should().NotIntersectWith(sourceOptionIds);
        copyChoices.Should().HaveCount(3);
        copyChoices.SelectMany(c => c.Options.Select(o => o.Label))
            .Should().Contain(["Finland", "Permanent", "Laptop"]);
    }

    private static async Task<Guid> CreateFormAsync(HttpClient client, string title)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        await client.PutAsJsonAsync($"/forms/{id}", new FormDto
        {
            Id = id,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Revision = 1,
            VersionNumber = 1,
            Status = FormVersionStatusDto.Draft,
            Pages =
            [
                new FormPageDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Page 1",
                    Questions = [new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Answer me", IsRequired = false }],
                },
            ],
        });

        return id;
    }
}
