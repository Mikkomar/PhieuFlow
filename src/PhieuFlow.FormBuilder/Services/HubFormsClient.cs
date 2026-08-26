using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public class HubFormsClient(HttpClient httpClient)
{
    private const int BatchSize = 100;

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
