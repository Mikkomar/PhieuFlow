using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Persistence.Projections;
using PhieuFlow.Persistence.Repositories;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// The publish gate. <c>POST /forms/{id}/publish</c> validates the persisted latest version
/// and either publishes (200) or returns the annotated tree (422) without publishing.
/// </summary>
public sealed class FormPublishGateTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    [Fact]
    public async Task TestPublish_When_FormHasBlankQuestion_Should_Return422WithAnnotatedTreeAndLeaveStatusDraft()
    {
        using var client = WriteClient;
        var id = await SaveFormAsync(client, "Gate", Question("Answer me"), Question(""));

        var response = await client.PostAsync($"/forms/{id}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var result = await response.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Published.Should().BeFalse();
        result.Form.Pages[0].Questions[1].Issues.Should().ContainSingle()
            .Which.Field.Should().Be(ValidationField.Text);

        var persisted = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        persisted!.Status.Should().Be(FormVersionStatusDto.Draft);
    }

    [Fact]
    public async Task TestPublish_When_FormIsClean_Should_PublishAndReturnPublishedAt()
    {
        using var client = WriteClient;
        var id = await SaveFormAsync(client, "Clean", Question("All good"));

        var response = await client.PostAsync($"/forms/{id}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Published.Should().BeTrue();
        result.PublishedAt.Should().NotBeNull();
        result.IsFirstPublish.Should().BeTrue();

        var persisted = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        persisted!.Status.Should().Be(FormVersionStatusDto.Published);
    }

    [Fact]
    public async Task TestPublish_When_AnotherSaveLandsBetweenValidateAndFlip_Should_RejectWithoutPublishing()
    {
        using var client = WriteClient;
        var id = await SaveFormAsync(client, "Raced", Question("All good"));
        // id is now v1/r1.

        // A second session's save lands (bumping to r2) — the exact window between the publish
        // handler's validate-read (which would have captured r1) and its flip.
        await client.PutAsJsonAsync($"/forms/{id}", new FormDto
        {
            Id = id,
            Title = "Raced (edited)",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Revision = 1,
            VersionNumber = 1,
            Status = FormVersionStatusDto.Draft,
            Pages = [new FormPageDto { Id = Guid.NewGuid(), Title = "Page 1", Questions = [Question("All good")] }],
        });

        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFormRepository>();

        var result = await repo.PublishAsync(id, expectedVersionNumber: 1, expectedRevision: 1, CancellationToken.None);

        result.Status.Should().Be(FormPublishStatus.RevisionMismatch);

        var persisted = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        persisted!.Status.Should().Be(FormVersionStatusDto.Draft);
    }

    private static async Task<Guid> SaveFormAsync(HttpClient client, string title, params QuestionDto[] questions)
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
            Pages = [new FormPageDto { Id = Guid.NewGuid(), Title = "Page 1", Questions = questions.ToList() }],
        });

        return id;
    }

    private static QuestionDto Question(string text) => new TextAreaQuestionDto
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
    };
}
