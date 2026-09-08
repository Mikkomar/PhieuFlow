using System.Net.Http;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.FormBuilder.Services;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class FormResponsesSessionTests
{
    private static readonly Guid FormId = Guid.NewGuid();

    [Fact]
    public async Task TestLoadAsync_When_HubStreamsBatches_Should_AccumulateRowsNewestFirst()
    {
        var older = Response(DateTimeOffset.UtcNow.AddHours(-2), ("q1", "Question 1", 0, "old"));
        var newer = Response(DateTimeOffset.UtcNow, ("q1", "Question 1", 0, "new"));
        var forms = new FakeFormsService { Batches = [[older], [newer]] };
        var session = new FormResponsesSession(forms);

        await session.LoadAsync(FormId);

        session.TotalCount.Should().Be(2);
        session.PagedResponses.Select(r => r.Answers[0].Value).Should().ContainInOrder("new", "old");
        session.LoadError.Should().BeNull();
        session.Loading.Should().BeFalse();
    }

    [Fact]
    public async Task TestLoadAsync_When_NoSubmissions_Should_ReportEmpty()
    {
        var session = new FormResponsesSession(new FakeFormsService { Batches = [[]] });

        await session.LoadAsync(FormId);

        session.TotalCount.Should().Be(0);
        session.Columns.Should().BeEmpty();
        session.LoadError.Should().BeNull();
    }

    [Fact]
    public async Task TestLoadAsync_When_HubThrows_Should_SurfaceLoadError()
    {
        var session = new FormResponsesSession(new FakeFormsService { Error = new HttpRequestException() });

        await session.LoadAsync(FormId);

        session.LoadError.Should().NotBeNullOrWhiteSpace();
        session.Loading.Should().BeFalse();
    }

    [Fact]
    public async Task TestLoadAsync_When_SubmissionsSpanQuestionSets_Should_UnionColumnsByOrder()
    {
        // An older submission answered q1, q2; a newer one (a forked version) dropped q2 and
        // added q3 further down the form.
        var v1 = Response(DateTimeOffset.UtcNow.AddHours(-1),
            ("q1", "First", 0, "a"), ("q2", "Second", 1, "b"));
        var v2 = Response(DateTimeOffset.UtcNow,
            ("q1", "First", 0, "c"), ("q3", "Third", 2, "d"));

        var session = new FormResponsesSession(new FakeFormsService { Batches = [[v1, v2]] });

        await session.LoadAsync(FormId);

        session.Columns.Select(c => c.QuestionText).Should().ContainInOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task TestSetPage_When_MoreRowsThanPageSize_Should_ClampAndSlice()
    {
        var many = Enumerable.Range(0, FormResponsesSession.PageSize + 5)
            .Select(i => Response(DateTimeOffset.UtcNow.AddMinutes(-i), ("q1", "Q", 0, $"v{i}")))
            .ToList();
        var session = new FormResponsesSession(new FakeFormsService { Batches = [many] });
        await session.LoadAsync(FormId);

        session.TotalPages.Should().Be(2);

        session.SetPage(99);

        session.ClampedPage.Should().Be(2);
        session.PagedResponses.Should().HaveCount(5);
    }

    private static Guid QId(string seed)
    {
        var bytes = new byte[16];
        System.Text.Encoding.UTF8.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static FormResponse Response(DateTimeOffset submittedAt, params (string Id, string Text, int Order, string? Value)[] answers) => new()
    {
        Id = Guid.NewGuid(),
        SubmittedAt = submittedAt,
        FormVersionNumber = 1,
        Answers = answers
            .Select(a => new FormResponseAnswer
            {
                QuestionId = QId(a.Id),
                QuestionText = a.Text,
                Order = a.Order,
                Value = a.Value,
            })
            .ToList(),
    };

    private sealed class FakeFormsService : IFormsService
    {
        public List<List<FormResponse>> Batches { get; init; } = [];

        public Exception? Error { get; init; }

        public async IAsyncEnumerable<List<FormResponse>> GetSubmissionsStreamingAsync(
            Guid formId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Error is not null)
            {
                await Task.Yield();
                throw Error;
            }

            foreach (var batch in Batches)
            {
                await Task.Yield();
                yield return batch;
            }
        }

        public Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<List<FormSummary>> GetAllStreamingAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(FormEditModel local, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
