using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Models;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Extensions;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger(LogCategory.API);
    }

    public async Task InvokeAsync(HttpContext context,
        IOptions<AuthenticationSettings> options)
    {
        var apiKeys = options.Value.Schemes.ApiKeyScheme.Keys;
        var apiKeyPresentInHeader = context.Request.Headers.TryGetValue(options.Value.Schemes.ApiKeyScheme.ApiKeyName ?? "", out var extractedApiKey);

        if ((apiKeyPresentInHeader && apiKeys.Any(k => k.Value == extractedApiKey))
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            string newKeyValue = extractedApiKey.ToString();
            //var emailPattern = @"(?<=[\w]{1})[\w-\._\+%]*(?=[\w]{1}@)";
            var pattern = @"(?<=[\w]{5})[\w-\._\+%]*(?=[\w]{3})";
            var maskedKey = Regex.Replace(newKeyValue ?? "", pattern, m => new string('*', m.Length));

            _logger.LogInformation(CustomLogEvent.API, "x-ofm-apikey:{maskedKey}", maskedKey);

            await _next(context);

            return;
        }

        var endpoint = context.GetEndpoint();
        var isAllowAnonymous = endpoint?.Metadata.Any(x => x.GetType() == typeof(AllowAnonymousAttribute));
        if (isAllowAnonymous == true)
        {
            _logger.LogWarning(CustomLogEvent.API, "Anonymous user detected.");
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync(options.Value.Schemes.ApiKeyScheme.ApiKeyErrorMesssage);
    }
}

public static class ApiKeyMiddlewareExtension
{
    public static IApplicationBuilder UseApiKey(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
