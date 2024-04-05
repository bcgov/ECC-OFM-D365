using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Batches;

public interface ID365BatchProvider
{
    Int16 BatchTypeId { get; } //e.g. 1

    Task<string> PrepareDataAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService);

    Task<JsonObject> ExecuteAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService);
}