using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;

namespace OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;

public class P615CreateMonthlyReportProvider(IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly D365AuthSettings _d365AuthSettings = d365AuthSettings.Value;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _facId;
    private DateTime _previousMonth;
    private string _hrQuestionIdsXML;
    private Guid _reportTemplateId;
    private DateTime firstOfCurrentMonth;

    public Int16 ProcessId => Setup.Process.FundingReports.CreateMonthlyReportId;
    public string ProcessName => Setup.Process.FundingReports.CreateMonthlyReportName;
    public string RequestUri
    {
        get
        {
            // Note: Get the active funding record
            //for reference only
            var fetchXml = $"""
                                <fetch distinct="true">
                                  <entity name="ofm_funding">
                                    <attribute name="ofm_end_date" />
                                    <attribute name="ofm_facility" />
                                    <attribute name="ofm_funding_number" />
                                    <attribute name="ofm_start_date" />
                                    <attribute name="statecode" />
                                    <attribute name="statuscode" />
                                    <filter>
                                      <condition attribute="statecode" operator="eq" value="{(int)ECC.Core.DataContext.ofm_funding_statecode.Active}" />
                                      <condition attribute="statuscode" operator="eq" value="{(int)ECC.Core.DataContext.ofm_funding_StatusCode.Active}" />
                                      <condition attribute="ofm_facility" operator="not-null" value="" />
                                      <condition attribute="ofm_start_date" operator="le" value="{firstOfCurrentMonth}" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

            var requestUri = $"""                                
                                ofm_fundings?fetchXml={WebUtility.UrlEncode(fetchXml)}
                                """;

            return requestUri.CleanCRLF();
        }
    }

    public string HRQuestionTemplateUri
    {
        get
        {
            // Note: Get the active funding record
            //for reference only
            var fetchXml = $"""
                                <fetch distinct="true">
                                  <entity name="ofm_question">
                                    <attribute name="ofm_question_id" />
                                    <attribute name="ofm_header" />
                                    <attribute name="ofm_questionid" />
                                    <filter>
                                      <condition attribute="ofm_question_id" operator="in">
                                        {_hrQuestionIdsXML}
                                      </condition>
                                    </filter>
                                    <link-entity name="ofm_section" from="ofm_sectionid" to="ofm_section">
                                      <link-entity name="ofm_survey" from="ofm_surveyid" to="ofm_survey">
                                        <filter>
                                          <condition attribute="ofm_surveyid" operator="eq" value="{_reportTemplateId}" />
                                        </filter>
                                      </link-entity>
                                    </link-entity>
                                  </entity>
                                </fetch>
                                """;

            var hrQuestionTemplateUri = $"""
                            ofm_questions?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """.CleanCRLF();
            return hrQuestionTemplateUri;
        }
    }

    public string HRQuestionResponseUri
    {
        get
        {
            // Note: Get the active funding record
            //for reference only
            var fetchXml = $"""
                                <fetch version="1.0" mapping="logical" no-lock="true" distinct="true">
                                  <entity name="ofm_question_response">
                                    <attribute name="ofm_response_text" />
                                    <link-entity name="ofm_survey_response" link-type="inner" from="ofm_survey_responseid" to="ofm_survey_response">
                                      <filter type="and">
                                        <condition attribute="ofm_facility" operator="eq" value="{_facId}" />
                                        <condition attribute="ofm_start_date" operator="on" value="{_previousMonth}" />
                                        <condition attribute="ofm_submitted_on" operator="not-null" />
                                      </filter>
                                    </link-entity>
                                    <link-entity name="ofm_question" from="ofm_questionid" to="ofm_question" link-type="outer" alias="question">
                                      <attribute name="ofm_question_id" />
                                      <filter>
                                        <condition attribute="ofm_question_id" operator="in">
                                         {_hrQuestionIdsXML}
                                        </condition>
                                      </filter>
                                      <link-entity name="ofm_section" from="ofm_sectionid" to="ofm_section">
                                        <filter>
                                          <condition attribute="ofm_section_title" operator="begins-with" value="Human" />
                                        </filter>
                                      </link-entity>
                                    </link-entity>
                                    <link-entity name="ofm_question" from="ofm_questionid" to="ofm_header" link-type="outer" alias="header" visible="false">
                                      <attribute name="ofm_question_id" />
                                    </link-entity>
                                    <attribute name="ofm_row_id" />
                                  </entity>
                                </fetch>
                                """;

            var previousHRQuestionResponseUri = $"""
                            ofm_question_responses?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """.CleanCRLF();
            return previousHRQuestionResponseUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P615CreateMonthlyReportProvider));

        if (_data is null)
        {
            _logger.LogDebug(CustomLogEvent.Process, "Getting  with query {requestUri}", RequestUri);

            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query reports with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No facility found with query {requestUri}", RequestUri);
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString(Setup.s_writeOptionsForLogs));
        }

        return await Task.FromResult(_data);
    }


    public async Task<ProcessData> GetReportDataAsync(string uri)
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetTemplateToSendEmail");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, uri, isProcess:true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query result with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No result found with query {requestUri}", uri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public int ConvertMonthToFiscalMonth(int month)
    {
        if (month <= 0 || month > 13)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var fiscalMonth = month - 3 > 0 ? month - 3 : month - 3 + 12;
        return fiscalMonth;
    }


    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        var startTime = _timeProvider.GetTimestamp();

        _processParams = processParams;

        if (_processParams == null || _processParams.FundingReport == null || _processParams.FundingReport.BatchFlag == null || _processParams.FundingReport.HRQuestions == null)
        {
            _logger.LogError(CustomLogEvent.Process, "BatchFlag is missing.");
            throw new Exception("BatchFlag is missing.");

        }

        var batchFlag = (bool) _processParams.FundingReport.BatchFlag;

        List<string> facilities = [];

        var currentUTC = DateTime.UtcNow;
        DateTime monthEndDate = new DateTime();
        DateTime monthEndDateInPST = new DateTime();

        var currentMonthPST = currentUTC.ToLocalPST().AddMonths(-1);
        firstOfCurrentMonth = new DateTime(currentMonthPST.Year, currentMonthPST.Month, 1, 23, 59, 59);

        //batch create the monthly report
        if (batchFlag)
        {
            var localData = await GetDataAsync();
            if (localData.Data.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "Create monthly report process completed. No facilities found.");
                return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            }

            facilities = localData.Data.AsArray().Select(fac => fac["_ofm_facility_value"].ToString()).Distinct().ToList();

            //batch creation -> create the report for previous month

            // april report
            //Run at PST May 1 1AM -> Min 1 month -> April 1 1AM
            monthEndDate = currentUTC.ToLocalPST().AddMonths(-1);
            monthEndDateInPST = new DateTime(monthEndDate.Year, monthEndDate.Month, DateTime.DaysInMonth(monthEndDate.Year, monthEndDate.Month), 23, 59, 00);
        }
        else
        {
            if(_processParams.FundingReport.FacilityId == null)
            {
                _logger.LogError(CustomLogEvent.Process, "Facility id is missing.");
                throw new Exception("Facility id is missing.");
            }

            var facilityId = _processParams.FundingReport.FacilityId;

            facilities.Add(facilityId);

            //when funding is expired or terminated -> create the report for current month
            monthEndDate = currentUTC.ToLocalPST();
            monthEndDateInPST = monthEndDate;
        }

        var monthEndDateInUTC = monthEndDateInPST.ToUTC();

        //Set the fiscal year and duedate
        //fetch the current fiscal year -> fiscal year startDate and endDate are not DateOnly
        var fiscalYearFetchXML = $"""
            <fetch distinct="true">
              <entity name="ofm_fiscal_year">
                <filter>
                  <condition attribute="ofm_end_date" operator="ge" value="{monthEndDateInUTC}" />
                  <condition attribute="ofm_start_date" operator="le" value="{monthEndDateInUTC}" />
                </filter>
              </entity>
            </fetch>
            """;

        var requestFiscalYearUri = $"""
                            ofm_fiscal_years?fetchXml={WebUtility.UrlEncode(fiscalYearFetchXML)}
                            """.CleanCRLF();

        var fiscalYearData = await GetReportDataAsync(requestFiscalYearUri);
        if (fiscalYearData == null)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Cannot find fiscal year data.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        var fiscalYear = fiscalYearData.Data.AsArray().FirstOrDefault()?["ofm_fiscal_yearid"];

        //fetch the template -> template startDate and endDate are DateOnly -> PST Date
        var reportTemplateFetchXML = $"""
            <fetch distinct="true">
              <entity name="ofm_survey">
                <filter>
                  <condition attribute="ofm_start_date" operator="on-or-before" value="{monthEndDateInPST}" />
                  <condition attribute="statecode" operator="eq" value="{(int)ECC.Core.DataContext.ofm_survey_statecode.Active}" />
                  <condition attribute="statuscode" operator="eq" value="{(int)ECC.Core.DataContext.ofm_survey_StatusCode.Active}" />
                </filter>
              </entity>
            </fetch>
            """;
        var requestReportTemplateUri = $"""
                            ofm_surveies?fetchXml={WebUtility.UrlEncode(reportTemplateFetchXML)}
                            """.CleanCRLF();
        var reportTemplateData = await GetReportDataAsync(requestReportTemplateUri);
        var serializedReportTemplateDate = System.Text.Json.JsonSerializer.Deserialize<List<ECC.Core.DataContext.ofm_survey>>(reportTemplateData.Data, Setup.s_writeOptionsForLogs);
        var reportTemplate = serializedReportTemplateDate.Where(t => t.ofm_end_date == null || t.ofm_end_date?.Date >= monthEndDateInPST.Date).FirstOrDefault();
        if(reportTemplate  == null)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Cannot find report template.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }
        _reportTemplateId = reportTemplate.Id;


        //Convert the report Month
        var reportMonth = ConvertMonthToFiscalMonth(monthEndDateInPST.Month);

        var duedateInUTC = monthEndDateInUTC.AddMonths(1);

        if (!batchFlag)
        {
            var duedateMonth = monthEndDateInUTC.AddMonths(1);
            var duedateInPST = new DateTime(duedateMonth.Year, duedateMonth.Month, DateTime.DaysInMonth(duedateMonth.Year, duedateMonth.Month), 23, 59, 00);
            duedateInUTC = duedateInPST.ToUTC();
        }


        //Start date
        var startdateInPST = new DateTime(monthEndDateInPST.Year, monthEndDateInPST.Month, 1).ToString("yyyy-MM-dd");

        List<HttpRequestMessage> requests = [];

        foreach (var facility in facilities)
        {

            var reportData = new JsonObject
            {
                {"ofm_facility@odata.bind", $"/accounts({facility})"},
                {"ofm_survey@odata.bind", $"/ofm_surveies({_reportTemplateId})" },
                {"ofm_fiscal_year@odata.bind", $"/ofm_fiscal_years({fiscalYear})" },
                {"ofm_report_month", reportMonth},
                {"ofm_duedate", duedateInUTC },
                { "ofm_start_date", startdateInPST}
            };

            //Create a new report
            var newReportRequest = new CreateRequest("ofm_survey_responses", reportData);
            newReportRequest.Headers.Add("Prefer", "return=representation");
            requests.Add(newReportRequest);
        }

        var batchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requests, null);


        if (batchResult.Errors.Any())
        {
            var result = ProcessResult.Failure(ProcessId, batchResult.Errors, batchResult.TotalProcessed, batchResult.TotalRecords);

            _logger.LogError(CustomLogEvent.Process, "Create Monthly report process finished with an error {error}", JsonValue.Create(result)!.ToJsonString());

            return result.SimpleProcessResult;
        }


        #region Step 2: Copy Response from previous month
        var createdReports = batchResult.Result;
        _previousMonth = new DateTime(monthEndDateInPST.Year, monthEndDateInPST.Month, 1).AddMonths(-1);
        var hrQuestionIds = _processParams.FundingReport.HRQuestions.Split(",");
        _hrQuestionIdsXML = "<value>" + String.Join("</value>\r\n            <value>", hrQuestionIds) + "</value>";

        //Get question template for current report template

        var hrQuestionTemplateData = await GetReportDataAsync(HRQuestionTemplateUri);
        var serializedHRQuestionTemplateData = System.Text.Json.JsonSerializer.Deserialize<List<Question>>(hrQuestionTemplateData.Data, Setup.s_writeOptionsForLogs);

        List<HttpRequestMessage> questionResponseRequests = [];

        foreach (var report in createdReports)
        {
            var uid = report["ofm_survey_responseid"].ToString();
            _facId = report["_ofm_facility_value"].ToString();

            var previousHRQuestionResponseData = await GetReportDataAsync(HRQuestionResponseUri);
            var serializedPreviousHRQuestionResponseData = System.Text.Json.JsonSerializer.Deserialize<List<QuestionResponse>>(previousHRQuestionResponseData.Data, Setup.s_writeOptionsForLogs);

            foreach(var questionResponse in serializedPreviousHRQuestionResponseData)
            {

                var questionTemplateId = $"/ofm_questions({serializedHRQuestionTemplateData?.Where(q => q.ofm_question_id == questionResponse.ofm_question_qid).FirstOrDefault().ofm_questionid})";
                var headerTemplateId = String.IsNullOrEmpty(questionResponse.ofm_header_qid) ? null:$"/ofm_questions({serializedHRQuestionTemplateData?.Where(q => q.ofm_question_id == questionResponse.ofm_header_qid).FirstOrDefault().ofm_questionid})";

                var newQuestionResponse = new JsonObject
                    {
                        {"ofm_survey_response@odata.bind", $"/ofm_survey_responses({uid})"},
                        {"ofm_question@odata.bind", questionTemplateId },
                        {"ofm_header@odata.bind", headerTemplateId },
                        {"ofm_row_id", questionResponse.ofm_row_id },
                        {"ofm_response_text", questionResponse.ofm_response_text}
                    };

                    //Create a new report
                    var newQuestionResponseRequest = new CreateRequest("ofm_question_responses", newQuestionResponse);
                    questionResponseRequests.Add(newQuestionResponseRequest);
            }
        }

        if(questionResponseRequests.Count == 0)
        {
            _logger.LogInformation(CustomLogEvent.Process, "Cannot find HR questions responses.");
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
        }

        var questionResponseBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, questionResponseRequests, null);

        var endTime = _timeProvider.GetTimestamp();

        _logger.LogInformation(CustomLogEvent.Process, "Create Monthly report process finished in {totalElapsedTime} minutes", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);


        if (questionResponseBatchResult.Errors.Any())
        {
            var result = ProcessResult.Failure(ProcessId, questionResponseBatchResult.Errors, questionResponseBatchResult.TotalProcessed, questionResponseBatchResult.TotalRecords);

            _logger.LogError(CustomLogEvent.Process, "Copy HR response process finished with an error {error}", JsonValue.Create(result)!.ToJsonString());

            return result.SimpleProcessResult;
        }

        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

}