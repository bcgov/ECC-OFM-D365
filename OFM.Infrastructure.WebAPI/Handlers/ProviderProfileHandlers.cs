using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class ProviderProfileHandlers
{
    public static async Task<Results<BadRequest<string>, NotFound<string>, ProblemHttpResult, Ok<JsonObject>>> GetProfileAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILogger<string> logger,
        string userName,
        string? userId)
    {
        if (string.IsNullOrEmpty(userName)) return TypedResults.BadRequest("Invalid Request");
        var timer = new Stopwatch();
        timer.Start();

        var fetchXml = $"""
                    <?xml version="1.0" encoding="utf-16"?>
                    <fetch top="1" distinct="true" no-lock="true">
                      <entity name="contact">
                        <attribute name="contactid" />
                        <attribute name="ccof_userid" />
                        <attribute name="ccof_username" />
                        <filter type="or">
                          <condition attribute="ccof_userid" operator="eq" value="{userId}" />
                          <condition attribute="ccof_username" operator="eq" value="{userName}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

        var requestUrl = $"contacts?fetchXml=" + WebUtility.UrlEncode(fetchXml);
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, requestUrl);

        if (response.IsSuccessStatusCode)
        {
            var jsonDom = await response.Content.ReadFromJsonAsync<JsonObject>();

            if (jsonDom?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0) { return TypedResults.NotFound($"User not found: {userId}"); }
            }

            timer.Stop();

            using (logger.BeginScope("ScopeProfile: {userName}", userName))
            {
                logger.LogInformation("ScopeProfile: Response Time: {timer.ElapsedMilliseconds}", timer.ElapsedMilliseconds);
            }

            return TypedResults.Ok(jsonDom);
        }
        else
        {
            timer.Stop();

            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            var traceId = "";
            if (problemDetails?.Extensions.TryGetValue("traceId", out var currentValue) == true)
                traceId = currentValue?.ToString();
            using (logger.BeginScope($"ScopeProfile: {userName}"))
            {
                logger.LogWarning("API Failure: Failed to Retrieve profile: {userName}. Response: {response}. TraceId: {traceId}. Finished in {timer.ElapsedMilliseconds} miliseconds.", userName, response, traceId, timer.ElapsedMilliseconds);
                return TypedResults.Problem($"Failed to Retrieve profile: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }
        }
    }
}