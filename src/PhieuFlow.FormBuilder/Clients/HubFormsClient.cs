using System.Net;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Clients;

public class HubFormsClient(HttpClient httpClient) : IHubFormsClient
{
    private const int BatchSize = 100;

    public async Task<Guid> CreateFormAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/forms", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<FormCreatedDto>(cancellationToken)
            ?? throw new InvalidOperationException("Create-form response body was empty.");
        return created.Id;
    }

    public async Task<FormDto?> GetFormByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/forms/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FormDto>(cancellationToken);
    }

    public async Task<FormVersionStateDto> SaveFormAsync(FormDto dto, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/forms/{dto.Id}", dto, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new FormRevisionConflictException(dto.Id);
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FormVersionStateDto>(cancellationToken))
            ?? throw new InvalidOperationException("Save response body was empty.");
    }

    public async Task<PublishResultDto> PublishFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"/forms/{formId}/publish", null, cancellationToken);

        // A validation failure is a meaningful 422 body (the annotated tree), not an error.
        if (response.StatusCode is not HttpStatusCode.UnprocessableEntity)
        {
            response.EnsureSuccessStatusCode();
        }

        return (await response.Content.ReadFromJsonAsync<PublishResultDto>(cancellationToken))
            ?? throw new InvalidOperationException("Publish response body was empty.");
    }

    public async Task<List<FormListItemDto>> GetAllFormsAsync(CancellationToken cancellationToken = default)
    {
        var forms = new List<FormListItemDto>();
        Guid? startId = null;

        while (true)
        {
            var url = startId is null
                ? $"/forms?take={BatchSize}"
                : $"/forms?take={BatchSize}&startId={startId}";

            var response = await httpClient.GetFromJsonAsync<FormBatchResponse>(url, cancellationToken);
            if (response is null)
            {
                break;
            }

            forms.AddRange(response.Items);

            if (response.NextStartId is null)
            {
                break;
            }

            startId = response.NextStartId;
        }

        return forms;
    }

    public async Task DeleteFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/forms/{formId}", cancellationToken);

        // The row is gone either way; a 404 just means someone else got there first.
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateFormAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/forms/{sourceId}/duplicate", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Form {sourceId} no longer exists.");
        }

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<FormCreatedDto>(cancellationToken)
            ?? throw new InvalidOperationException("Duplicate-form response body was empty.");
        return created.Id;
    }
}
