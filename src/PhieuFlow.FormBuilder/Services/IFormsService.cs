using PhieuFlow.FormBuilder.Models;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);
}
