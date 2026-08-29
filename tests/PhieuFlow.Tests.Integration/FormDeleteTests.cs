using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>DELETE /forms/{id}</c> removes the form and its whole version tree (cascade), needs
/// <c>forms:write</c>, and 404s for an unknown id so the client can distinguish "gone" from
/// "never existed".
/// </summary>
public sealed class FormDeleteTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    private HttpClient ReadOnlyClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

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
    public async Task TestDelete_When_TokenLacksWriteScope_Should_Return403()
    {
        var id = await CreateFormAsync(WriteClient, "Guarded");

        using var readOnly = ReadOnlyClient;
        var response = await readOnly.DeleteAsync($"/forms/{id}");

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
