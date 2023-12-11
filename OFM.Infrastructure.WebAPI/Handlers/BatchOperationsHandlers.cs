using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.Batches;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class BatchOperationsHandlers
{
    /// <summary>
    /// Batch Operation to perform multiple actions
    /// </summary>
    /// <param name="batchService"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="jsonBody"></param>
    /// <returns></returns>
    /// <remarks>
    /// Sample request:
    ///
    /// POST api/batches
    ///
    /// {
    ///    "batchTypeId":2,
    ///    "feature": "AccountManagement",
    ///    "function":"UserEdit",
    ///    "actionMode": "Update",
    ///    "scope": "Parent-Child",
    ///    "data":{
    ///        "contact":{
    ///            "ofm_first_name":"first",
    ///            "ofm_last_name": "last",
    ///            "entityNameSet":"contacts",
    ///            "actionMode":"Update"
    ///        },
    ///        "ofm_bceid_facility":[
    ///            {"ofm_bceid_facilityid":"00000000-0000-0000-0000-000000000000","ofm_portal_access":1,"entityNameSet":"ofm_bceid_facilities","actionMode":"Update"},
    ///            {"ofm_bceid_facilityid":"00000000-0000-0000-0000-000000000000","ofm_portal_access":0,"entityNameSet":"ofm_bceid_facilities","actionMode":"Update"}
    ///        ]
    ///    } 
    /// }
    /// </remarks>
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> BatchOperationsAsync(
        ID365BatchService batchService,
        ILoggerFactory loggerFactory,
        [FromBody] dynamic jsonBody)
    {
        var logger = loggerFactory.CreateLogger(LogCategory.Batch);
        using (logger.BeginScope("ScopeBatch:POST"))
        {
            if (jsonBody is null) return TypedResults.BadRequest("Invalid batch query.");

            using (JsonDocument jsonDocument = JsonDocument.Parse(jsonBody))
            {
                JsonElement root = jsonDocument.RootElement;
                JsonElement batchTypeId = root.GetProperty("batchTypeId");
                //Add validation here

                var batchResult = await batchService.ExecuteAsync(jsonDocument,Convert.ToInt16(batchTypeId));
                // Process the result and return             
            };

            return TypedResults.Ok(new JsonObject());
        }
    }
}