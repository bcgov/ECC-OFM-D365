using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

public class DocumentProvider : ID365DocumentProvider
{
    public string EntityNameSet { get => "ofm_documents"; }

    public Task<string> PrepareDocumentBodyAsync(JsonObject jsonData, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        // Clean up the json body to only include the required attributes for the OOTB Note Attachment
        string jsonBody = $$"""
                            {
                                "filename": {{jsonData["filename"]}},
                                "subject": {{jsonData["subject"]}},
                                "notetext": {{jsonData["notetext"]}},                                                  
                                "documentbody":{{jsonData["documentbody"]}}
                            }
                            """;

        return Task.FromResult(jsonBody);
    }

    public async Task<JsonObject> CreateDocumentAsync(FileMapping document, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        var requestBody = new JsonObject()
        {
            ["ofm_subject"] = document.ofm_subject,
            ["ofm_extension"] = document.ofm_extension,
            ["ofm_file_size"] = document.ofm_file_size,
            ["ofm_description"] = document.ofm_description,
            [$"ofm_regardingid_{(document.entity_name_set).TrimEnd('s')}@odata.bind"] = $"/{document.entity_name_set}({document.regardingid})",
        };

        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, EntityNameSet, requestBody.ToString());
       
        if (!response.IsSuccessStatusCode)
        {
            //log the error
            return await Task.FromResult<JsonObject>(new JsonObject() {});
        }

        var newDocument = response.Content.ReadFromJsonAsync<JsonObject>();

        return await newDocument;
    }
}