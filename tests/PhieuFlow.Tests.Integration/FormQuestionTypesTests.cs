using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// A form with one question of every <see cref="QuestionDto"/> subtype, saved and read back
/// over the real Hub and SQL Server. Drives both directions of <c>QuestionMapper</c>, the
/// TPH discriminator, decimal and date column types, and the split-query include ordering.
/// </summary>
public sealed class FormQuestionTypesTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task TestSaveAsync_When_FormHasEveryQuestionType_Should_RoundTripEveryTypedField()
    {
        using var client = CreateClient();
        var id = await CreateAsync(client);

        (await client.PutAsJsonAsync($"/forms/{id}", TestForms.AllQuestionTypes(id)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");

        AssertAllTypes(reread!.Pages.Should().ContainSingle().Subject.Questions);
    }

    [Fact]
    public async Task TestGetFormPublishedById_When_FormHasEveryQuestionType_Should_RoundTripEveryTypedField()
    {
        using var client = CreateClient();
        var id = await CreateAsync(client);
        (await client.PutAsJsonAsync($"/forms/{id}", TestForms.AllQuestionTypes(id)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/forms/{id}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var published = await client.GetFromJsonAsync<PublishedFormDto>($"/forms/published/{id}");

        AssertAllTypes(published!.Pages.Should().ContainSingle().Subject.Questions);
    }

    private static void AssertAllTypes(List<QuestionDto> questions)
    {
        questions.Should().HaveCount(7);

        var text = questions.OfType<TextAreaQuestionDto>().Should().ContainSingle().Subject;
        text.Text.Should().Be("Free text");
        text.IsRequired.Should().BeTrue();
        text.MinLength.Should().Be(2);
        text.MaxLength.Should().Be(400);

        var checkbox = questions.OfType<CheckboxQuestionDto>().Should().ContainSingle().Subject;
        checkbox.Label.Should().Be("I agree");

        var dropdown = questions.OfType<DropDownQuestionDto>().Should().ContainSingle().Subject;
        dropdown.Options.Select(o => o.Label).Should().Equal("Finland", "Vietnam", "Peru");
        dropdown.Options.Select(o => o.Order).Should().Equal(0, 1, 2);

        var radio = questions.OfType<RadioButtonQuestionDto>().Should().ContainSingle().Subject;
        radio.Options.Select(o => o.Label).Should().Equal("Permanent", "Fixed term");

        var group = questions.OfType<CheckBoxGroupQuestionDto>().Should().ContainSingle().Subject;
        group.MinSelections.Should().Be(1);
        group.MaxSelections.Should().Be(3);
        group.Options.Select(o => o.Label).Should().Equal("Laptop", "Monitor", "Headset", "Phone");

        var number = questions.OfType<NumberQuestionDto>().Should().ContainSingle().Subject;
        number.Min.Should().Be(35.1234m);   // decimal(18,4) keeps four places
        number.Max.Should().Be(120.5m);

        var calendar = questions.OfType<CalendarQuestionDto>().Should().ContainSingle().Subject;
        calendar.MinDate.Should().Be(new DateOnly(2026, 1, 1));
        calendar.MaxDate.Should().Be(new DateOnly(2026, 12, 31));
    }

    private static async Task<Guid> CreateAsync(HttpClient client)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        return created!.Id;
    }
}
