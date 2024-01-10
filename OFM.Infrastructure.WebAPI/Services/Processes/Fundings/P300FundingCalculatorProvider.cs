using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public class P300FundingCalculatorProvider : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private string _requestUri = string.Empty;
    private Dictionary<string, FundingRate[]> _parameters;


    public P300FundingCalculatorProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {

        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
        _parameters = new Dictionary<string, FundingRate[]>();
    }

    public Int16 ProcessId => Setup.Process.Funding.FundingCalculatorId;
    public string ProcessName => Setup.Process.Funding.FundingCalculatorName;

    
    //For reference
    public async Task<ProcessData> GetData()
    {
        throw new NotImplementedException();
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {

        #region Step 0: Get Fixed Parameters
        await SetupNonHRParameters();

        #endregion

        #region Step 1: Pre-Calculation
        //Get application and license data -> this will be moved to Calculator Object later so ignore
        var localData = await GetData();

        //Calculate the total spaces


        //Calculate the total operating hours

        #endregion

        #region  Step 2: Non-HR Calculation

        //1. Programming
        var totalSpaces = 25;
        var ownership = 0;
        var programmingFunding = 0.00;

        FundingRate[] programmingRate = _parameters["programming"].Where(rate => rate.ofm_ownership == ownership).OrderBy(rate => rate.ofm_step).ToArray();


        

        #endregion

        #region  Step 3: HR Calculation

        #endregion

        #region  Step 4: Post-Calculation

        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    #region Local Validation & Setup Code

    private async Task SetupNonHRParameters()
    {
              //For reference
/*            var fetchXml = """
                        <fetch>
                          <entity name="ofm_funding_rate">
                            <attribute name="ofm_ownership" />
                            <attribute name="ofm_rate" />
                            <attribute name="ofm_spaces_max" />
                            <attribute name="ofm_spaces_min" />
                            <attribute name="ofm_step" />
                            <attribute name="statecode" />
                            <link-entity name="ofm_rate_schedule" from="ofm_rate_scheduleid" to="ofm_rate_schedule">
                              <attribute name="ofm_fiscal_year" />
                              <attribute name="ofm_fundinng_envelope" />
                              <link-entity name="ofm_fiscal_year" from="ofm_fiscal_yearid" to="ofm_fiscal_year">
                                <attribute name="ofm_caption" />
                                <attribute name="statecode" />
                                <filter>
                                  <condition attribute="statuscode" operator="eq" value="1" />
                                </filter>
                              </link-entity>
                            </link-entity>
                          </entity>
                        </fetch>
                """;*/

        var requestUri = $"""
                            ofm_funding_rates?$select=ofm_ownership,ofm_rate,ofm_spaces_max,ofm_spaces_min,ofm_step,statecode&$expand=ofm_rate_schedule($select=_ofm_fiscal_year_value,ofm_fundinng_envelope;$expand=ofm_fiscal_year($select=ofm_caption,statecode))
                            """;

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUri);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the funding rate with a server error {responseBody}", responseBody.CleanLog());
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No funding rate found with query {requestUri}", requestUri);
                }
                d365Result = currentValue!;
            }

            var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<FundingRate>>(d365Result, Setup.s_writeOptionsForLogs);

        //Programming = 6, Administration = 7, Operational = 8, Facility = 9
        FundingRate[]? progammingScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 6).ToArray();
        FundingRate[]? administrationScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 7).ToArray();
        FundingRate[]? operationalScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 8).ToArray();
        FundingRate[]? facilityScheduleRate = serializedData?.Where(rate => rate?.ofm_rate_schedule?.ofm_fundinng_envelope == 9).ToArray();

        _parameters.Add("programming", progammingScheduleRate);
        _parameters.Add("administration", administrationScheduleRate);
        _parameters.Add("operational", operationalScheduleRate);
        _parameters.Add("facility", facilityScheduleRate);
    }

    #endregion
}