using Microsoft.Extensions.Logging;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.LicenceDetailRecords
{
    public class P900CalculateLicenceTotal(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
    {
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        private ProcessData? _data;
        private string? licenceFilterDate;
        
        private int Group_Child_Care_Under_36_Enrolled = 0;
        private int Group_Child_Care_Under_36_Licenced = 0;
        private int Group_Child_Care_Under_36_Operational = 0;
        private int Group_Child_Care_30_School_Age_Enrolled = 0;
        private int Group_Child_Care_30_School_Age_Licenced = 0;
        private int Group_Child_Care_30_School_Age_Operational = 0;
        private int Group_Multi_Age_Enrolled = 0;
        private int Group_Multi_Age_Licenced = 0;
        private int Group_Multi_Age_Operational = 0;
        private int Preschool_4Hour_1_Enrolled = 0;
        private int Preschool_4Hour_1_Licenced = 0;
        private int Preschool_4Hour_1_Operational = 0;
        private int Preschool_4Hour_2_Enrolled = 0;
        private int Preschool_4Hour_2_Licenced = 0;
        private int Preschool_4Hour_2_Operational = 0;
        private int Preschool_4Hour_3_Enrolled = 0;
        private int Preschool_4Hour_3_Licenced = 0;
        private int Preschool_4Hour_3_Operational = 0;
        private int Preschool_4Hour_4_Enrolled = 0;
        private int Preschool_4Hour_4_Licenced = 0;
        private int Preschool_4Hour_4_Operational = 0;
        private int Group_School_Age1_Enrolled = 0;
        private int Group_School_Age1_Licenced = 0;
        private int Group_School_Age1_Operational = 0;
        private int Group_School_Age2_Enrolled = 0;
        private int Group_School_Age2_Licenced = 0;
        private int Group_School_Age2_Operational = 0;
        private int Group_School_Age3_Enrolled = 0;
        private int Group_School_Age3_Licenced = 0;
        private int Group_School_Age3_Operational = 0;
        private int In_Home_Multi_Enrolled = 0;
        private int In_Home_Multi_Licenced = 0;
        private int In_Home_Multi_Operational = 0;
        private int Family_Enrolled = 0;
        private int Famnily_Licenced = 0;
        private int Family_Operational = 0;
        private int Total_Licenced_Spaces = 0;
        private int Total_Operational_Spaces = 0;
        private int Total_Enrolled_Spaces = 0;
        private decimal Total_Three_to_Five;
        private decimal Total_Under_Three;
        private decimal Total_Star_Percentage;

        public short ProcessId => Setup.Process.LicenceDetailRecords.CalculateLicenceTotal;
        public string ProcessName => Setup.Process.LicenceDetailRecords.CalculteLicenceTotalName;

        public string ServiceDeliveryDetailRequestURI
        {
            get
            {
                var fetchXml = $"""
                    <fetch aggregate="true">
                      <entity name="ofm_licence_detail">
                       <attribute name="ofm_enrolled_spaces" alias="Total_Enrolled_Spaces" aggregate="sum" />
                        <attribute name="ofm_licence_spaces" alias="Total_Licenced_Spaces" aggregate="sum" />
                        <attribute name="ofm_operational_spaces" alias="Total_Operational_Spaces" aggregate="sum" />
                        <attribute name="ofm_star_spaces_three_to_five_years" alias="Total_Three_to_Five" aggregate="sum" />
                        <attribute name="ofm_star_spaces_under_three_years" alias="Total_Under_Three" aggregate="sum" />
                        <attribute name="ofm_licence_type" groupby="true" alias="type" />
                        <filter>
                          <condition attribute="statecode" operator="eq" value="0" />
                        </filter>
                        <link-entity name="ofm_licence" from="ofm_licenceid" to="ofm_licence" link-type="inner">
                          <filter type="and">
                            <condition attribute="ofm_start_date" operator="on-or-before" value="{licenceFilterDate}" />
                            <condition attribute="statecode" operator="eq" value="0" />
                            <filter type="or">
                              <condition attribute="ofm_end_date" operator="null" />
                              <condition attribute="ofm_end_date" operator="on-or-after" value="{licenceFilterDate}" />
                            </filter>
                          </filter>
                          <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner">
                            <filter>
                              <condition attribute="accountid" operator="eq" value="{_processParams.Application.facilityId}" />
                            </filter>
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         ofm_licence_details?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }


        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P900CalculateLicenceTotal));

            if (_data is null && _processParams is not null)
            {
                if (_processParams.Application.submittedOn is not null)
                {
                    licenceFilterDate = _processParams.Application.submittedOn.ToString();
                }

                else
                {
                    licenceFilterDate = _processParams.Application.createdOn.ToString();
                }

                _logger.LogDebug(CustomLogEvent.Process, "Getting Licence Detail with query {requestUri}", ServiceDeliveryDetailRequestURI.CleanLog());

                var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, ServiceDeliveryDetailRequestURI, isProcess: true);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to query Licence Detail with the server error {responseBody}", responseBody.CleanLog());

                    return await Task.FromResult(new ProcessData(string.Empty));
                }

                var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

                JsonNode d365Result = string.Empty;
                if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
                {
                    if (currentValue?.AsArray().Count == 0)
                    {
                        _logger.LogInformation(CustomLogEvent.Process, "No Licence Detail found with query {requestUri}", ServiceDeliveryDetailRequestURI.CleanLog());
                    }
                    d365Result = currentValue!;
                }

                _data = new ProcessData(d365Result);

                _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data?.Data.ToString().CleanLog());
            }

            return await Task.FromResult(_data!);
        }
        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            _processParams = processParams;
            if (_processParams == null || _processParams.Application == null || _processParams.Application.applicationId == null ||_processParams.Application.facilityId == null
                || (_processParams.Application.submittedOn == null && _processParams.Application.createdOn == null))
            {
                _logger.LogError(CustomLogEvent.Process, "Application data is missing.");
                throw new Exception("Application data is missing.");
            }

            var startTime = _timeProvider.GetTimestamp();

            _logger.LogDebug(CustomLogEvent.Process, "Start of {nameof}", nameof(P900CalculateLicenceTotal));
            var licenceDetail = await GetDataAsync();
            var deserializedLicenceDetailData = JsonSerializer.Deserialize<List<LicenceDetail>>(licenceDetail.Data.ToString());
            #region commented code to hold total space functionality
            /*deserializedLicenceDetailData?.ForEach(licenceDetail =>
            {
                Total_Enrolled_Spaces += licenceDetail.Total_Enrolled_Spaces;
                Total_Licenced_Spaces += licenceDetail.Total_Licenced_Spaces;
                Total_Operational_Spaces += licenceDetail.Total_Operational_Spaces;
                Total_Under_Three += licenceDetail.Total_Under_Three;
                Total_Three_to_Five += licenceDetail.Total_Three_to_Five;

                switch (licenceDetail.Type)
                {
                    case 1:
                        Group_Child_Care_Under_36_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_Child_Care_Under_36_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_Child_Care_Under_36_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 2:
                        Group_Child_Care_30_School_Age_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_Child_Care_30_School_Age_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_Child_Care_30_School_Age_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 3:
                        Group_Multi_Age_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_Multi_Age_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_Multi_Age_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 4:
                        Preschool_4Hour_1_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Preschool_4Hour_1_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Preschool_4Hour_1_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 5:
                        Preschool_4Hour_2_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Preschool_4Hour_2_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Preschool_4Hour_2_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 6:
                        Preschool_4Hour_3_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Preschool_4Hour_3_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Preschool_4Hour_3_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 7:
                        Group_School_Age1_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_School_Age1_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_School_Age1_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 8:
                        Group_School_Age2_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_School_Age2_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_School_Age2_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 9:
                        Group_School_Age3_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Group_School_Age3_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Group_School_Age3_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 10:
                        In_Home_Multi_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        In_Home_Multi_Licenced += licenceDetail.Total_Licenced_Spaces;
                        In_Home_Multi_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 11:
                        Family_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Famnily_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Family_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                    case 12:
                        Preschool_4Hour_4_Enrolled += licenceDetail.Total_Enrolled_Spaces;
                        Preschool_4Hour_4_Licenced += licenceDetail.Total_Licenced_Spaces;
                        Preschool_4Hour_4_Operational += licenceDetail.Total_Operational_Spaces;
                        break;
                }
            });
            var updateApplicationRecord = new JsonObject {
                                {"ofm_group_child_care_under_36_months_enrolled", Group_Child_Care_Under_36_Enrolled},
                                {"ofm_group_child_care_under_36_months_licenced", Group_Child_Care_Under_36_Licenced},
                                {"ofm_group_child_care_under_36_months_op", Group_Child_Care_Under_36_Operational},
                                {"ofm_group_child_care_30_months_school_age_enrolled", Group_Child_Care_30_School_Age_Enrolled},
                                {"ofm_group_child_care_30_months_school_age_licenced", Group_Child_Care_30_School_Age_Licenced},
                                {"ofm_group_child_care_30_months_school_age_op", Group_Child_Care_30_School_Age_Operational},
                                {"ofm_group_multi_age_child_care_enrolled", Group_Multi_Age_Enrolled},
                                {"ofm_group_multi_age_child_care_licenced", Group_Multi_Age_Licenced},
                                {"ofm_group_multi_age_child_care_op", Group_Multi_Age_Operational},
                                {"ofm_preschool_4_hours_max_group1_enrolled", Preschool_4Hour_1_Enrolled},
                                {"ofm_preschool_4_hours_max_group1_licenced", Preschool_4Hour_1_Licenced},
                                {"ofm_preschool_4_hours_max_group1_op", Preschool_4Hour_1_Operational},
                                {"ofm_preschool_4_hours_max_group2_enrolled", Preschool_4Hour_2_Enrolled},
                                {"ofm_preschool_4_hours_max_group2_licenced", Preschool_4Hour_2_Licenced},
                                {"ofm_preschool_4_hours_max_group2_op", Preschool_4Hour_2_Operational},
                                {"ofm_preschool_4_hours_max_group3_enrolled", Preschool_4Hour_3_Enrolled},
                                {"ofm_preschool_4_hours_max_group3_licenced", Preschool_4Hour_3_Licenced},
                                {"ofm_preschool_4_hours_max_group3_op", Preschool_4Hour_3_Operational},
                                {"ofm_preschool_4_hours_max_group4_enrolled", Preschool_4Hour_4_Enrolled},
                                {"ofm_preschool_4_hours_max_group4_licenced", Preschool_4Hour_4_Licenced},
                                {"ofm_preschool_4_hours_max_group4_op", Preschool_4Hour_4_Operational},
                                {"ofm_group_child_care_school_age_group1_enrolled", Group_School_Age1_Enrolled},
                                {"ofm_group_child_care_school_age_group1_licenced", Group_School_Age1_Licenced},
                                {"ofm_group_child_care_school_age_group1_op", Group_School_Age1_Operational},
                                {"ofm_group_child_care_school_age_group2_enrolled", Group_School_Age2_Enrolled},
                                {"ofm_group_child_care_school_age_group2_licenced", Group_School_Age2_Licenced},
                                {"ofm_group_child_care_school_age_group2_op", Group_School_Age2_Operational},
                                {"ofm_group_child_care_school_age_group3_enrolled", Group_School_Age3_Enrolled},
                                {"ofm_group_child_care_school_age_group3_licenced", Group_School_Age3_Licenced},
                                {"ofm_group_child_care_school_age_group3_op", Group_School_Age3_Operational},
                                {"ofm_in_home_multi_age_child_care_enrolled", In_Home_Multi_Enrolled},
                                {"ofm_in_home_multi_age_child_care_licenced", In_Home_Multi_Licenced},
                                {"ofm_in_home_multi_age_child_care_op", In_Home_Multi_Operational},
                                {"ofm_family_child_care_enrolled", Family_Enrolled},
                                {"ofm_family_child_care_licenced", Famnily_Licenced},
                                {"ofm_family_child_care_op", Family_Operational},
                                {"ofm_total_licenced_spaces", Total_Licenced_Spaces},
                                {"ofm_total_operational_spaces", Total_Operational_Spaces},
                                {"ofm_total_enrolled_spaces", Total_Enrolled_Spaces},
                               };*/
            #endregion

            deserializedLicenceDetailData?.ForEach(licenceDetail =>
            {
                Total_Operational_Spaces += licenceDetail.Total_Operational_Spaces;
                Total_Under_Three += licenceDetail.Total_Under_Three;
                Total_Three_to_Five += licenceDetail.Total_Three_to_Five;

               
            });
           // var Total = Math.Round(Total_Under_Three + Total_Three_to_Five, 0, MidpointRounding.AwayFromZero);
            if (Total_Operational_Spaces != 0)
            {
                Total_Star_Percentage = Math.Round(Total_Under_Three + Total_Three_to_Five / Total_Operational_Spaces * 100, 0, MidpointRounding.AwayFromZero) ;
           }
            else
            {
                _logger.LogError("Total_Operational_Spaces is null or zero. Cannot compute percentage.");
            }

          
           var updateApplicationRecord = new JsonObject {
                                {"ofm_total_operational_spaces", Total_Operational_Spaces},
                                {"ofm_star_total_percentage",Total_Star_Percentage},
                               };
            var serializedApplicationRecord = JsonSerializer.Serialize(updateApplicationRecord);
            var updateApplicationResult = await d365WebApiService.SendPatchRequestAsync(_appUserService.AZSystemAppUser, $"ofm_applications({_processParams.Application.applicationId})", serializedApplicationRecord);

            if (!updateApplicationResult.IsSuccessStatusCode)
            {
                var responseBody = await updateApplicationResult.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to update Application record with the following server error {responseBody}", responseBody.CleanLog());
            }

            var endTime = timeProvider.GetTimestamp();
            _logger.LogDebug(CustomLogEvent.Process, "P900CalculateLicenceTotal process finished in {timer.ElapsedMilliseconds} miliseconds.", timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes);

            return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
        }
    }
}
