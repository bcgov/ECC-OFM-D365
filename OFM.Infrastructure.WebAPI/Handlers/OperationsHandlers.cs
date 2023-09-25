using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace OFM.Infrastructure.WebAPI.Handlers;
public static class OperationsHandlers
{
    static readonly string pageSizeParam = "pageSize";
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> GetAsync(
        HttpContext context,
        IOptionsMonitor<AppSettings> appSettings,
        ID365AppUserService appUserService,
        ID365WebApiService d365WebApiService,
        ILogger<string> logger,
        string statement,
        int pageSize = 50)
    {
        if (string.IsNullOrEmpty(statement)) return TypedResults.BadRequest("Must provide a valid query.");

        if (context.Request?.QueryString.Value?.IndexOf('&') > 0)
        {
            var queryString = WebUtility.UrlDecode(context.Request?.QueryString.Value) ?? throw new FormatException("Unable to decode Url");
            var statementFormatted = queryString.Replace("?statement=", "");

            NameValueCollection qsVariables = HttpUtility.ParseQueryString(statementFormatted);

            if (qsVariables.HasKeys() && qsVariables.AllKeys.Contains(pageSizeParam))
            {
                statementFormatted = statementFormatted[..(statementFormatted.IndexOf(pageSizeParam) - 1)]; // Remove pagesize parameter
            }

            statement = statementFormatted;
        }

        int pagerTake = (pageSize > 0 && pageSize <= appSettings.CurrentValue.MaxPageSize) ? pageSize : appSettings.CurrentValue.MaxPageSize;
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, statement, formatted: true, pagerTake);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            logger.LogInformation("[Statement: {statement}]", statement);

            return TypedResults.Ok(result);
        }
        else
        {
            logger.LogError("[Error: {response.ReasonPhrase}].[Statement: {statement}]", response.ReasonPhrase, statement);
            return TypedResults.Problem($"Failed to Retrieve records: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }

    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> PostAsync(
        HttpContext context,
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILogger<string> logger,
        string statement,
        [FromBody] dynamic jsonBody)
    {
        if (context.Request?.QueryString.Value?.IndexOf('&') > 0)
        {
            var filters = context.Request.QueryString.Value.Substring(context.Request.QueryString.Value.IndexOf('&') + 1);
            statement = $"{statement}?{filters}";
        }

        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, statement, jsonBody.ToString());

        if (response.IsSuccessStatusCode)
        {
            var entityUri = response.Headers.GetValues("OData-EntityId")[0];
            string pattern = @"(\{){0,1}[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}(\}){0,1}";
            Match m = Regex.Match(entityUri, pattern, RegexOptions.IgnoreCase);
            var newRecordId = string.Empty;

            if (m.Success)
            {
                newRecordId = m.Value;
                var result = await response.Content.ReadFromJsonAsync<JsonObject>();

                logger.LogInformation("[Statement: {statement}]", statement);
                return TypedResults.Ok(result);
            }
            else
            {
                logger.LogError("Failed to Create record. Query: {statement}", statement);
                return TypedResults.Problem($"Unable to create record at this time: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }
        }
        else
        {
            logger.LogError("Failed to Create record. Query: {statement}", statement);

            return TypedResults.Problem($"Failed to Create record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }

    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> PatchAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILogger<string> logger,
        string statement,
        [FromBody] dynamic jsonBody)
    {
        HttpResponseMessage response = await d365WebApiService.SendUpdateRequestAsync(appUserService.AZPortalAppUser, statement, jsonBody.ToString());

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            logger.LogInformation("[Statement: {statement}]", statement);
            return TypedResults.Ok(result);
        }
        else
        {
            string jsonString = jsonBody.ToString();
            logger.LogError("API Failure: Failed to Update record.[Response: {response.ReasonPhrase}].[Statement: {statement}].[jsonBody: {jsonBody}]", response.ReasonPhrase, statement, jsonString);
            return TypedResults.Problem($"Failed to Update record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }

    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> DeleteAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILogger<string> logger,
        string statement = "contacts(00000000-0000-0000-0000-000000000000)")
    {
        var response = await d365WebApiService.SendDeleteRequestAsync(appUserService.AZPortalAppUser, statement);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            logger.LogInformation("[Statement: {statement}]", statement);
            return TypedResults.Ok(result);
        }
        else
        {
            logger.LogWarning("API Failure: Failed to Delete record. [Statement: {statement}]", statement);
            return TypedResults.Problem($"Failed to Delete record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
    }
}