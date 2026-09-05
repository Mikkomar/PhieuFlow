using System.Runtime.CompilerServices;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormFiller.Clients;

public class HubFormsClient(HttpClient httpClient) : IHubFormsClient
{
    private const int BatchSize = 100;

    public async IAsyncEnumerable<List<PublishedFormListItemDto>> GetPublishedFormBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guid? startId = null;

        while (true)
        {
            var url = startId is null
                ? $"/forms/published?take={BatchSize}"
                : $"/forms/published?take={BatchSize}&startId={startId}";

            var response = await httpClient.GetFromJsonAsync<PublishedFormBatchResponse>(url, cancellationToken);
            if (response is null)
            {
                yield break;
            }

            yield return response.Items.ToList();

            if (response.NextStartId is null)
            {
                yield break;
            }

            startId = response.NextStartId;
        }
    }
}
