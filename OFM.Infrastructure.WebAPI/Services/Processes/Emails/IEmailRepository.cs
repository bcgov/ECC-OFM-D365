using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IEmailRepository
{
    Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync();
    Task<JsonObject> CreateAndUpdateEmail(string subject, string emailDescription, List<Guid> toRecipient, Guid? senderId, string communicationType, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, Int16 processId);
    Task<ProcessData> GetTemplateDataAsync(int templateNumber);
}

public class EmailRepository(ID365AppUserService appUserService, ID365WebApiService service, ID365DataService dataService, ILoggerFactory loggerFactory) : IEmailRepository
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly ID365DataService _dataService = dataService;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;
    private int _templateNumber;

    #region Pre-Defined Queries

    private string CommunicationTypeRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = """
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_communication_type">
                                <attribute name="ofm_communication_typeid" />
                                <attribute name="ofm_communication_type_number" />
                                <attribute name="statecode" />
                                <filter>
                                  <condition attribute="statecode" operator="eq" value="0" />
                                </filter>
                              </entity>
                            </fetch>
                """;

            var requestUri = $"""
                              ofm_communication_types?fetchXml={WebUtility.UrlEncode(fetchXml)}
                              """;

            return requestUri;
        }
    }

    private string TemplatetoRetrieveUri
    {
        get
        {          
            var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="template">
                    <attribute name="title" />
                    <attribute name="subjectsafehtml" />
                    <attribute name="templatetypecode" />
                    <attribute name="safehtml" />
                    <attribute name="subjectsafehtml" />        
                    <attribute name="languagecode" />
                    <attribute name="templateid" />
                    <attribute name="description" />
                    <attribute name="body" />
                    <order attribute="title" descending="false" />
                    <filter type="or">
                      <condition attribute="ccof_templateid" operator="eq"  uitype="template" value="{_templateNumber}" />
                          </filter>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                            templates?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    #endregion

    public async Task<ProcessData> GetTemplateDataAsync(int templateNumber)
    {
        _templateNumber = templateNumber;
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetTemplateToSendEmail");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TemplatetoRetrieveUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Emmail Template to update with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No template found with query {requestUri}", TemplatetoRetrieveUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync()
    {
        var localdata = await _dataService.FetchDataAsync(CommunicationTypeRequestUri, "CommunicationTypes");
        var deserializedData = localdata.Data.Deserialize<List<D365CommunicationType>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!);
    }


    #region Create and Update Email

    public async Task<JsonObject> CreateAndUpdateEmail(string subject, string emailDescription, List<Guid> toRecipient, Guid? senderId, string communicationType, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, Int16 processId)
    {
        toRecipient.ForEach(async recipient => 
        {  
            var requestBody = new JsonObject(){
                            {"subject",subject },
                            {"description",emailDescription },
                            {"email_activity_parties", new JsonArray(){
                                new JsonObject
                                {
                                    { "partyid_systemuser@odata.bind", $"/systemusers({senderId})"},
                                    { "participationtypemask", 1 } //From Email
                                },
                                new JsonObject
                                {
                                    { "partyid_contact@odata.bind", $"/contacts({recipient})" },
                                    { "participationtypemask",   2 } //To Email                             
                                }
                            }},
                            { "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({communicationType})"}
                        };

            var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, "emails", requestBody.ToString());

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create the record with the server error {responseBody}", responseBody.CleanLog());

                return;
            }

            var newEmail = await response.Content.ReadFromJsonAsync<JsonObject>();
            var newEmailId = newEmail?["activityid"];

            var emailStatement = $"emails({newEmailId})";

            var payload = new JsonObject {
                        { "ofm_sent_on", DateTime.UtcNow },
                        { "statuscode", (int) Email_StatusCode.Completed },
                        { "statecode", (int) email_statecode.Completed }};

            var requestBody1 = JsonSerializer.Serialize(payload);

            var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, emailStatement, requestBody1);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var responseBody = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());

                return;
            }
        });

        return ProcessResult.Completed(processId).SimpleProcessResult;
    }

    #endregion
}