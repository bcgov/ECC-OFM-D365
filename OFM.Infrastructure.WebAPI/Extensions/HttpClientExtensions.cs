using OFM.Infrastructure.WebAPI.Models;
using Polly.Extensions.Http;
using Polly;
using System.Net.Http.Headers;

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
        }).AddPolicyHandler(GetRetryPolicy(appSettings));

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
}
