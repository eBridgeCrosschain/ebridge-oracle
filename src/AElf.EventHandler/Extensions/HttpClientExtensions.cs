using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace AElf.EventHandler
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> GetHttpResponseMessageWithRetryAsync<T>
        (
            this HttpClient httpClient,
            string url,
            ILogger<T> logger = null)
        {
            var sleepDurations = new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(7)
            };

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(httpClient.Timeout);
            var cancellationToken = cancellationTokenSource.Token;

            return await HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(sleepDurations,
                    (responseMessage, timeSpan, retryCount, context) =>
                    {
                        if (responseMessage.Exception != null)
                        {
                            string httpErrorCode = responseMessage.Result == null
                                ? string.Empty
                                : "HTTP-" + (int) responseMessage.Result.StatusCode + ", ";

                            logger?.LogWarning(
                                "{Count}. HTTP request attempt failed to {Url} with an error: {HttpErrorCode}{Message}. " +
                                "Waiting {TotalSeconds} secs for the next try...", retryCount, url, httpErrorCode,
                                responseMessage.Exception.Message, timeSpan.TotalSeconds);
                        }
                        else if (responseMessage.Result != null)
                        {
                            logger?.LogWarning(
                                "{Count}. HTTP request attempt failed to {Url} with an error: {Code}-{Message}. " +
                                "Waiting {TotalSeconds} secs for the next try...", retryCount, url,
                                (int) responseMessage.Result.StatusCode, responseMessage.Result.ReasonPhrase,
                                timeSpan.TotalSeconds);
                        }
                    })
                .ExecuteAsync(async () => await httpClient.GetAsync(url, cancellationToken));
        }
    }
}