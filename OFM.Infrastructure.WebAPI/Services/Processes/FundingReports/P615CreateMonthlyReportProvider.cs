using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;

namespace OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;

public class P615CreateMonthlyReportProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly D365AuthSettings _d365AuthSettings;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P615CreateMonthlyReportProvider(IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _d365AuthSettings = d365AuthSettings.Value;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

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
                                      <condition attribute="statecode" operator="eq" value="{ECC.Core.DataContext.ofm_funding_statecode.Active}" />
                                      <condition attribute="statuscode" operator="eq" value="{ECC.Core.DataContext.ofm_funding_StatusCode.Active}" />
                                      <condition attribute="ofm_facility" operator="not-null" value="" />
                                    </filter>
                                  </entity>
                                </fetch>
                                """;

            var requestUri = $"""                                
                                ofm_fundings?$select=ofm_end_date,_ofm_facility_value,ofm_funding_number,ofm_start_date,statecode,statuscode&$filter=(statecode eq {(int)ECC.Core.DataContext.ofm_funding_statecode.Active} and statuscode eq {(int)ECC.Core.DataContext.ofm_funding_StatusCode.Active} and _ofm_facility_value ne null)
                                """;

            return requestUri.CleanCRLF();
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

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, uri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query fiscal year with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No fiscal year found with query {requestUri}", uri.CleanLog());
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

    public TimeZoneInfo GetPSTTimeZoneInfo(string timezoneId1, string timezoneId2)
    {
        try
        {
            TimeZoneInfo info = TimeZoneInfo.FindSystemTimeZoneById(timezoneId1);

            return info;
        }
        catch (System.TimeZoneNotFoundException)
        {
            try
            {
                TimeZoneInfo info = TimeZoneInfo.FindSystemTimeZoneById(timezoneId2);

                return info;
            }
            catch (System.TimeZoneNotFoundException)
            {
                _logger.LogError(CustomLogEvent.Process, "Could not find timezone by Id");
                return null;
            }
        }
        catch (System.Exception)
        {
            return null;
        }
    }


    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        var startTime = _timeProvider.GetTimestamp();

        _processParams = processParams;

        if (_processParams == null || _processParams.FundingReport == null || _processParams.FundingReport.BatchFlag == null)
        {
            _logger.LogError(CustomLogEvent.Process, "BatchFlag is missing.");
            throw new Exception("BatchFlag is missing.");

        }

        var batchFlag = (bool) _processParams.FundingReport.BatchFlag;

        List<string> facilities = [];

        var currentUTC = DateTime.UtcNow;
        DateTime monthEndDate = new DateTime();

        
       TimeZoneInfo PSTZone = GetPSTTimeZoneInfo("Pacific Standard Time", "America/Los_Angeles");
        
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
            monthEndDate = TimeZoneInfo.ConvertTimeFromUtc(currentUTC, PSTZone).AddMonths(-1);
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
            monthEndDate = TimeZoneInfo.ConvertTimeFromUtc(currentUTC, PSTZone);
        }

        var monthEndDateInPST = new DateTime(monthEndDate.Year, monthEndDate.Month, DateTime.DaysInMonth(monthEndDate.Year, monthEndDate.Month), 23, 59, 00);
        var monthEndDateInUTC = TimeZoneInfo.ConvertTimeToUtc(monthEndDateInPST, PSTZone);

        //Set the fiscal year and duedate
        //fetch the current fiscal year
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
        var fiscalYear = fiscalYearData.Data.AsArray().FirstOrDefault()?["ofm_fiscal_yearid"];

        //fetch the template
        var reportTemplateFetchXML = $"""
            <fetch distinct="true">
              <entity name="ofm_survey">
                <filter>
                  <condition attribute="ofm_start_date" operator="le" value="{monthEndDateInUTC}" />
                </filter>
              </entity>
            </fetch>
            """;
        var requestReportTemplateUri = $"""
                            ofm_surveies?fetchXml={WebUtility.UrlEncode(reportTemplateFetchXML)}
                            """.CleanCRLF();
        var reportTemplateData = await GetReportDataAsync(requestReportTemplateUri);
        var serializedReportTemplateDate = System.Text.Json.JsonSerializer.Deserialize<List<ECC.Core.DataContext.ofm_survey>>(reportTemplateData.Data, Setup.s_writeOptionsForLogs);
        var reportTemplate = serializedReportTemplateDate.Where(t => t.ofm_end_date == null || t.ofm_end_date >= monthEndDateInUTC).FirstOrDefault();
        var reportTempateId = reportTemplate.Id;


        //Convert the report Month
        var reportMonth = ConvertMonthToFiscalMonth(monthEndDateInPST.Month);
        var duedateInUTC = monthEndDateInUTC.AddMonths(1);

        List<HttpRequestMessage> requests = [];

        foreach (var facility in facilities)
        {

            var reportData = new JsonObject
            {
                {"ofm_facility@odata.bind", $"/accounts({facility})"},
                {"ofm_survey@odata.bind", $"/ofm_surveies({reportTempateId})" },
                {"ofm_fiscal_year@odata.bind", $"/ofm_fiscal_years({fiscalYear})" },
                {"ofm_report_month", reportMonth},
                {"ofm_duedate", duedateInUTC }
            };

            //Create a new report
            var newReportRequest = new CreateRequest("ofm_survey_responses", reportData);
            requests.Add(newReportRequest);
        }

        var batchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requests, null);

        var endTime = _timeProvider.GetTimestamp();

        _logger.LogInformation(CustomLogEvent.Process, "Create Monthly report process finished in {totalElapsedTime} minutes", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);

        if (batchResult.Errors.Any())
        {
            var result = ProcessResult.Failure(ProcessId, batchResult.Errors, batchResult.TotalProcessed, batchResult.TotalRecords);

            _logger.LogError(CustomLogEvent.Process, "Create Monthly report process finished with an error {error}", JsonValue.Create(result)!.ToJsonString());

            return result.SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

}