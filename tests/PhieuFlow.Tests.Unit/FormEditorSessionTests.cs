using System.Net.Http;
using AwesomeAssertions;
using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.FormBuilder.Services;
using PhieuFlow.Hub.Contracts;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class FormEditorSessionTests
{
    [Fact]
    public async Task TestOpenAsync_When_FormIdIsNull_Should_RedirectToNewlyMintedForm()
    {
        var forms = new FakeFormsService();
        await using var session = new FormEditorSession(forms);

        var outcome = await session.OpenAsync(null);

        outcome.Kind.Should().Be(OpenOutcomeKind.RedirectToNew);
        outcome.NewFormId.Should().Be(forms.NewFormId);
    }

    [Fact]
    public async Task TestOpenAsync_When_FormMissing_Should_ReportNotFound()
    {
        var forms = new FakeFormsService { OnGetById = _ => null };
        await using var session = new FormEditorSession(forms);

        var outcome = await session.OpenAsync(Guid.NewGuid());

        outcome.Should().Be(OpenOutcome.Failed);
        session.LoadState.Should().Be(FormLoadState.NotFound);
        session.LoadError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TestOpenAsync_When_ServerErrors_Should_ReportError()
    {
        var forms = new FakeFormsService { GetByIdError = new HttpRequestException("hub down") };
        await using var session = new FormEditorSession(forms);

        var outcome = await session.OpenAsync(Guid.NewGuid());

        outcome.Should().Be(OpenOutcome.Failed);
        session.LoadState.Should().Be(FormLoadState.Error);
        session.LoadError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TestOpenAsync_When_CalledTwiceForSameForm_Should_NotRefetch()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, "Survey") };
        await using var session = new FormEditorSession(forms);

        await session.OpenAsync(id);
        var second = await session.OpenAsync(id);

        second.Kind.Should().Be(OpenOutcomeKind.Reopened);
        forms.GetByIdCalls.Should().Be(1);
    }

    [Fact]
    public async Task TestOpenAsync_When_TitledFormLoads_Should_SeedSaveStateAsSaved()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, "Survey") };
        await using var session = new FormEditorSession(forms);

        await session.OpenAsync(id);

        session.LoadState.Should().Be(FormLoadState.Loaded);
        session.SaveState.Should().Be(SaveState.Saved);
    }

    [Fact]
    public async Task TestOpenAsync_When_UntitledFormLoads_Should_SeedSaveStateAsIdle()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, string.Empty) };
        await using var session = new FormEditorSession(forms);

        await session.OpenAsync(id);

        session.SaveState.Should().Be(SaveState.Idle);
    }

    [Fact]
    public async Task TestPublishAsync_When_TitleBlank_Should_ReturnMissingTitle()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, string.Empty) };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.MissingTitle);
    }

    [Fact]
    public async Task TestPublishAsync_When_AlreadyPublished_Should_ReturnAlreadyPublished()
    {
        var id = Guid.NewGuid();
        var published = FormWith(id, "Survey");
        published.Status = FormVersionStatus.Published;
        var forms = new FakeFormsService { OnGetById = _ => published };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.AlreadyPublished);
    }

    [Fact]
    public async Task TestPublishAsync_When_GateReturnsIssues_Should_ReturnNeedsFixesWithRows()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, "Survey") };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        var annotated = FormEditMapper.ToDto(FormWith(id, "Survey"));
        annotated.Issues.Add(new ValidationIssueDto { Message = "Title is required", Field = ValidationField.Title });
        forms.PublishResult = new PublishResultDto
        {
            Published = false,
            Form = annotated,
            VersionNumber = 1,
            IsFirstPublish = true,
        };

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.NeedsFixes);
        outcome.Result.Should().BeSameAs(forms.PublishResult);
        outcome.Rows.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestPublishAsync_When_GatePasses_Should_ReturnPublishedAndMarkSaved()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService { OnGetById = _ => FormWith(id, "Survey") };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        forms.PublishResult = new PublishResultDto
        {
            Published = true,
            Form = FormEditMapper.ToDto(FormWith(id, "Survey")),
            VersionNumber = 3,
            IsFirstPublish = true,
            Revision = 1,
            Status = FormVersionStatusDto.Published,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.Published);
        session.SaveState.Should().Be(SaveState.Saved);
        session.Form.Status.Should().Be(FormVersionStatus.Published);
        session.Form.VersionNumber.Should().Be(3);
    }

    [Fact]
    public async Task TestPublishAsync_When_RequestFails_Should_ReturnRequestFailedAndClearPublishing()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            PublishError = new HttpRequestException("boom"),
        };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.RequestFailed);
        session.PublishError.Should().NotBeNullOrWhiteSpace();
        session.Publishing.Should().BeFalse();
    }

    [Fact]
    public async Task TestPublishAsync_When_FlushCannotCatchUp_Should_SetPublishNoticeWithoutCallingHub()
    {
        var id = Guid.NewGuid();
        FormEditorSession? session = null;
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveResult = StateDto(version: 1),
            // Every save is immediately followed by a fresh edit, so the flush can never catch up
            // — mirrors AutosaveControllerTests.TestFlushAsync_When_EditsOutrunEveryRetry_Should_ReportIncomplete.
            OnSave = () => session!.NotifyEdited(),
        };
        await using var s = new FormEditorSession(forms);
        session = s;
        await session.OpenAsync(id);

        session.Form.Title = "Survey v2";
        session.NotifyEdited();

        var outcome = await session.PublishAsync();

        outcome.Kind.Should().Be(PublishOutcomeKind.Incomplete);
        session.PublishNotice.Should().Contain("haven't reached the server yet");
        forms.PublishCalls.Should().Be(0);
    }

    [Fact]
    public async Task TestNotifyEdited_When_PublishNoticeWasSet_Should_ClearIt()
    {
        var id = Guid.NewGuid();
        FormEditorSession? session = null;
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveResult = StateDto(version: 1),
            OnSave = () => session!.NotifyEdited(),
        };
        await using var s = new FormEditorSession(forms);
        session = s;
        await session.OpenAsync(id);
        session.Form.Title = "Survey v2";
        session.NotifyEdited();
        await session.PublishAsync();
        session.PublishNotice.Should().NotBeNull();

        session.NotifyEdited();

        session.PublishNotice.Should().BeNull();
    }

    [Fact]
    public async Task TestAutosaveFlush_When_ServerForksVersion_Should_ReconcileOnce()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveResult = StateDto(version: 2),
        };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        session.Form.Title = "Survey v2";
        session.NotifyEdited();
        await session.FlushAsync();

        forms.SaveCalls.Should().Be(1);
        forms.ReconcileForkCalls.Should().Be(1);
    }

    [Fact]
    public async Task TestAutosaveFlush_Should_SeedLastSavedAtFromServerTimestamp()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveResult = new FormVersionStateDto
            {
                VersionNumber = 1,
                Revision = 1,
                Status = FormVersionStatusDto.Draft,
                LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
        };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        session.Form.Title = "Survey edited";
        session.NotifyEdited();
        await session.FlushAsync();

        session.LastSavedAt.Should().Be(forms.SaveResult.LastModifiedAt);
    }

    [Fact]
    public async Task TestAutosaveFlush_When_VersionUnchanged_Should_NotReconcile()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveResult = StateDto(version: 1),
        };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        session.Form.Title = "Survey edited";
        session.NotifyEdited();
        await session.FlushAsync();

        forms.SaveCalls.Should().Be(1);
        forms.ReconcileForkCalls.Should().Be(0);
    }

    [Fact]
    public async Task TestAutosaveFlush_When_SaveConflicts_Should_SetConflictState()
    {
        var id = Guid.NewGuid();
        var forms = new FakeFormsService
        {
            OnGetById = _ => FormWith(id, "Survey"),
            SaveError = new FormRevisionConflictException(id),
        };
        await using var session = new FormEditorSession(forms);
        await session.OpenAsync(id);

        session.Form.Title = "Survey edited elsewhere";
        session.NotifyEdited();
        var result = await session.FlushAsync();

        result.Should().Be(AutosaveFlushResult.Failed);
        session.SaveState.Should().Be(SaveState.Conflict);
    }

    private static FormEditModel FormWith(Guid id, string title)
    {
        var form = new FormEditModel { FormId = id, Title = title, VersionNumber = 1 };
        form.Pages.Add(new FormPageEditModel { Id = Guid.NewGuid(), Title = "Page 1", Order = 0 });
        return form;
    }

    private static FormVersionStateDto StateDto(int version) => new()
    {
        VersionNumber = version,
        Revision = 1,
        Status = FormVersionStatusDto.Draft,
        LastModifiedAt = DateTimeOffset.UtcNow,
    };

    private sealed class FakeFormsService : IFormsService
    {
        public Guid NewFormId { get; init; } = Guid.NewGuid();

        public Func<Guid, FormEditModel?>? OnGetById { get; init; }

        public Action? OnSave { get; init; }

        public Exception? GetByIdError { get; init; }

        public Exception? SaveError { get; init; }

        public Exception? PublishError { get; init; }

        public int GetByIdCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public int PublishCalls { get; private set; }

        public int ReconcileForkCalls { get; private set; }

        public FormVersionStateDto SaveResult { get; init; } = new()
        {
            VersionNumber = 1,
            Revision = 1,
            Status = FormVersionStatusDto.Draft,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

        public PublishResultDto? PublishResult { get; set; }

        public IReadOnlyDictionary<Guid, Guid> ForkRemap { get; init; } = new Dictionary<Guid, Guid>();

        public Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(NewFormId);

        public Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<FormSummary>());

        public Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            return GetByIdError is not null
                ? Task.FromException<FormEditModel?>(GetByIdError)
                : Task.FromResult(OnGetById?.Invoke(formId));
        }

        public Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            OnSave?.Invoke();
            return SaveError is not null
                ? Task.FromException<FormVersionStateDto>(SaveError)
                : Task.FromResult(SaveResult);
        }

        public Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            return PublishError is not null
                ? Task.FromException<PublishResultDto>(PublishError)
                : Task.FromResult(PublishResult ?? throw new InvalidOperationException("PublishResult was not set."));
        }

        public Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(NewFormId);

        public Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(
            FormEditModel local, CancellationToken cancellationToken = default)
        {
            ReconcileForkCalls++;
            return Task.FromResult(ForkRemap);
        }
    }
}
