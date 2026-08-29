using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    /// <summary>
    /// Asks the Hub to create a blank draft and returns its id. The builder then opens it by
    /// id, so a reload re-loads the same form.
    /// </summary>
    Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default);

    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the pre-publish gate against the persisted latest version. Callers flush any
    /// pending save first so the server is current.
    /// </summary>
    Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default);
}
