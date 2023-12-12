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
    ///            "entityID":"00000000-0000-0000-0000-000000000000",
    ///            "emailaddress1": "test.user@cgi.com",
    ///            "ofm_portal_role": "1,2,3,4",
    ///            "actionMode":"Update"
    ///        },
    ///        "ofm_bceid_facility":[
    ///            {"entityID":"00000000-0000-0000-0000-000000000000","ofm_portal_access":true,"entityNameSet":"ofm_bceid_facilities","actionMode":"Update"},
    ///            {"entityID":"00000000-0000-0000-0000-000000000000","ofm_portal_access":false,"entityNameSet":"ofm_bceid_facilities","actionMode":"Update"}
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
            var jsonData = JsonSerializer.Serialize(jsonBody);
            using (JsonDocument jsonDocument = JsonDocument.Parse(jsonData))
            {
                JsonElement root = jsonDocument.RootElement;
                JsonElement batchTypeId = root.GetProperty("batchTypeId");
                JsonElement data = root.GetProperty("data");
                //Add validation here

                var batchResult = await batchService.ExecuteAsync(jsonDocument, batchTypeId.GetInt16());
                // Process the result and return             
            };

            return TypedResults.Ok(new JsonObject());
        }
    }
}