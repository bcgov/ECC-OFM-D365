using OFM.Infrastructure.WebAPI.Models;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.Json;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Extensions;

namespace OFM.Infrastructure.WebAPI.Services.Documents;

sealed class ApplicationDocumentProvider : ID365DocumentProvider
{
    public string EntityNameSet => "ccof_application_facility_document";

    public async Task<string> PrepareDocumentBodyAsync(JsonObject jsonData,ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        var fetchXml = $$"""
                    <?xml version="1.0" encoding="utf-16"?>
                    <fetch>
                      <entity name="ccof_application_facility_document">
                        <attribute name="ccof_application" />
                        <attribute name="ccof_application_facility_documentid" />
                        <attribute name="ccof_name" />
                        <attribute name="ccof_facility" />
                        <filter>
                          <condition attribute="ccof_application" operator="eq" value="{{jsonData["ccof_applicationid"]}}"/>
                          <condition attribute="ccof_facility" operator="eq" value="{{jsonData["ccof_facility"]}}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

        string statement = $"ccof_application_facility_documents?fetchXml=" + WebUtility.UrlEncode(fetchXml);
        HttpResponseMessage response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, statement, true);

        var appFacilityDocsResult = await response.Content.ReadFromJsonAsync<JsonObject>();
        if (appFacilityDocsResult?.TryGetPropertyValue("value", out JsonNode? myResult) == true && myResult is not null)
        {
            JsonObject? jsonObject = myResult[0].Deserialize<JsonObject>(CommonInfo.s_readOptions!);

            string jsonBody = $$"""
                                {
                                    "filename": {{jsonData["filename"]}},
                                    "subject": {{jsonData["subject"]}},
                                    "notetext": {{jsonData["notetext"]}},                         
                                    "ccof_applicationid": {{jsonData["ccof_applicationid"]}},
                                    "ccof_facility": {{jsonData["ccof_facility"]}},
                                    "objectid_ccof_application_facility_document@odata.bind":"/ccof_application_facility_documents({{jsonObject?["ccof_application_facility_documentid"]}}")",                    
                                    "documentbody":{{jsonData["documentbody"]}}
                                }
                                """;
            return await Task.FromResult(jsonBody);
        }

        return await Task.FromResult(jsonData.ToJsonString());
    }
}