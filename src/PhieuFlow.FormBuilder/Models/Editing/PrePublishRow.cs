namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>One row of the pre-publish dialog's "problems block publishing" list, with where its jump link goes.</summary>
public sealed record PrePublishRow(string Number, string Message, string JumpLabel, JumpTarget Target)
{
    /// <summary>Flattens the populated <c>Issues</c> tree into dialog rows, in reading order.</summary>
    public static IReadOnlyList<PrePublishRow> From(FormEditModel form)
    {
        var rows = new List<PrePublishRow>();

        foreach (var issue in form.Issues)
        {
            rows.Add(new PrePublishRow(string.Empty, issue.Message, "Go to title", new JumpTarget(null, null)));
        }

        for (var p = 0; p < form.Pages.Count; p++)
        {
            var page = form.Pages[p];

            foreach (var issue in page.Issues)
            {
                rows.Add(new PrePublishRow($"P{p + 1}", issue.Message, "Go to page", new JumpTarget(page.Id, null)));
            }

            for (var q = 0; q < page.Questions.Count; q++)
            {
                var question = page.Questions[q];
                var target = new JumpTarget(page.Id, question.Id);
                var number = (q + 1).ToString();

                foreach (var issue in question.Issues)
                {
                    rows.Add(new PrePublishRow(number, issue.Message, "Go to question", target));
                }

                if (question is ChoiceQuestionEditModel choice)
                {
                    foreach (var option in choice.Options)
                    {
                        foreach (var issue in option.Issues)
                        {
                            rows.Add(new PrePublishRow(number, issue.Message, "Go to question", target));
                        }
                    }
                }
            }
        }

        return rows;
    }
}
