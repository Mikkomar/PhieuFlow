using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Persistence;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>DELETE /forms/{id}</c> removes the form and its whole version tree (cascade) and
/// 404s for an unknown id so the client can distinguish "gone" from "never existed". The
/// <c>forms:write</c> scope gate is covered in <see cref="HubAuthorizationTests"/>.
/// </summary>
public sealed class FormDeleteTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient WriteClient => CreateClient();

    [Fact]
    public async Task TestDelete_Should_RemoveTheFormFromLookupAndListing()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client, "Doomed");

        var response = await client.DeleteAsync($"/forms/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/forms/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listing = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");
        listing!.Items.Should().NotContain(i => i.Id == id);
    }

    [Fact]
    public async Task TestDelete_When_FormDoesNotExist_Should_Return404()
    {
        using var client = WriteClient;

        var response = await client.DeleteAsync($"/forms/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestDelete_When_FormHasASubmission_Should_Return500AndKeepTheForm()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client, "Has responses");
        (await client.PostAsync($"/forms/{id}/publish", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AddSubmissionAsync(id);

        var response = await client.DeleteAsync($"/forms/{id}");

        // TODO: FormSubmission FKs are DeleteBehavior.Restrict — a deliberate guard against
        // destroying response data. SQL Server rejects the DELETE, EF wraps it as
        // DbUpdateException, and FormEndpoints has no exception handler, so it surfaces as a
        // bare 500. It should return 409 Conflict.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        (await client.GetAsync($"/forms/{id}")).StatusCode.Should().Be(HttpStatusCode.OK, "nothing was deleted");
    }

    private async Task AddSubmissionAsync(Guid formId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var versionId = await db.FormVersions
            .Where(v => v.FormId == formId)
            .Select(v => v.Id)
            .FirstAsync();

        var submissionId = Guid.NewGuid();
        db.FormSubmissions.Add(new FormSubmission
        {
            Id = submissionId,
            FormId = formId,
            FormVersionId = versionId,
            FormVersionNumber = 1,
            SubmittedAt = DateTimeOffset.UtcNow,
            Answers =
            {
                new ValueSubmissionAnswer
                {
                    Id = Guid.NewGuid(), FormSubmissionId = submissionId,
                    QuestionId = Guid.NewGuid(), QuestionText = "Answer me", Order = 0, Value = "a response",
                },
                new OptionSubmissionAnswer
                {
                    Id = Guid.NewGuid(), FormSubmissionId = submissionId,
                    QuestionId = Guid.NewGuid(), QuestionText = "Pick one", Order = 1, OptionId = Guid.NewGuid(),
                },
            },
        });
        await db.SaveChangesAsync();
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
