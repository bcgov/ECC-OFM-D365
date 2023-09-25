using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Handlers;

public static class SearchesHandlers
{
    public static async Task<Results<BadRequest<string>, ProblemHttpResult, Ok<JsonObject>>> DataverseSearchAsync(
        ID365WebApiService d365WebApiService,
        ID365AppUserService appUserService,
        ILogger<string> logger,
        [FromBody] dynamic? searchTerm)
    {
       throw new NotImplementedException();
    }
}