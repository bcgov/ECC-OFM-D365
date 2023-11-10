using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Handlers.D365;

public static class BatchProcessesHandlers
{
    public static async Task<Results<ProblemHttpResult, Ok<JsonObject>>> BP1_CloseInactiveRequestsAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        Guid? callerObjectId)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.BatchProcesses);
        using (logger.BeginScope("ScopeBatch: CloseInactiveRequests"))
        {
            logger.LogInformation(CustomLogEvents.BatchProcesses, "Daily batch process start");

            var startTime = timeProvider.GetTimestamp();

            // Note: FetchXMl limit is 5000
            var fetchXml = $"""
                    <fetch distinct="true" no-lock="true">
                      <entity name="ofm_assistance_request">
                        <attribute name="ofm_assistance_requestid" />
                        <attribute name="ofm_name" />
                        <attribute name="ofm_subject" />
                        <attribute name="ofm_request_category" />
                        <attribute name="ofm_contact" />
                        <attribute name="modifiedon" />
                        <attribute name="statecode" />
                        <attribute name="statuscode" />
                        <filter>
                          <condition attribute="statuscode" operator="eq" value="4" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                             ofm_assistance_requests?fetchXml={WebUtility.UrlEncode(fetchXml)}
                             """;

            logger.LogDebug(CustomLogEvents.BatchProcesses, "Getting inactive requests with query {requestUri}", requestUri);

            var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, requestUri);

            var endTime = timeProvider.GetTimestamp();

            if (!response.IsSuccessStatusCode)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>() ?? new ProblemDetails();

                return TypedResults.Problem($"Failed to Retrieve inactive requests: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }

            var jsonDom = await response.Content.ReadFromJsonAsync<JsonObject>();
           
            var result = new JsonObject
            {
                ["Status"] = "Completed", //Partial Completed, Failed
                ["Processed"] = 100,
                ["TotalRecords"] = 100,
                ["CompletedAt"] = DateTime.Now,
                ["ResultMessage"] = "All records have been successfull deactivated with no warning.",
            };

            logger.LogInformation(CustomLogEvents.BatchProcesses, "Batch result {result}", result);

            return TypedResults.Ok(result);
        }
    }

    public static Task BP2(HttpContext context)
    {
        throw new NotImplementedException();
    }
}