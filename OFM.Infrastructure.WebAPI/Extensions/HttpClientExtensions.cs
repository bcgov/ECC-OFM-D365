using OFM.Infrastructure.WebAPI.Models;
using Polly.Extensions.Http;
using Polly;
using System.Net.Http.Headers;
using System.Text;
using Polly.Timeout;
using Microsoft.Extensions.DependencyInjection;

namespace OFM.Infrastructure.WebAPI.Extensions;

public static class HttpClientExtensions
{
    public static IServiceCollection AddD365HttpClient(this IServiceCollection services, IConfiguration config)
    {
        var appSettings = config.GetRequiredSection(nameof(AppSettings)).Get<AppSettings>() ?? throw new KeyNotFoundException(nameof(AppSettings));
        var authSettings = config.GetRequiredSection(nameof(D365AuthSettings)).Get<D365AuthSettings>() ?? throw new KeyNotFoundException(nameof(D365AuthSettings));

        services.AddHttpClient(authSettings.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(authSettings.TimeOutInSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).AddPolicyHandler(GetRetryPolicy(appSettings)).AddPolicyHandler(GetTimeoutRetryPolicy(appSettings));
        
        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(AppSettings appSettings)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(httpResponseMessage => httpResponseMessage.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: appSettings.MaxRetries,
                sleepDurationProvider: (count, response, context) =>
                {
                    int seconds;
                    HttpResponseHeaders headers = response.Result.Headers;

                    // Use the value of the Retry-After header if it exists
                    // See https://docs.microsoft.com/power-apps/developer/data-platform/api-limits#retry-operations

                    if (headers.Contains("Retry-After"))
                    {
                        seconds = int.Parse(headers.GetValues("Retry-After").FirstOrDefault() ?? "0");
                    }
                    else
                    {
                        seconds = (int)Math.Pow(2, count);
                    }
                    return TimeSpan.FromSeconds(seconds);
                },
                onRetryAsync: (_, _, _, _) => { return Task.CompletedTask; }
            );
    }
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutRetryPolicy(AppSettings appSettings)
    {
       return Policy
            .HandleResult<HttpResponseMessage>(response => !response.IsSuccessStatusCode)
            .Or<HttpRequestException>() // Handles transient HTTP failures
            .WaitAndRetryAsync(
                retryCount: appSettings.MaxRetries,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff
                onRetry: (response, timeSpan, retryCount, context) =>
                {
                 
                });
        

    }
    public static async Task<string> ToRawString(this HttpRequestMessage request)
    {
        var sb = new StringBuilder();

        var line1 = $"{request.Method} {request.RequestUri} HTTP/{request.Version}";
        sb.AppendLine(line1);

        foreach (var (key, value) in request.Headers)
            foreach (var val in value)
            {
                var header = $"{key}: {val}";
                sb.AppendLine(header);
            }

        if (request.Content?.Headers != null)
        {
            foreach (var (key, value) in request.Content.Headers)
                foreach (var val in value)
                {
                    var header = $"{key}: {val}";
                    sb.AppendLine(header);
                }
        }
        sb.AppendLine();

        var body = await (request.Content?.ReadAsStringAsync() ?? Task.FromResult<string>(null));
        if (!string.IsNullOrWhiteSpace(body))
            sb.AppendLine(body);

        return sb.ToString();
    }

    public static async Task<string> ToRawString(this HttpResponseMessage response)
    {
        var sb = new StringBuilder();

        var statusCode = (int)response.StatusCode;
        var line1 = $"HTTP/{response.Version} {statusCode} {response.ReasonPhrase}";
        sb.AppendLine(line1);

        foreach (var (key, value) in response.Headers)
            foreach (var val in value)
            {
                var header = $"{key}: {val}";
                sb.AppendLine(header);
            }

        foreach (var (key, value) in response.Content.Headers)
            foreach (var val in value)
            {
                var header = $"{key}: {val}";
                sb.AppendLine(header);
            }
        sb.AppendLine();

        var body = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body))
            sb.AppendLine(body);

        return sb.ToString();
    }

    /// <summary>
    /// Converts HttpResponseMessage to derived type
    /// </summary>
    /// <typeparam name="T">The type derived from HttpResponseMessage</typeparam>
    /// <param name="response">The HttpResponseMessage</param>
    /// <returns></returns>
    public static T As<T>(this HttpResponseMessage response) where T : HttpResponseMessage
    {
        T? typedResponse = (T)Activator.CreateInstance(typeof(T));

        //Copy the properties
        typedResponse.StatusCode = response.StatusCode;
        response.Headers.ToList().ForEach(h =>
        {
            typedResponse.Headers.TryAddWithoutValidation(h.Key, h.Value);
        });
        typedResponse.Content = response.Content;
        return typedResponse;
    }

    //public static async Task<List<T>> GetAllDataFromPaginatedApi<T, T1>(this HttpClient client,
    //    string route, string fieldsToReturn, string? filter = null) where T1 : IHaveItems<T>
    //{
    //    var fullResults = new List<T>();

    //    bool needToGetMoreItems;
    //    var offset = 0;
    //    const int limit = 50;
    //    do
    //    {
    //        var queryString = $"fields={fieldsToReturn}&limit={limit}&offset={offset}{filter}";
    //        var response = await client.GetFromJsonAsync<T1>($"{route}?{queryString}");
    //        if (response?.Items.Any() ?? false)
    //        {
    //            fullResults.AddRange(response.Items);
    //        }
    //        needToGetMoreItems = (response?.Items?.Count ?? 0) == limit;
    //        offset += response?.Items?.Count ?? 0;
    //    } while (needToGetMoreItems);

    //    return fullResults;
    //}
}