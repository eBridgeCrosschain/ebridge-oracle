using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AElf.Client.Services;

public interface IHttpService
{
    Task<T?> GetResponseAsync<T>(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK);

    Task<T?> PostResponseAsync<T>(string url, Dictionary<string, string> parameters,
        string? version = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null);

    Task<T?> DeleteResponseAsObjectAsync<T>(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null);
}

public class HttpService : IHttpService
{
    private bool _useCamelCase { get; set; }
    private int _timeoutSeconds { get; set; }
    private HttpClient? _client { get; set; }
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpService(IHttpClientFactory httpClientFactory, int timeoutSeconds, bool useCamelCase = false)
    {
        _httpClientFactory = httpClientFactory;
        _useCamelCase = useCamelCase;
        _timeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Get request.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="version"></param>
    /// <param name="expectedStatusCode"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T?> GetResponseAsync<T>(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var response = await GetResponseAsync(url, version, expectedStatusCode);
        var stream = await response.Content.ReadAsStreamAsync();
        var jsonSerializerOptions = _useCamelCase
            ? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            : new JsonSerializerOptions();
        return await JsonSerializer.DeserializeAsync<T>(stream, jsonSerializerOptions);
    }

    /// <summary>
    /// Post request.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="parameters"></param>
    /// <param name="version"></param>
    /// <param name="expectedStatusCode"></param>
    /// <param name="authenticationHeaderValue"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T?> PostResponseAsync<T>(string url, Dictionary<string, string> parameters,
        string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null)
    {
        var response = await PostResponseAsync(url, parameters, version, true, expectedStatusCode,
            authenticationHeaderValue);
        var stream = await response.Content.ReadAsStreamAsync();
        var jsonSerializerOptions = _useCamelCase
            ? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            : new JsonSerializerOptions();
        return await JsonSerializer.DeserializeAsync<T>(stream, jsonSerializerOptions);
    }

    /// <summary>
    /// Delete request.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="version"></param>
    /// <param name="expectedStatusCode"></param>
    /// <param name="authenticationHeaderValue"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<T?> DeleteResponseAsObjectAsync<T>(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null)
    {
        var response = await DeleteResponseAsync(url, version, expectedStatusCode, authenticationHeaderValue);
        var stream = await response.Content.ReadAsStreamAsync();
        var jsonSerializerOptions = _useCamelCase
            ? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            : new JsonSerializerOptions();
        return await JsonSerializer.DeserializeAsync<T>(stream, jsonSerializerOptions);
    }

    #region GetResponse

    private async Task<HttpResponseMessage> GetResponseAsync(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;

        var client = GetHttpClient(version);
        try
        {
            var response = await client.GetAsync(url);
            if (response.StatusCode == expectedStatusCode)
                return response;
            throw new AElfClientException(response.ToString());
        }
        catch (Exception ex)
        {
            throw new AElfClientException(ex.Message);
        }
    }

    #endregion

    #region PostResponse

    private async Task<HttpResponseMessage> PostResponseAsync(string url,
        Dictionary<string, string> parameters,
        string? version = null, bool useApplicationJson = false,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;
        var client = GetHttpClient(version);

        if (authenticationHeaderValue != null)
        {
            client.DefaultRequestHeaders.Authorization = authenticationHeaderValue;
        }

        HttpContent content;
        if (useApplicationJson)
        {
            var paramsStr = JsonSerializer.Serialize(parameters);
            content = new StringContent(paramsStr, Encoding.UTF8, "application/json");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse($"application/json{version}");
        }

        else
        {
            content = new FormUrlEncodedContent(parameters);
            content.Headers.ContentType =
                MediaTypeHeaderValue.Parse($"application/x-www-form-urlencoded{version}");
        }

        try
        {
            var response = await client.PostAsync(url, content);
            if (response.StatusCode == expectedStatusCode)
                return response;
            var message = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(message);
        }
        catch (Exception ex)
        {
            throw new AElfClientException(ex.Message);
        }
    }

    #endregion

    #region DeleteResponse

    private async Task<string> DeleteResponseAsStringAsync(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null)
    {
        var response = await DeleteResponseAsync(url, version, expectedStatusCode, authenticationHeaderValue);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> DeleteResponseAsync(string url, string? version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        AuthenticationHeaderValue? authenticationHeaderValue = null)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;
        var client = GetHttpClient(version);

        if (authenticationHeaderValue != null)
        {
            client.DefaultRequestHeaders.Authorization = authenticationHeaderValue;
        }

        try
        {
            var response = await client.DeleteAsync(url);
            if (response.StatusCode == expectedStatusCode)
                return response;
            throw new AElfClientException(response.ToString());
        }
        catch (Exception ex)
        {
            throw new AElfClientException(ex.Message);
        }
    }

    #endregion

    #region private methods

    private HttpClient GetHttpClient(string? version = null)
    {
        if (_client != null) return _client;
        _client = _httpClientFactory.CreateClient();
        _client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(
            MediaTypeWithQualityHeaderValue.Parse($"application/json{version}"));
        _client.DefaultRequestHeaders.Add("Connection", "close");
        return _client;
    }

    #endregion
}