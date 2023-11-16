using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class BatchOperationsHandlers
{
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<string>>> BatchOperationsAsync(
        HttpContext context,
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILoggerFactory loggerFactory,
        [FromBody] dynamic jsonBody,
        Guid? callerObjectId)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Batch);
        using (logger.BeginScope("ScopeBatch:POST"))
        {
            if (jsonBody is null) return TypedResults.BadRequest("Invalid batch query.");

            //if (context.Request?.QueryString.Value?.IndexOf('&') > 0)
            //{
            //    var filters = context.Request.QueryString.Value.Substring(context.Request.QueryString.Value.IndexOf('&') + 1);
            //    statement = $"{statement}?{filters}";
            //}

            //logger.LogDebug(CustomLogEvents.Operations, "Creating record(s) with the statement {statement}", statement);

            //List<HttpRequestMessage> createRequests = new() {
            //    new CreateRequest("tasks",new JsonObject(){
            //        {"subject","Task 1 in batch OFM" }
            //    }),
            //    new CreateRequest("tasks",new JsonObject(){
            //        {"subject","Task 2 in batch OFM" }
            //    }),
            //    new CreateRequest("tasks",new JsonObject(){
            //        {"subject","Task 3 in batch OFM" }
            //    })
            //};

            List<HttpRequestMessage> requests = new() {
                new UpdateRequest(new EntityReference("tasks",new Guid("6587b78f-1583-ee11-8179-000d3af4865d")),new JsonObject(){
                    {"subject","Task 1 in batch OFM (Updated3)" }
                }),
                new UpdateRequest(new EntityReference("tasks",new Guid("6787b78f-1583-ee11-8179-000d3af4865d")),new JsonObject(){
                    {"subject","Task 2 in batch OFM (Updated3)" }
                }),
                 new UpdateRequest(new EntityReference("tasks",new Guid("6987b78f-1583-ee11-8179-000d3af4865d")),new JsonObject(){
                    {"subject","Task 3 in batch OFM (Updated3)" }
                })
            };

            HttpResponseMessage response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requests, callerObjectId);

            if (!response.IsSuccessStatusCode)
            {
                //logger.LogError(CustomLogEvents.Operations, "Failed to Create a record with the statement {jsonBody.ToString()}", jsonBody.ToString());

                return TypedResults.Problem($"Failed to Create a record with a reason {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
            }

            var result = await response.Content.ReadAsStringAsync();

            //logger.LogInformation(CustomLogEvents.Operations, "Created record(s) successfully with the result {result}", result);

            return TypedResults.Ok(result);
        }
    }
}