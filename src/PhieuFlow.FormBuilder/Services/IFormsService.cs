using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<SaveFormResultDto> SaveAsync(Guid formId, FormVersion form, CancellationToken cancellationToken = default);

    Task PublishAsync(Guid formId, CancellationToken cancellationToken = default);
}
