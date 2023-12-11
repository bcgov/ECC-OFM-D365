using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.Batches;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class BatchProvider : ID365BatchProvider
{
    public Int16 BatchTypeId { get => 100; }

    public async Task<string> PrepareDataAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        throw new NotImplementedException();
    }

    public async Task<JsonObject> ExecuteAsync(JsonDocument document, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        throw new NotImplementedException();

        //var requestBody = new JsonObject()
        //{
        //    ["ofm_subject"] = document.ofm_subject,
        //    ["ofm_extension"] = document.ofm_extension,
        //    ["ofm_file_size"] = document.ofm_file_size,
        //    ["ofm_description"] = document.ofm_description,
        //    [$"ofm_regardingid_{(document.entity_name_set).TrimEnd('s')}@odata.bind"] = $"/{document.entity_name_set}({document.regardingid})",
        //};

        //var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, EntityNameSet, requestBody.ToString());

        //if (!response.IsSuccessStatusCode)
        //{
        //    //log the error
        //    return await Task.FromResult<JsonObject>(new JsonObject() { });
        //}

        //var newDocument = await response.Content.ReadFromJsonAsync<JsonObject>();

        //return await Task.FromResult<JsonObject>(newDocument!);

    }

}