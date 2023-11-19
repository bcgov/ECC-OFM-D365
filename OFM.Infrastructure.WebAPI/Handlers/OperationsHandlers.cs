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
using System.Web;

namespace OFM.Infrastructure.WebAPI.Handlers;
public static class OperationsHandlers
{
    static readonly string pageSizeParam = "pageSize";

    /// <summary>
    /// A generic endpoint for D365 queries 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="appSettings"></param>
    /// <param name="appUserService"></param>
    /// <param name="d365WebApiService"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="statement" example="emails?$select=subject,description,lastopenedtime"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>

    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> GetAsync(
        HttpContext context,
        IOptions<AppSettings> appSettings,
        ID365AppUserService appUserService,
        ID365WebApiService d365WebApiService,
        ILoggerFactory loggerFactory,
        string statement,
        int pageSize = 50)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Operation);
        using (logger.BeginScope("ScopeOperations:GET"))
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

            int pagerTake = (pageSize > 0 && pageSize <= appSettings.Value.MaxPageSize) ? pageSize : appSettings.Value.MaxPageSize;

            logger.LogDebug(CustomLogEvents.Operation, "Quering data with the statement {statement} and pageSize {pagerTake}", statement, pagerTake);

            var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, statement, formatted: true, pagerTake);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonObject>();
                logger.LogInformation(CustomLogEvents.Operation, "Queried data successfully with the statement {statement}", statement);

                return TypedResults.Ok(result);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogError(CustomLogEvents.Operation, "Failed to query data by the statement {statement} with a server response error {responseBody}", statement, responseBody);

                return TypedResults.Problem($"Failed to Retrieve records: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }
        }
    }

    /// <summary>
    /// A generic endpoint to create D365 records
    /// </summary>
    /// <param name="context"></param>
    /// <param name="d365WebApiService"></param>
    /// <param name="appUserService"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="statement"></param>
    /// <param name="jsonBody"></param>
    /// <returns></returns>
    [Consumes("application/json")]
    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> PostAsync(
        HttpContext context,
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILoggerFactory loggerFactory,
        string statement,
        [FromBody] dynamic jsonBody)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Operation);
        using (logger.BeginScope("ScopeOperations:POST"))
        {
            if (context.Request?.QueryString.Value?.IndexOf('&') > 0)
            {
                var filters = context.Request.QueryString.Value.Substring(context.Request.QueryString.Value.IndexOf('&') + 1);
                statement = $"{statement}?{filters}";
            }

            logger.LogDebug(CustomLogEvents.Operation, "Creating record(s) with the statement {statement}", statement);

            HttpResponseMessage response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, statement, jsonBody.ToString());

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                logger.LogError(CustomLogEvents.Operation, "Failed to Create the record with the error {error}", responseBody);

                return TypedResults.Problem($"Failed to Create the record with a reason {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            if (result != null)
            {
                result.Remove("@odata.context");
                result.Remove("@odata.etag");
            }

            logger.LogInformation(CustomLogEvents.Operation, "Created record(s) successfully with the result {result}", result);

            return TypedResults.Ok(result);
        }
    }

    /// <summary>
    /// A generic endpoint to update D365 records
    /// </summary>
    /// <param name="d365WebApiService"></param>
    /// <param name="appUserService"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="statement"></param>
    /// <param name="jsonBody"></param>
    /// <returns></returns>
    [Consumes("application/json")]
    public static async Task<Results<ProblemHttpResult, NoContent>> PatchAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILoggerFactory loggerFactory,
        string statement,
        [FromBody] dynamic jsonBody)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Operation);
        using (logger.BeginScope("ScopeOperations:PATCH"))
        {
            logger.LogDebug(CustomLogEvents.Operation, "Updating the record(s) with query {statement}", statement);

            HttpResponseMessage response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZPortalAppUser, statement, jsonBody.ToString());

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogError(CustomLogEvents.Operation, "Failed to Update the record by the statement {statement} with the server response error {error}", statement, responseBody);

                return TypedResults.Problem($"Failed to Update a record: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }

            logger.LogDebug(CustomLogEvents.Operation, "Updated the record(s) successfully with query {statement}", statement);

            return TypedResults.NoContent();
        }
    }

    /// <summary>
    /// A generic endpoint to delete a D365 record
    /// </summary>
    /// <param name="d365WebApiService"></param>
    /// <param name="appUserService"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="statement" example="emails(00000000-0000-0000-0000-000000000000)"></param>
    /// <returns></returns>
    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> DeleteAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILoggerFactory loggerFactory,
        string statement = "emails(00000000-0000-0000-0000-000000000000)")
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Operation);
        using (logger.BeginScope("ScopeOperations: DELETE"))
        {
            var response = await d365WebApiService.SendDeleteRequestAsync(appUserService.AZPortalAppUser, statement);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                logger.LogError(CustomLogEvents.Operation, "Failed to Delete the record with a server error {responseBody}", responseBody);

                return TypedResults.Problem($"Failed to Delete a record with a reason {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonObject>();
            logger.LogInformation(CustomLogEvents.Operation, "Deleted the record by {statement}]", statement);

            return TypedResults.Ok(result);
        }
    }
}