using System.Net;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public class HubFormsClient(HttpClient httpClient)
{
    private const int BatchSize = 100;

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
}
