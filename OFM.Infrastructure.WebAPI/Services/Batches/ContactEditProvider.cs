using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.Batches;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class ContactEditProvider : ID365BatchProvider
{
    public Int16 BatchTypeId { get => 101; }

    public Task<string> PrepareDataAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Process the data
    /// </summary>
    /// <param name="jsonDocument"></param>
    /// <param name="appUserService"></param>
    /// <param name="d365WebApiService"></param>
    /// <returns></returns>
    public async Task<JsonObject> ExecuteAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        JsonElement root = jsonDocument.RootElement;

        JsonElement feature = root.GetProperty("feature");
        JsonElement target = root.GetProperty("target");
        JsonElement data = root.GetProperty("data");

        foreach (var jsonElement in data.EnumerateObject())
        {
            if (jsonElement.Name == "" && jsonElement.Value.ValueKind != JsonValueKind.Null && jsonElement.Value.ValueKind != JsonValueKind.Undefined)
            {
                var obj = jsonElement.Value;
              
                break;
            }
        }

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

        var batchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZPortalAppUser, requests, null);

        //logger.LogDebug(CustomLogEvent.Batch, "Batch operation completed with the result {result}", JsonValue.Create<BatchResult>(batchResult));

        return await Task.FromResult<JsonObject>(new JsonObject());
    }
}