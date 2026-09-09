using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

// Read-back of a form's responses for the FormBuilder Responses view.
public static class FormSubmissionEndpoints
{
    public static void MapFormSubmissionEndpoints(this WebApplication app)
    {
        // Keyset-paged by submission id, same contract as GET /forms.
        app.MapGet("/forms/{id:guid}/submissions", async (
            Guid id,
            IUnitOfWork unitOfWork,
            int take = 20,
            Guid? startId = null,
            CancellationToken cancellationToken = default) =>
        {
            if (!TakeParameter.IsValid(take))
            {
                return TakeParameter.OutOfRange();
            }

            var result = await unitOfWork.Forms.GetSubmissionsBatchAsync(id, startId, take, cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(SubmissionResponseMapper.ToDto(result));
        }).RequireAuthorization("submissions:read");
    }
}
