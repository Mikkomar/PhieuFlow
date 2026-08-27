using PhieuFlow.Core.Entities;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// In-memory stand-in for the hub's form-management API (ADR 0001 describes that API; it
/// does not exist in this repo yet). Lets the builder round-trip between the forms list
/// and the edit route without a real backend.
/// </summary>
public class FormRepository
{
    private readonly Dictionary<Guid, FormVersion> _versions = [];

    public FormVersion? TryGet(Guid formId) => _versions.GetValueOrDefault(formId);

    public FormVersion CreateNew()
    {
        var formId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        return new FormVersion
        {
            Id = versionId,
            FormId = formId,
            Title = string.Empty,
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), FormVersionId = versionId } },
        };
    }

    public void Save(FormVersion version) => _versions[version.FormId] = version;
}
