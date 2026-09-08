using AwesomeAssertions;
using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Reconciliation;
using Xunit;
using static PhieuFlow.Tests.Unit.EntityTreeBuilder;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// <see cref="FormVersionReconciler"/> in isolation (ADR 0007): the fork-on-publish-edit
/// decision and the incoming-vs-current tree diff, with no database. The EF-integration half
/// (that these graph edits flush as the right INSERT / cascade DELETE / UPDATE) stays in
/// <c>PhieuFlow.Tests.Integration.FormReconcileTests</c>.
/// </summary>
public class FormVersionReconcilerTests
{
    private readonly FormVersionReconciler _reconciler = new(new FormTreeCloner());
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    // ---- draft: reconcile in place ----

    [Fact]
    public void TestReconcile_When_VersionIsDraft_Should_BumpRevisionAndKeepVersionNumber()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, null, TextArea("Q1")));
        current.Revision = 4;
        var incoming = current.DeepCopy();

        var result = _reconciler.Reconcile(current, incoming, Now);

        result.IsFork.Should().BeFalse();
        result.Version.Should().BeSameAs(current);
        current.Revision.Should().Be(5);
        current.VersionNumber.Should().Be(1);
    }

    [Fact]
    public void TestReconcile_When_VersionIsDraft_Should_CopyTitleDescriptionAndLastModified()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, null, TextArea("Q1")));
        current.Title = "Old";
        current.Description = "old description";
        current.LastModifiedBy = "alice";
        var incoming = current.DeepCopy();
        incoming.Title = "New";
        incoming.Description = "new description";
        incoming.LastModifiedBy = "bob";

        _reconciler.Reconcile(current, incoming, Now);

        current.Title.Should().Be("New");
        current.Description.Should().Be("new description");
        current.LastModifiedBy.Should().Be("bob");
        current.LastModifiedAt.Should().Be(Now);
    }

    [Fact]
    public void TestReconcile_When_PagesAdded_Should_AppendThemAndParentThemToTheVersion()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, "P1", TextArea("Q1")));
        var incoming = current.DeepCopy();
        incoming.Pages.Add(Page(1, "P2", TextArea("Q2")));

        _reconciler.Reconcile(current, incoming, Now);

        current.Pages.Select(p => p.Title).Should().Equal("P1", "P2");
        current.Pages.Single(p => p.Title == "P2").FormVersionId.Should().Be(current.Id);
    }

    [Fact]
    public void TestReconcile_When_APageIsRemoved_Should_DropItAndKeepSiblings()
    {
        var current = Version(
            FormVersionStatus.Draft,
            Page(0, "P1", TextArea("Q1")),
            Page(1, "P2", TextArea("Q2")));
        var incoming = current.DeepCopy();
        var doomed = incoming.Pages.First(p => p.Title == "P1");
        incoming.Pages.Remove(doomed);

        _reconciler.Reconcile(current, incoming, Now);

        current.Pages.Select(p => p.Title).Should().Equal("P2");
    }

    [Fact]
    public void TestReconcile_When_PagesReordered_Should_PersistNewOrder()
    {
        var current = Version(
            FormVersionStatus.Draft,
            Page(0, "P1", TextArea("Q1")),
            Page(1, "P2", TextArea("Q2")));
        var incoming = current.DeepCopy();
        incoming.Pages.Single(p => p.Title == "P1").Order = 1;
        incoming.Pages.Single(p => p.Title == "P2").Order = 0;

        _reconciler.Reconcile(current, incoming, Now);

        current.Pages.Single(p => p.Title == "P1").Order.Should().Be(1);
        current.Pages.Single(p => p.Title == "P2").Order.Should().Be(0);
    }

    [Fact]
    public void TestReconcile_When_QuestionsAdded_Should_AppendThemAndParentThemToThePage()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, null, TextArea("Q1")));
        var incoming = current.DeepCopy();
        incoming.Pages.First().Questions.Add(TextArea("Q2", order: 1));

        _reconciler.Reconcile(current, incoming, Now);

        var page = current.Pages.First();
        page.Questions.OfType<TextAreaQuestion>().Select(q => q.Text).Should().Equal("Q1", "Q2");
        page.Questions.Single(q => q.Text == "Q2").FormPageId.Should().Be(page.Id);
    }

    [Fact]
    public void TestReconcile_When_AQuestionIsRemoved_Should_DropItAndKeepSiblings()
    {
        var current = Version(
            FormVersionStatus.Draft,
            Page(0, null, TextArea("Q1"), Number("Q2"), Checkbox("Q3", "label")));
        var incoming = current.DeepCopy();
        var doomed = incoming.Pages.First().Questions.OfType<NumberQuestion>().Single();
        incoming.Pages.First().Questions.Remove(doomed);

        _reconciler.Reconcile(current, incoming, Now);

        var questions = current.Pages.First().Questions;
        questions.Select(q => q.Text).Should().BeEquivalentTo("Q1", "Q3");
        questions.OfType<NumberQuestion>().Should().BeEmpty();
    }

    [Fact]
    public void TestReconcile_When_QuestionsReordered_Should_PersistNewOrder()
    {
        var current = Version(
            FormVersionStatus.Draft,
            Page(0, null, TextArea("Q1"), TextArea("Q2"), TextArea("Q3")));
        var incoming = current.DeepCopy();
        incoming.Pages.First().Questions.Single(q => q.Text == "Q1").Order = 2;
        incoming.Pages.First().Questions.Single(q => q.Text == "Q2").Order = 1;
        incoming.Pages.First().Questions.Single(q => q.Text == "Q3").Order = 0;

        _reconciler.Reconcile(current, incoming, Now);

        var questions = current.Pages.First().Questions;
        questions.Single(q => q.Text == "Q1").Order.Should().Be(2);
        questions.Single(q => q.Text == "Q3").Order.Should().Be(0);
    }

    [Fact]
    public void TestReconcile_When_OptionsAddedRemovedReorderedAndRelabelled_Should_MatchIncoming()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, null, DropDown("Pick one", "A", "B", "C")));
        var incoming = current.DeepCopy();
        var choice = incoming.Pages.First().Questions.OfType<ChoiceQuestion>().Single();
        choice.Options.Remove(choice.Options.Single(o => o.Label == "B"));
        var a = choice.Options.Single(o => o.Label == "A");
        a.Label = "A-edited";
        a.Order = 1;
        choice.Options.Single(o => o.Label == "C").Order = 0;
        choice.Options.Add(new QuestionOption { Id = Guid.NewGuid(), Label = "D", Order = 2 });

        _reconciler.Reconcile(current, incoming, Now);

        var tracked = current.Pages.First().Questions.OfType<ChoiceQuestion>().Single();
        tracked.Options.OrderBy(o => o.Order).Select(o => o.Label).Should().Equal("C", "A-edited", "D");
    }

    [Fact]
    public void TestReconcile_When_TypedFieldsEdited_Should_ApplyPerSubtypeUpdates()
    {
        var current = Version(
            FormVersionStatus.Draft,
            Page(0, null,
                TextArea("text"),
                Checkbox("check", "old label"),
                Number("number"),
                Calendar("calendar"),
                CheckBoxGroup("group", null, null, "x", "y")));
        var incoming = current.DeepCopy();
        var page = incoming.Pages.First();

        var textArea = page.Questions.OfType<TextAreaQuestion>().Single();
        textArea.Text = "edited text";
        textArea.IsRequired = true;
        textArea.MinLength = 1;
        textArea.MaxLength = 9;
        page.Questions.OfType<CheckboxQuestion>().Single().Label = "new label";
        var number = page.Questions.OfType<NumberQuestion>().Single();
        number.Min = 2m;
        number.Max = 8m;
        var calendar = page.Questions.OfType<CalendarQuestion>().Single();
        calendar.MinDate = new DateOnly(2026, 1, 1);
        calendar.MaxDate = new DateOnly(2026, 12, 31);
        var group = page.Questions.OfType<CheckBoxGroupQuestion>().Single();
        group.MinSelections = 1;
        group.MaxSelections = 2;

        _reconciler.Reconcile(current, incoming, Now);

        var tracked = current.Pages.First().Questions;
        var trackedText = tracked.OfType<TextAreaQuestion>().Single();
        trackedText.Text.Should().Be("edited text");
        trackedText.IsRequired.Should().BeTrue();
        trackedText.MinLength.Should().Be(1);
        trackedText.MaxLength.Should().Be(9);
        tracked.OfType<CheckboxQuestion>().Single().Label.Should().Be("new label");
        tracked.OfType<NumberQuestion>().Single().Should().BeEquivalentTo(new { Min = 2m, Max = 8m });
        tracked.OfType<CalendarQuestion>().Single().Should()
            .BeEquivalentTo(new { MinDate = new DateOnly(2026, 1, 1), MaxDate = new DateOnly(2026, 12, 31) });
        tracked.OfType<CheckBoxGroupQuestion>().Single().Should()
            .BeEquivalentTo(new { MinSelections = 1, MaxSelections = 2 });
    }

    [Fact]
    public void TestReconcile_When_AQuestionChangesType_Should_ThrowInvalidOperationException()
    {
        var current = Version(FormVersionStatus.Draft, Page(0, null, TextArea("Q1")));
        var incoming = current.DeepCopy();
        var page = incoming.Pages.First();
        var original = page.Questions.First();
        page.Questions.Clear();
        var swapped = Number("Q1", order: original.Order);
        swapped.Id = original.Id;
        swapped.FormPageId = original.FormPageId;
        page.Questions.Add(swapped);

        var act = () => _reconciler.Reconcile(current, incoming, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*changed type*");
    }

    // ---- published: fork a new draft ----

    [Fact]
    public void TestReconcile_When_VersionIsPublished_Should_ReturnAForkedNewVersion()
    {
        var current = Version(FormVersionStatus.Published, Page(0, null, TextArea("Q1")));
        var incoming = current.DeepCopy();

        var result = _reconciler.Reconcile(current, incoming, Now);

        result.IsFork.Should().BeTrue();
        result.Version.Should().NotBeSameAs(current);
        result.Version.Status.Should().Be(FormVersionStatus.Draft);
        result.Version.FormId.Should().Be(current.FormId);
        result.Version.CreatedAt.Should().Be(Now);
        result.Version.LastModifiedAt.Should().Be(Now);
    }

    [Fact]
    public void TestReconcile_When_VersionIsPublished_Should_SetNextVersionNumberAndResetRevisionToOne()
    {
        var current = Version(FormVersionStatus.Published, Page(0, null, TextArea("Q1")));
        current.VersionNumber = 3;
        current.Revision = 5;
        var incoming = current.DeepCopy();

        var result = _reconciler.Reconcile(current, incoming, Now);

        result.Version.VersionNumber.Should().Be(4);
        result.Version.Revision.Should().Be(1);
    }

    [Fact]
    public void TestReconcile_When_VersionIsPublished_Should_GiveEveryNodeAFreshId()
    {
        var current = Version(
            FormVersionStatus.Published,
            Page(0, null, DropDown("Pick", "A", "B"), TextArea("Q2")));
        var incoming = current.DeepCopy();

        var result = _reconciler.Reconcile(current, incoming, Now);

        NodeIds(current).Intersect(NodeIds(result.Version)).Should().BeEmpty();
    }

    [Fact]
    public void TestReconcile_When_VersionIsPublished_Should_LeaveTheCurrentVersionUntouched()
    {
        var current = Version(FormVersionStatus.Published, Page(0, "P1", TextArea("Q1")));
        current.Title = "Published title";
        var originalIds = NodeIds(current).ToList();
        var incoming = current.DeepCopy();
        incoming.Title = "Edited title";
        incoming.Pages.First().Questions.Add(TextArea("Q2", order: 1));

        _reconciler.Reconcile(current, incoming, Now);

        current.Status.Should().Be(FormVersionStatus.Published);
        current.Title.Should().Be("Published title");
        current.Revision.Should().Be(1);
        current.Pages.Should().ContainSingle();
        current.Pages.First().Questions.Should().ContainSingle();
        NodeIds(current).Should().Equal(originalIds);
    }

    [Fact]
    public void TestReconcile_When_VersionIsPublished_Should_CarryIncomingContentIntoTheFork()
    {
        var current = Version(FormVersionStatus.Published, Page(0, null, TextArea("Q1")));
        var incoming = current.DeepCopy();
        incoming.Title = "Forked title";
        incoming.Pages.First().Questions.OfType<TextAreaQuestion>().Single().Text = "Q1 reworded";

        var result = _reconciler.Reconcile(current, incoming, Now);

        result.Version.Title.Should().Be("Forked title");
        result.Version.Pages.Single().Questions.Single().Text.Should().Be("Q1 reworded");
    }

    [Fact]
    public void TestReconcile_When_ForkContentHasUnknownQuestionType_Should_ThrowNotSupportedException()
    {
        var current = Version(FormVersionStatus.Published, Page(0, null, TextArea("Q1")));
        var incoming = Version(
            FormVersionStatus.Published,
            Page(0, null, new MysteryQuestion { Id = Guid.NewGuid(), FormPageId = Guid.NewGuid(), Text = "?" }));

        var act = () => _reconciler.Reconcile(current, incoming, Now);

        act.Should().Throw<NotSupportedException>();
    }

    private static IEnumerable<Guid> NodeIds(FormVersion version) =>
        new[] { version.Id }
            .Concat(version.Pages.Select(p => p.Id))
            .Concat(version.Pages.SelectMany(p => p.Questions).Select(q => q.Id))
            .Concat(version.Pages
                .SelectMany(p => p.Questions)
                .OfType<ChoiceQuestion>()
                .SelectMany(q => q.Options)
                .Select(o => o.Id));

    private sealed class MysteryQuestion : Question
    {
    }
}
