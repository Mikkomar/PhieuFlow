using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    /// <summary>
    /// Builds a blank in-memory form (new identity, one empty page). It is not persisted
    /// anywhere until the builder's first autosave.
    /// </summary>
    FormVersion CreateNew();

    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveAsync(Guid formId, FormVersion form, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default);
}
