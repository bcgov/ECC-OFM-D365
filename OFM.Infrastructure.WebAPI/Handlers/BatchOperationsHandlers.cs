using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class BatchOperationsHandlers
{
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> BatchOperationsAsync(
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
                new UpdateRequest(new EntityReference("tasks",new Guid("00000000-0000-0000-0000-000000000000")),new JsonObject(){
                    {"subject","Task 1 in batch OFM (Updated3)" }
                }),
                new UpdateRequest(new EntityReference("tasks",new Guid("00000000-0000-0000-0000-000000000000")),new JsonObject(){
                    {"subject","Task 2 in batch OFM (Updated3).BAD" }
                }),
                 new UpdateRequest(new EntityReference("tasks",new Guid("00000000-0000-0000-0000-000000000000")),new JsonObject(){
                    {"subject","Task 3 in batch OFM (Updated3)" }
                })
            };

            var batchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZPortalAppUser, requests, callerObjectId);

            logger.LogDebug(CustomLogEvents.Batch, "Batch operation completed with the result {result}", JsonValue.Create<BatchResult>(batchResult));

            return TypedResults.Ok(batchResult.SimpleBatchResult);
        }
    }
}