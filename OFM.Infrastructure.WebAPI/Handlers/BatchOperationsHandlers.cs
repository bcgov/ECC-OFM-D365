using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OFM.Infrastructure.WebAPI.Extensions;
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
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> BatchOperationAsync(
        ILogger<string> logger,
        ID365AppUserService appUserService,
        ID365WebApiService d365WebApiService,
        [FromBody] dynamic jsonBody,
        Guid? callerObjectId)
    {
        throw new NotImplementedException();
    }
}