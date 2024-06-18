using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Batches;

public class ProviderReportResetProvider(ILoggerFactory loggerFactory) : ID365BatchProvider
{
    private string? _recordId;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Batch);

    public short BatchTypeId { get => 102; }

    public string RequestUri
    {
        get
        {
            // For reference only
            var fetchXml = $"""
                                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                                  <entity name="ofm_question_response">
                                    <attribute name="ofm_header" />
                                    <attribute name="ofm_multiple_line" />
                                    <attribute name="ofm_name" />
                                    <attribute name="ofm_question" />
                                    <attribute name="ofm_question_responseid" />
                                    <attribute name="ofm_response_text" />
                                    <attribute name="ofm_row_id" />
                                    <attribute name="ofm_survey_response" />
                                    <attribute name="ownerid" />
                                    <attribute name="owningbusinessunit" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="ofm_survey_response" operator="eq" value="{_recordId}" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

            var requestUri = $"""                                
                                ofm_question_responses?$select=_createdby_value,createdon,_createdonbehalfby_value,importsequencenumber,_modifiedby_value,modifiedon,_modifiedonbehalfby_value,ofm_embedded_data,_ofm_header_value,ofm_multiple_line,ofm_name,_ofm_question_value,ofm_question_responseid,ofm_response_text,ofm_row_id,_ofm_survey_response_value,overriddencreatedon,_ownerid_value,_owningbusinessunit_value,statecode,statuscode,timezoneruleversionnumber,utcconversiontimezonecode,versionnumber&$filter=(_ofm_survey_response_value eq '{_recordId}')
                                """;

            return requestUri.CleanCRLF();
        }
    }

    public async Task<JsonObject> PrepareDataAsync(JsonDocument jsonDocument, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        throw new NotImplementedException();
    }

    public async Task<JsonObject> ExecuteAsync(JsonDocument document, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        JsonElement root = document.RootElement;
        JsonElement data = root.GetProperty("data");
        List<HttpRequestMessage> requests = [];
     
        foreach (var jsonElement in data.EnumerateObject())
        {
            JsonObject jsonObject = [];
            if (jsonElement.Name != "" && jsonElement.Value.ValueKind != JsonValueKind.Null && jsonElement.Value.ValueKind != JsonValueKind.Undefined)
            {
                var obj = jsonElement.Value;
                if (jsonElement.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty jobject in obj.EnumerateObject())
                    {
                        jsonObject.Add(jobject.Name, (jobject.Value.ValueKind == JsonValueKind.Null) ? null : (JsonNode)jobject.Value.ToString());
                    }
                    _recordId = (string?)jsonObject["entityID"];

                    #region Step 01: Get Record Template data of the Active Provider Report for Cloning

                    HttpResponseMessage recordTemplateResponse = await d365WebApiService.GetRecordTemplateForClone(appUserService.AZPortalAppUser, Guid.Parse(_recordId!), ofm_survey_response.EntityLogicalName, ofm_survey_response.EntitySetName);
                    if (!recordTemplateResponse.IsSuccessStatusCode)
                    {
                        var recordTemplateResponseError = await recordTemplateResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Batch, "Failed to query the Record Template Data using InitializeFrom with the server error {recordTemplateResponseBody}", recordTemplateResponseError);

                        return await Task.FromResult(new JsonObject());
                    }

                    JsonObject? providerReportToCopy = await recordTemplateResponse.Content.ReadFromJsonAsync<JsonObject>();
                    providerReportToCopy![$"{ofm_survey_response.Fields.ofm_current_version}@odata.bind"] = $"/{ofm_survey_response.EntitySetName}({_recordId})"; // link to the Active Version
                    providerReportToCopy[ofm_survey_response.Fields.ofm_unlock] = false;

                    #endregion

                    #region Step 02: Create a copy from the Active Version of the Provider Report from the Clone data template 

                    var newReportResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZPortalAppUser, ofm_survey_response.EntitySetName, providerReportToCopy.ToString());

                    if (!newReportResponse.IsSuccessStatusCode)
                    {
                        var newReportResponseError = await newReportResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Batch, "Failed to create the record with the server error {newReportResponseBody}", newReportResponseError.CleanLog());
                        
                        return await Task.FromResult(new JsonObject());
                    }

                    JsonObject? newProviderReport = await newReportResponse.Content.ReadFromJsonAsync<JsonObject>();

                    #endregion

                    #region Step 03: Increase the Active Provider Report's version number by 1

                    int? currentVersionNumber = (Nullable<int>)newProviderReport?["ofm_version_number"];

                    JsonObject providerReportToUpdate = new()
                    {
                        {"ofm_version_number", ++currentVersionNumber }
                    };

                    string providerReportToUpdateRequest = JsonSerializer.Serialize(providerReportToUpdate);

                    HttpResponseMessage updateProviderReportResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZPortalAppUser, $"{ofm_survey_response.EntitySetName}({_recordId})", providerReportToUpdateRequest);

                    if (!updateProviderReportResponse.IsSuccessStatusCode)
                    {
                        var updateProviderReportResponseError = await updateProviderReportResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Batch, "Failed to update the record with the server error {responseBody}", updateProviderReportResponseError.CleanLog());
                        
                        return await Task.FromResult(new JsonObject());
                    }

                    #endregion

                    #region Step 04:Deactivate the new Provider Report copy

                    JsonNode? newProviderReportResponseId = newProviderReport?["ofm_survey_responseid"];

                    JsonObject providerReportToDeactivate = new()
                    {
                        { ofm_survey_response.Fields.statecode, (int)ofm_survey_response_statecode.Inactive },
                        { ofm_survey_response.Fields.statuscode, (int)ofm_survey_response_StatusCode.Inactive }
                    };

                    var deactivateProviderReportRequest = JsonSerializer.Serialize(providerReportToDeactivate);
                    var deactivatProviderReportResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZPortalAppUser, $"{ofm_survey_response.EntitySetName}({newProviderReportResponseId})", deactivateProviderReportRequest);

                    if (!deactivatProviderReportResponse.IsSuccessStatusCode)
                    {
                        var deactivatProviderReportResponseError = await deactivatProviderReportResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Batch, "Failed to deactivate the record with the server error {responseBody}", deactivatProviderReportResponseError.CleanLog());
                    }

                    #endregion

                    #region Step 05: Finally, Associate the question responses to the New copy of provider report & Deactivate them at the same time.

                    var reportQuestionResponsesResponse = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZPortalAppUser, RequestUri, isProcess: true);

                    if (!reportQuestionResponsesResponse.IsSuccessStatusCode)
                    {
                        var reportQuestionResponsesResponseError = await reportQuestionResponsesResponse.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Process, "Failed to query Question Responses with the server error {reportQuestionResponsesResponseBody}", reportQuestionResponsesResponseError.CleanLog());
                        
                        return await Task.FromResult(new JsonObject());
                    }

                    JsonObject? reportQuestionResponsesData = await reportQuestionResponsesResponse.Content.ReadFromJsonAsync<JsonObject>();

                    if (reportQuestionResponsesData?.TryGetPropertyValue("value", out var d365Data) == true)
                    {
                        if (d365Data?.AsArray().Count == 0)
                        {
                            _logger.LogInformation(CustomLogEvent.Batch, "No Question Response found with query {requestUri}", RequestUri);
                        }

                        List<ofm_question_response>? questionResponses = JsonSerializer.Deserialize<List<ofm_question_response>>(d365Data, Setup.s_writeOptionsForLogs);

                        if (questionResponses is not null && questionResponses.Count > 0)
                        {
                            List<HttpRequestMessage> questionResponseRequestList = [];

                            foreach (ofm_question_response qresponse in questionResponses)
                            {
                                var questionResponseToUpdate = new JsonObject
                                {
                                    { $"{ofm_survey_response.EntityLogicalName}@odata.bind", $"/{ofm_survey_response.EntitySetName}({newProviderReportResponseId})" },
                                    { ofm_question_response.Fields.statecode, (int)ofm_question_response_statecode.Inactive },
                                    { ofm_question_response.Fields.statuscode, (int)ofm_question_response_StatusCode.Inactive }
                                };

                                var questionResponseRequest = new D365UpdateRequest(new EntityReference(ofm_question_response.EntitySetName, qresponse.Id), questionResponseToUpdate); // new D365UpdateRequest("ofm_question_responses", questionData);
                                questionResponseRequestList.Add(questionResponseRequest);
                            }

                            var questionResponseRequestBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZPortalAppUser, questionResponseRequestList, null);

                            if (questionResponseRequestBatchResult.Errors.Any())
                            {
                                var sendQuestionResponseError = ProcessResult.Failure(BatchTypeId, questionResponseRequestBatchResult.Errors, questionResponseRequestBatchResult.TotalProcessed, questionResponseRequestBatchResult.TotalRecords);
                                _logger.LogError(CustomLogEvent.Batch, "Failed to re-associate question response to the new Provider Report copy with an error: {error}", JsonValue.Create(sendQuestionResponseError)!.ToString());

                                return sendQuestionResponseError.SimpleProcessResult;
                            }

                            ProcessResult processResult = ProcessResult.Success(questionResponseRequestBatchResult.ProcessId, questionResponseRequestBatchResult.TotalRecords);

                            return await Task.FromResult(processResult.SimpleProcessResult);

                        }
                    }
                    
                    #endregion              
                }
            }
        }

        return await Task.FromResult(new JsonObject());
    }
}