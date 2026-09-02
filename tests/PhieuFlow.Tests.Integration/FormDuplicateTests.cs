using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>POST /forms/{id}/duplicate</c> deep-copies a form's latest version into a new draft in
/// one transaction — the create and the copy either both land or neither does, so a failure
/// can't leave a blank orphan form behind. Needs <c>forms:write</c>, and 404s for an unknown
/// source id.
/// </summary>
public sealed class FormDuplicateTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    private HttpClient ReadOnlyClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

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
    public async Task TestDuplicate_When_TokenLacksWriteScope_Should_Return403()
    {
        var sourceId = await CreateFormAsync(WriteClient, "Guarded");

        using var readOnly = ReadOnlyClient;
        var response = await readOnly.PostAsync($"/forms/{sourceId}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
