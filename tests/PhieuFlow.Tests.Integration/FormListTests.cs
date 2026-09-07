using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>GET /forms</c> — the builder-facing list (<c>FormRepository.GetBatchAsync</c>), the
/// untested twin of <see cref="FormPublishedListTests"/>. Exercises the Guid cursor
/// (<c>Id &gt;= startId</c> + <c>ORDER BY Id</c> under SQL Server's <c>uniqueidentifier</c>
/// ordering, which is not .NET Guid order), the <c>Take(take + 1)</c> / <c>NextStartId</c>
/// trim, the correlated-subquery projection (current version, latest published) and the
/// <c>PageCount</c>/<c>QuestionCount</c> aggregates.
/// </summary>
public sealed class FormListTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task TestGetForms_When_TakeIsOutOfRange_Should_Return400(int take)
    {
        using var client = CreateClient();

        (await client.GetAsync($"/forms?take={take}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TestGetForms_When_MoreItemsExistThanTake_Should_PageThroughEveryFormExactlyOnce()
    {
        using var client = CreateClient();
        var created = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            created.Add(await CreateBlankAsync(client));
        }

        // Each test starts from an empty (rolled-back) database, so exactly these 5 forms exist.
        var seen = new List<Guid>();
        Guid? startId = null;
        var continuations = 0;
        do
        {
            var url = startId is null ? "/forms?take=1" : $"/forms?take=1&startId={startId}";
            var page = await client.GetFromJsonAsync<FormBatchResponse>(url);
            page!.Items.Should().ContainSingle();
            seen.Add(page.Items[0].Id);
            if (startId is not null)
            {
                continuations++;
            }

            startId = page.NextStartId;
        }
        while (startId is not null);

        continuations.Should().Be(4, "5 forms at take=1 need 4 continuations");
        seen.Should().BeEquivalentTo(created, "the cursor must tile the whole set with no gaps or repeats");
    }

    [Fact]
    public async Task TestGetForms_When_FormHasPagesAndQuestions_Should_ProjectPageAndQuestionCounts()
    {
        using var client = CreateClient();
        var id = await CreateBlankAsync(client);
        var form = TestForms.SingleTextQuestion(id, "Counted");
        form.Pages.Add(new FormPageDto
        {
            Id = Guid.NewGuid(),
            Title = "Page 2",
            Questions =
            [
                new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Q2", IsRequired = false },
                new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Q3", IsRequired = false },
            ],
        });
        (await client.PutAsJsonAsync($"/forms/{id}", form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await SingleItemAsync(client, id);

        item.PageCount.Should().Be(2);
        item.QuestionCount.Should().Be(3);
    }

    [Fact]
    public async Task TestGetForms_When_FormHasNoQuestions_Should_ReportZeroQuestionCount()
    {
        using var client = CreateClient();
        var id = await CreateBlankAsync(client); // POST /forms mints one empty page, no questions

        var item = await SingleItemAsync(client, id);

        item.PageCount.Should().Be(1);
        item.QuestionCount.Should().Be(0, "SUM over an empty set must coalesce to 0");
    }

    [Fact]
    public async Task TestGetForms_When_PublishedFormHasANewerDraft_Should_ShowDraftStateAndLatestPublishedPointer()
    {
        using var client = CreateClient();
        var id = await CreateBlankAsync(client);

        var draft = TestForms.SingleTextQuestion(id, "Live then edited");
        (await client.PutAsJsonAsync($"/forms/{id}", draft)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/forms/{id}/publish", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Edit the published version → forks v2 draft.
        var current = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        current!.Title = "Unpublished edit";
        (await client.PutAsJsonAsync($"/forms/{id}", current)).StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await SingleItemAsync(client, id);

        item.VersionNumber.Should().Be(2);
        item.Status.Should().Be(FormVersionStatusDto.Draft);
        item.LatestPublishedVersionNumber.Should().Be(1);
        item.LatestPublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TestGetForms_When_FormHasASubmission_Should_ReportHasSubmissions()
    {
        using var client = CreateClient();
        var withReplies = await CreateBlankAsync(client);
        var untouched = await CreateBlankAsync(client);
        await SubmissionSeed.AddAsync(Services, withReplies);

        var batch = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");

        batch!.Items.Should().ContainSingle(i => i.Id == withReplies)
            .Subject.HasSubmissions.Should().BeTrue();
        batch.Items.Should().ContainSingle(i => i.Id == untouched)
            .Subject.HasSubmissions.Should().BeFalse("no submission was seeded for it");
    }

    private static async Task<FormListItemDto> SingleItemAsync(HttpClient client, Guid id)
    {
        var batch = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");
        return batch!.Items.Should().ContainSingle(i => i.Id == id).Subject;
    }

    private static async Task<Guid> CreateBlankAsync(HttpClient client)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        return created!.Id;
    }
}
