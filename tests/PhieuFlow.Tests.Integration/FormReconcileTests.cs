using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// Successive <c>PUT /forms/{id}</c> calls that mutate a draft, exercising
/// <c>FormVersionReconciler</c> against the real EF change tracker: add, remove, reorder
/// and field edits flushed to SQL. Unit tests cover its branching separately.
/// </summary>
public sealed class FormReconcileTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task TestSaveAsync_When_PagesQuestionsAndOptionsAdded_Should_PersistThemAll()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages[0].Questions.Add(new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Added Q", IsRequired = false });
        form.Pages[0].Questions.OfType<DropDownQuestionDto>().Single().Options
            .Add(new QuestionOptionDto { Id = Guid.NewGuid(), Label = "Chile", Order = 3 });
        form.Pages.Add(new FormPageDto
        {
            Id = Guid.NewGuid(),
            Title = "Added page",
            Questions = [new NumberQuestionDto { Id = Guid.NewGuid(), Text = "On page 2", IsRequired = false }],
        });
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages.Should().HaveCount(2);
        reread.Pages[0].Questions.Should().Contain(q => q.Text == "Added Q");
        reread.Pages[0].Questions.OfType<DropDownQuestionDto>().Single().Options
            .Select(o => o.Label).Should().Equal("Finland", "Vietnam", "Peru", "Chile");
        reread.Pages[1].Questions.Should().ContainSingle().Which.Should().BeOfType<NumberQuestionDto>();
    }

    [Fact]
    public async Task TestSaveAsync_When_APageIsRemoved_Should_CascadeDeleteItsQuestions()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages.Add(new FormPageDto
        {
            Id = Guid.NewGuid(),
            Title = "Doomed",
            Questions = [new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Goes away", IsRequired = false }],
        });
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        form = await GetAsync(client, id);
        form.Pages.RemoveAll(p => p.Title == "Doomed");
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages.Should().ContainSingle();
        reread.Pages[0].Questions.Should().HaveCount(7);
    }

    [Fact]
    public async Task TestSaveAsync_When_AQuestionIsRemoved_Should_DropItAndKeepSiblings()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages[0].Questions.RemoveAll(q => q is NumberQuestionDto);
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages[0].Questions.Should().HaveCount(6).And.NotContain(q => q is NumberQuestionDto);
    }

    [Fact]
    public async Task TestSaveAsync_When_AnOptionIsRemoved_Should_DropItFromTheChoice()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        var group = form.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single();
        group.Options.RemoveAll(o => o.Label == "Phone");
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single().Options
            .Select(o => o.Label).Should().Equal("Laptop", "Monitor", "Headset");
    }

    [Fact]
    public async Task TestSaveAsync_When_QuestionsReordered_Should_PersistNewOrder()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages[0].Questions.Reverse();
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages[0].Questions.Select(q => q.GetType()).Should().Equal(
            typeof(CalendarQuestionDto), typeof(NumberQuestionDto), typeof(CheckBoxGroupQuestionDto),
            typeof(RadioButtonQuestionDto), typeof(DropDownQuestionDto), typeof(CheckboxQuestionDto),
            typeof(TextAreaQuestionDto));
    }

    [Fact]
    public async Task TestSaveAsync_When_OptionsReordered_Should_PersistNewOrder()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages[0].Questions.OfType<DropDownQuestionDto>().Single().Options.Reverse();
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        var options = reread.Pages[0].Questions.OfType<DropDownQuestionDto>().Single().Options;
        options.Select(o => o.Label).Should().Equal("Peru", "Vietnam", "Finland");
        options.Select(o => o.Order).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task TestSaveAsync_When_TypedFieldsEdited_Should_PersistPerTypeUpdates()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        form.Pages[0].Questions.OfType<CheckboxQuestionDto>().Single().Label = "I really agree";
        var group = form.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single();
        group.MinSelections = 2;
        group.MaxSelections = 4;
        var number = form.Pages[0].Questions.OfType<NumberQuestionDto>().Single();
        number.Min = 1m;
        number.Max = 9.9999m;
        var calendar = form.Pages[0].Questions.OfType<CalendarQuestionDto>().Single();
        calendar.MinDate = new DateOnly(2027, 6, 1);
        calendar.MaxDate = new DateOnly(2027, 6, 30);
        (await Save(client, form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await GetAsync(client, id);
        reread.Pages[0].Questions.OfType<CheckboxQuestionDto>().Single().Label.Should().Be("I really agree");
        var rereadGroup = reread.Pages[0].Questions.OfType<CheckBoxGroupQuestionDto>().Single();
        rereadGroup.MinSelections.Should().Be(2);
        rereadGroup.MaxSelections.Should().Be(4);
        var rereadNumber = reread.Pages[0].Questions.OfType<NumberQuestionDto>().Single();
        rereadNumber.Min.Should().Be(1m);
        rereadNumber.Max.Should().Be(9.9999m);
        var rereadCalendar = reread.Pages[0].Questions.OfType<CalendarQuestionDto>().Single();
        rereadCalendar.MinDate.Should().Be(new DateOnly(2027, 6, 1));
        rereadCalendar.MaxDate.Should().Be(new DateOnly(2027, 6, 30));
    }

    [Fact]
    public async Task TestSaveAsync_When_AQuestionChangesType_Should_Return500()
    {
        using var client = CreateClient();
        var id = await CreateAllTypesAsync(client);

        var form = await GetAsync(client, id);
        var textId = form.Pages[0].Questions.OfType<TextAreaQuestionDto>().Single().Id;
        var index = form.Pages[0].Questions.FindIndex(q => q.Id == textId);
        // Same id, different runtime type.
        form.Pages[0].Questions[index] = new NumberQuestionDto { Id = textId, Text = "Now a number", IsRequired = false };

        var response = await Save(client, form);

        // TODO: a question whose type changed makes ReconcileQuestions throw
        // InvalidOperationException, which surfaces as a bare 500. It should be a 400 or 409.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var reread = await GetAsync(client, id);
        reread.Pages[0].Questions.OfType<TextAreaQuestionDto>().Should().ContainSingle("nothing was written");
    }

    private static async Task<Guid> CreateAllTypesAsync(HttpClient client)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;
        (await client.PutAsJsonAsync($"/forms/{id}", TestForms.AllQuestionTypes(id))).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<FormDto> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<FormDto>($"/forms/{id}"))!;

    private static Task<HttpResponseMessage> Save(HttpClient client, FormDto form) =>
        client.PutAsJsonAsync($"/forms/{form.Id}", form);
}
