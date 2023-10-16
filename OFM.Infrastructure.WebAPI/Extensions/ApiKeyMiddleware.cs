using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Extensions;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync(HttpContext context,
        IOptions<AuthenticationSettings> options)
    {
        var apiKeys = options.Value.Schemes.ApiKeyScheme.Keys;
        var apiKeyPresentInHeader = context.Request.Headers.TryGetValue(options.Value.Schemes.ApiKeyScheme.ApiKeyName ?? "", out var extractedApiKey);

        if ((apiKeyPresentInHeader && apiKeys.Any(k => k.Value == extractedApiKey))
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var isAllowAnonymous = endpoint?.Metadata.Any(x => x.GetType() == typeof(AllowAnonymousAttribute));
        if (isAllowAnonymous == true)
        {
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