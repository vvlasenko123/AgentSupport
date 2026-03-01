using System.Net.Http.Json;
using Infrastructure.Broker.Kafka.Contracts;

namespace Infrastructure.Broker.Kafka.Rpc.Handler;

public sealed class MlComplaintClient : IMlComplaintClient
{
    private readonly HttpClient _httpClient;

    public MlComplaintClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MlComplaintResponse?> ProcessAsync(MlProcessRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request), "Запрос в ML не может быть null");
        }

        using var response = await _httpClient.PostAsJsonAsync("process", request, cancellationToken);

        if (response.IsSuccessStatusCode is false)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MlComplaintResponse>(cancellationToken: cancellationToken);
    }
}