using System.Net.Http;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.FormBuilder.Services;
using PhieuFlow.Hub.Contracts;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class FormsListSessionTests
{
    [Fact]
    public async Task TestSetView_When_QueryMatchesTitle_Should_FilterFilteredForms()
    {
        var forms = new FakeFormsService { Batches = [[FormWith("Alpha"), FormWith("Beta")]] };
        var session = new FormsListSession(forms);
        await session.LoadAsync();

        session.SetView("alpha", FormListTab.All, FormListSortColumn.Modified, true, 1);

        session.FilteredForms.Should().ContainSingle(f => f.Title == "Alpha");
    }

    [Fact]
    public async Task TestSetView_When_TabIsLive_Should_ExcludeNeverPublishedForms()
    {
        var live = FormWith("Live form", latestPublishedVersionNumber: 1);
        var draft = FormWith("Draft form");
        var forms = new FakeFormsService { Batches = [[live, draft]] };
        var session = new FormsListSession(forms);
        await session.LoadAsync();

        session.SetView(string.Empty, FormListTab.Live, FormListSortColumn.Modified, true, 1);

        session.FilteredForms.Should().ContainSingle(f => f.Title == "Live form");
    }

    [Fact]
    public async Task TestSetView_When_SortIsStatus_Should_RankNeverPublishedBeforeUnpublishedEditsBeforeLive()
    {
        var neverPublished = FormWith("Never");
        var unpublishedEdits = FormWith("UnpublishedEdits", FormStatus.Draft, latestPublishedVersionNumber: 1);
        var live = FormWith("Live", FormStatus.Published, latestPublishedVersionNumber: 1);
        var forms = new FakeFormsService { Batches = [[live, unpublishedEdits, neverPublished]] };
        var session = new FormsListSession(forms);
        await session.LoadAsync();

        session.SetView(string.Empty, FormListTab.All, FormListSortColumn.Status, false, 1);

        session.FilteredForms.Select(f => f.Title).Should().ContainInOrder("Never", "UnpublishedEdits", "Live");
    }

    [Fact]
    public async Task TestLoadAsync_When_MultipleBatchesArrive_Should_RaiseChangedAfterEachBatch()
    {
        var forms = new FakeFormsService { Batches = [[FormWith("A")], [FormWith("B")]] };
        var session = new FormsListSession(forms);
        var changedCount = 0;
        session.Changed += () => changedCount++;

        await session.LoadAsync();

        changedCount.Should().BeGreaterThanOrEqualTo(2);
        session.FilteredForms.Should().HaveCount(2);
    }

    [Fact]
    public async Task TestPublishAsync_When_FormAlreadyPublished_Should_NoOpAndReturnNull()
    {
        var form = FormWith("Already published", FormStatus.Published, latestPublishedVersionNumber: 1);
        var forms = new FakeFormsService();
        var session = new FormsListSession(forms);

        var result = await session.PublishAsync(form);

        result.Should().BeNull();
        forms.PublishCalls.Should().Be(0);
    }

    [Fact]
    public async Task TestPublishAsync_When_HubUnreachable_Should_SetActionError()
    {
        var form = FormWith("Draft");
        var forms = new FakeFormsService { PublishError = new HttpRequestException() };
        var session = new FormsListSession(forms);

        var result = await session.PublishAsync(form);

        result.Should().BeNull();
        session.ActionError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TestDuplicateAsync_When_NewFormLoadsSuccessfully_Should_PrependToFilteredForms()
    {
        var source = FormWith("Source");
        var newId = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            Batches = [[source]],
            DuplicateNewId = newId,
            OnGetById = _ => new FormEditModel { FormId = newId, Title = "Copy of Source" },
        };
        var session = new FormsListSession(forms);
        await session.LoadAsync();

        await session.DuplicateAsync(source);

        session.FilteredForms.Should().Contain(f => f.Title == "Copy of Source");
    }

    [Fact]
    public async Task TestDeleteAsync_When_FormRemoved_Should_UpdateClampedPage()
    {
        var toDelete = FormWith("Only form");
        var forms = new FakeFormsService { Batches = [[toDelete]] };
        var session = new FormsListSession(forms);
        await session.LoadAsync();
        session.SetView(string.Empty, FormListTab.All, FormListSortColumn.Modified, true, 5);

        await session.DeleteAsync(toDelete);

        session.ClampedPage.Should().Be(1);
        session.FilteredForms.Should().BeEmpty();
    }

    private static FormSummary FormWith(string title, FormStatus status = FormStatus.Draft, int? latestPublishedVersionNumber = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        LastModifiedBy = string.Empty,
        Revision = 1,
        VersionNumber = 1,
        LatestPublishedVersionNumber = latestPublishedVersionNumber,
        QuestionCount = 0,
        PageCount = 1,
    };

    private sealed class FakeFormsService : IFormsService
    {
        public List<List<FormSummary>> Batches { get; init; } = [];

        public Exception? PublishError { get; init; }

        public PublishResultDto? PublishResult { get; init; }

        public Guid DuplicateNewId { get; init; } = Guid.NewGuid();

        public Func<Guid, FormEditModel?>? OnGetById { get; init; }

        public Exception? DeleteError { get; init; }

        public int PublishCalls { get; private set; }

        public Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<List<FormSummary>> GetAllStreamingAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var batch in Batches)
            {
                await Task.Yield();
                yield return batch;
            }
        }

        public Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OnGetById?.Invoke(formId));

        public Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            return PublishError is not null
                ? Task.FromException<PublishResultDto>(PublishError)
                : Task.FromResult(PublishResult ?? new PublishResultDto
                {
                    Published = true,
                    Form = new FormDto
                    {
                        Id = formId,
                        Title = string.Empty,
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastModifiedAt = DateTimeOffset.UtcNow,
                        Revision = 1,
                        VersionNumber = 1,
                        Status = FormVersionStatusDto.Published,
                        Pages = [],
                    },
                    VersionNumber = 1,
                    IsFirstPublish = true,
                    Revision = 1,
                    Status = FormVersionStatusDto.Published,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                    PublishedAt = DateTimeOffset.UtcNow,
                });
        }

        public Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(FormEditModel local, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default) =>
            DeleteError is not null ? Task.FromException(DeleteError) : Task.CompletedTask;

        public Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DuplicateNewId);
    }
}
