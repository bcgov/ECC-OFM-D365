using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;

public class P600CloneFundingReportResponse : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly D365AuthSettings _d365AuthSettings;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P600CloneFundingReportResponse(IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _d365AuthSettings = d365AuthSettings.Value;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.FundingReports.CloneFundingReportResponseId;
    public string ProcessName => Setup.Process.FundingReports.CloneFundingReportResponseName;
    public string RequestUri
    {
        get
        {
            // Note: Get the funding report response

                //for reference only
                var fetchXml = $"""
                                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="true">
                                  <entity name="ofm_question_response">
                                    <attribute name="createdby" />
                                    <attribute name="createdon" />
                                    <attribute name="createdonbehalfby" />
                                    <attribute name="importsequencenumber" />
                                    <attribute name="modifiedby" />
                                    <attribute name="modifiedon" />
                                    <attribute name="modifiedonbehalfby" />
                                    <attribute name="ofm_embedded_data" />
                                    <attribute name="ofm_header" />
                                    <attribute name="ofm_multiple_line" />
                                    <attribute name="ofm_name" />
                                    <attribute name="ofm_question" />
                                    <attribute name="ofm_question_responseid" />
                                    <attribute name="ofm_response_text" />
                                    <attribute name="ofm_row_id" />
                                    <attribute name="ofm_survey_response" />
                                    <attribute name="overriddencreatedon" />
                                    <attribute name="ownerid" />
                                    <attribute name="owningbusinessunit" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <attribute name="timezoneruleversionnumber" />
                                    <attribute name="utcconversiontimezonecode" />
                                    <attribute name="versionnumber" />
                                    <filter>
                                      <condition attribute="ofm_survey_response" operator="eq" value="{_processParams?.FundingReport?.FundingReportId}" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

                var requestUri = $"""                                
                                ofm_question_responses?$select=_createdby_value,createdon,_createdonbehalfby_value,importsequencenumber,_modifiedby_value,modifiedon,_modifiedonbehalfby_value,ofm_embedded_data,_ofm_header_value,ofm_multiple_line,ofm_name,_ofm_question_value,ofm_question_responseid,ofm_response_text,ofm_row_id,_ofm_survey_response_value,overriddencreatedon,_ownerid_value,_owningbusinessunit_value,statecode,statuscode,timezoneruleversionnumber,utcconversiontimezonecode,versionnumber&$filter=(_ofm_survey_response_value eq '{_processParams?.FundingReport?.FundingReportId}')
                                """;

            return requestUri.CleanCRLF();
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P600CloneFundingReportResponse));

        if (_data is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting  with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query question response with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No question response found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_data);
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {

        _processParams = processParams;

        if(_processParams == null || _processParams.FundingReport == null || _processParams.FundingReport.FundingReportId == null)
        {
            _logger.LogError(CustomLogEvent.Process, "Funding Report Response is missing.");
            throw new Exception("Funding Report Response is missing.");

        }

        var startTime = _timeProvider.GetTimestamp();

        var fundingReportData = await InitializeCloneRequest(_processParams.FundingReport.FundingReportId, "ofm_survey_response");
        fundingReportData["ofm_current_version@odata.bind"] = $"/ofm_survey_responses({_processParams.FundingReport.FundingReportId})";
        fundingReportData["ofm_unlock"] = false;

        //Create a new fundingReport
        var sendNewFundingReportResponseRequestResult = await d365WebApiService.SendCreateRequestAsync(_appUserService.AZSystemAppUser, "ofm_survey_responses", fundingReportData.ToString());

        if (!sendNewFundingReportResponseRequestResult.IsSuccessStatusCode)
        {
            var responseBody = await sendNewFundingReportResponseRequestResult.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create the record with the server error {responseBody}", responseBody.CleanLog());
        }

        var newFundingReportResponse = await sendNewFundingReportResponseRequestResult.Content.ReadFromJsonAsync<JsonObject>();
        var newFundingReportResponseId = newFundingReportResponse?["ofm_survey_responseid"];

        //Update the original funding report version number by 1
        var orignialVersionNumber = (int)newFundingReportResponse?["ofm_version_number"];
        var newVersionNumber = orignialVersionNumber + 1;

        var updateFundingReportData = new JsonObject
            {
                {"ofm_version_number", newVersionNumber }
            };

        var updateFundingReportRequest = JsonSerializer.Serialize(updateFundingReportData);

        var updateFundingReportResult = await d365WebApiService.SendPatchRequestAsync(_appUserService.AZSystemAppUser, $"ofm_survey_responses({_processParams.FundingReport.FundingReportId})", updateFundingReportRequest);

        if (!updateFundingReportResult.IsSuccessStatusCode)
        {
            var responseBody = await updateFundingReportResult.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to deactivate the record with the server error {responseBody}", responseBody.CleanLog());
        }


        //Fetch Question Response from the funding report response
        var questionResponseData = await GetDataAsync();

        var questionResponse = System.Text.Json.JsonSerializer.Deserialize<List<ofm_question_response>>(questionResponseData.Data, Setup.s_writeOptionsForLogs);

        if (questionResponse.Count > 0)
        {
            List<HttpRequestMessage> questionResponseRequestList = [];
            foreach (var question in questionResponse)
            {
                var questionData = await InitializeCloneRequest(question.Id.ToString(), "ofm_question_response");
                //Update the funding Report lookup
                questionData["ofm_survey_response@odata.bind"] = $"/ofm_survey_responses({newFundingReportResponseId})";
                questionData["ofm_current_version@odata.bind"] = $"/ofm_question_responses({question.Id})";

                //Create a new questionResponse
                var newQuestionResponseRequest = new CreateRequest("ofm_question_responses", questionData);
                newQuestionResponseRequest.Headers.Add("Prefer", "return=representation");
                questionResponseRequestList.Add(newQuestionResponseRequest);

            }
            var sendquestionResponseRequestBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, questionResponseRequestList, null);

            if (sendquestionResponseRequestBatchResult.Errors.Any())
            {
                var sendQuestionResponseError = ProcessResult.Failure(ProcessId, sendquestionResponseRequestBatchResult.Errors, sendquestionResponseRequestBatchResult.TotalProcessed, sendquestionResponseRequestBatchResult.TotalRecords);
                _logger.LogError(CustomLogEvent.Process, "Failed to clone question response with an error: {error}", JsonValue.Create(sendQuestionResponseError)!.ToString());

                return sendQuestionResponseError.SimpleProcessResult;
            }


            //Deactivate the copied funding report
            var deactivateFundingReportData = new JsonObject
            {
                {"statuscode", 2 },
                {"statecode", 1 }
            };

            var deactivateFundingReportRequest = JsonSerializer.Serialize(deactivateFundingReportData);

            var deactivateFundingReportRequestResult = await d365WebApiService.SendPatchRequestAsync(_appUserService.AZSystemAppUser, $"ofm_survey_responses({newFundingReportResponseId})", deactivateFundingReportRequest);

            if (!deactivateFundingReportRequestResult.IsSuccessStatusCode)
            {
                var responseBody = await deactivateFundingReportRequestResult.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to deactivate the record with the server error {responseBody}", responseBody.CleanLog());
            }

            //Deactivate the copied question responses
            var createResult = sendquestionResponseRequestBatchResult.Result;

            if(createResult?.Count() > 0)
            {
                List<HttpRequestMessage> deactivatedQuestionResponseRequestList = [];

                foreach (var res in createResult)
                {
                    var deactivatedQuestionResponseId = res["ofm_question_responseid"].ToString();
                    var deactivateQuestionResponseData = new JsonObject
                    {
                        {"statuscode",  2},
                        {"statecode", 1 }
                    };
                    deactivatedQuestionResponseRequestList.Add(new D365UpdateRequest(new D365EntityReference("ofm_question_responses", new Guid(deactivatedQuestionResponseId)), deactivateQuestionResponseData));

                  
                }

                var deactivatedQuestionResponseRequestResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, deactivatedQuestionResponseRequestList, null);

                if (deactivatedQuestionResponseRequestResult.Errors.Any())
                {
                    var deactivatedQuestionResponseError = ProcessResult.Failure(ProcessId, deactivatedQuestionResponseRequestResult.Errors, deactivatedQuestionResponseRequestResult.TotalProcessed, deactivatedQuestionResponseRequestResult.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to deactivate question response with an error: {error}", JsonValue.Create(deactivatedQuestionResponseError)!.ToString());

                    return deactivatedQuestionResponseError.SimpleProcessResult;
                }

            }

        }

        _logger.LogInformation(CustomLogEvent.Process, "A new copy of funding report response is created");

        var endTime = _timeProvider.GetTimestamp();

        var result = ProcessResult.Success(ProcessId, 1);
        _logger.LogInformation(CustomLogEvent.Process, "Create funding report response process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString());
        return result.SimpleProcessResult;
    }


    public async Task<JsonObject> InitializeCloneRequest(string id, string targetEntityName)
    {
 
        var entityMoniker = "{" + "'@odata.id':'" + targetEntityName + "s" + $"({id})'" + "}";
        var fundingReportReponseUri = $"""                                
                                InitializeFrom(EntityMoniker=@p1,TargetEntityName=@p2,TargetFieldType=@p3)?@p1={entityMoniker}&@p2='{targetEntityName}'&@p3=Microsoft.Dynamics.CRM.TargetFieldType'ValidForCreate'
                                """;

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, fundingReportReponseUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

            return await Task.FromResult<JsonObject>(null);
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();


        if(jsonObject != null)
        {
            return await Task.FromResult<JsonObject>(jsonObject);
        }
        else
        {
            _logger.LogInformation(CustomLogEvent.Process, "No record found");
            return await Task.FromResult<JsonObject>(null);
        }
    }

}