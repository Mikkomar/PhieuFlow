using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Models;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Form?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Form form, CancellationToken cancellationToken = default);
}
