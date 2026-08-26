using PhieuFlow.Core.Entities;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// In-memory stand-in for the hub's form-management API (ADR 0001 describes that API; it
/// does not exist in this repo yet). Lets the builder round-trip between the forms list
/// and the edit route without a real backend.
/// </summary>
public class FormRepository
{
    private readonly Dictionary<Guid, Form> _forms = [];

    public Form? TryGet(Guid id) => _forms.GetValueOrDefault(id);

    public Form CreateNew()
    {
        var formId = Guid.NewGuid();
        return new Form
        {
            Id = formId,
            Title = string.Empty,
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), FormId = formId } },
        };
    }

    public void Save(Form form) => _forms[form.Id] = form;
}
