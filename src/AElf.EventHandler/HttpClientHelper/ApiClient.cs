using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AElf.EventHandler.HttpClientHelper;

public class ApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    
    public ApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient();
    }

    public async Task<T> GetAsync<T>(string uri)
    {
        var response = await _httpClient.GetAsync(uri)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonConvert.DeserializeObject<T>(responseContent);
        }
        catch (Exception e)
        {
            throw new HttpRequestException(e.Message);
        }
    }
}