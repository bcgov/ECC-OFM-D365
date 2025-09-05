using ECC.Core.DataContext;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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
        private int Total_Operational_Spaces = 0;
        private decimal Total_Three_to_Five;
        private decimal Total_Under_Three;
        private decimal Total_Star_Percentage;
        private decimal School_Space_Ratio;
        public short ProcessId => Setup.Process.LicenceDetailRecords.CalculateLicenceTotal;
        public string ProcessName => Setup.Process.LicenceDetailRecords.CalculteLicenceTotalName;

        public string SDDforOperationalSpaceRequestURI
        {

            get
            {
                var fetchXml = $"""
                    <fetch>
                      <entity name="ofm_licence_detail">
                        <attribute name="ofm_operational_spaces"/>
                        <attribute name="ofm_program_session"/>
                     <attribute name="ofm_star_spaces_six_to_twelve_years"/>
                     <attribute name="ofm_star_spaces_three_to_five_years"/>
                     <attribute name="ofm_star_spaces_under_three_years"/>
                     <attribute name="ofm_default_program_session"/>
                     <attribute name="ofm_licence_detailid"/>
                       <attribute name="ofm_licence_type"/>
                        <filter>
                          <condition attribute="statecode" operator="eq" value="0"/>                         
                         </filter>
                        <link-entity name="ofm_licence" from="ofm_licenceid" to="ofm_licence" link-type="inner">
                          <filter type="and">
                            <condition attribute="ofm_start_date" operator="on-or-before" value="{licenceFilterDate}"/>
                            <condition attribute="statecode" operator="eq" value="0"/>
                            <filter type="or">
                              <condition attribute="ofm_end_date" operator="null"/>
                              <condition attribute="ofm_end_date" operator="on-or-after" value="{licenceFilterDate}"/>
                            </filter>
                          </filter>
                          <link-entity name="account" from="accountid" to="ofm_facility" link-type="inner">
                            <filter>
                              <condition attribute="accountid" operator="eq" value="{_processParams.Application.facilityId}"/>
                            </filter>
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""                                
                               ofm_licence_details?$select=ofm_star_spaces_six_to_twelve_years,ofm_star_spaces_three_to_five_years,ofm_star_spaces_under_three_years,ofm_operational_spaces,ofm_program_session,ofm_default_program_session,ofm_licence_type&$filter=statecode eq 0 and ofm_licence/ofm_start_date le {DateTime.Parse(licenceFilterDate).ToString("yyyy-MM-dd")} and (ofm_licence/ofm_end_date eq null or ofm_licence/ofm_end_date ge {DateTime.Parse(licenceFilterDate).ToString("yyyy-MM-dd")}) and ofm_licence/ofm_facility/accountid eq {_processParams.Application.facilityId}&$expand=ofm_licence($expand=ofm_facility)
                  """;

                return requestUri.CleanCRLF();


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
                    _logger.LogDebug(CustomLogEvent.Process, "Getting Licence Detail with query {requestUri}", SDDforOperationalSpaceRequestURI.CleanLog());

                    var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, SDDforOperationalSpaceRequestURI, true, isProcess: true);

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
                            _logger.LogInformation(CustomLogEvent.Process, "No Licence Detail found with query {requestUri}", SDDforOperationalSpaceRequestURI.CleanLog());
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
            if (_processParams == null || _processParams.Application == null || _processParams.Application.applicationId == null || _processParams.Application.facilityId == null
                || (_processParams.Application.submittedOn == null && _processParams.Application.createdOn == null))
            {
                _logger.LogError(CustomLogEvent.Process, "Application data is missing.");
                throw new Exception("Application data is missing.");
            }

            var startTime = _timeProvider.GetTimestamp();
            var updateLicenseRequest = new List<HttpRequestMessage>() { };
            _logger.LogDebug(CustomLogEvent.Process, "Start of {nameof}", nameof(P900CalculateLicenceTotal));
            var oplicencedetail = await GetDataAsync();
            var updateSessionDetailRequests = new List<HttpRequestMessage>() { };
            var deserializedopLicenceDetailData = JsonSerializer.Deserialize<List<D365LicenceDetail>>(oplicencedetail.Data.ToString());
            try
            { 
            var enrichedRecords = from d in deserializedopLicenceDetailData
                                  select new
                                  {
                                      Detail = d,
                                      ofm_program_session = string.IsNullOrWhiteSpace(d.ofm_program_session)
                                          ? string.IsNullOrWhiteSpace(d.ofm_default_program_session) ? (d.ofm_licence_typename.Contains("(School-Age)") ? "School Age Care" :
                                             d.ofm_licence_typename.Contains("Preschool (") ? d.ofm_licence_typename : Regex.Replace(d.ofm_licence_typename, @"group\s+\d+\b", "", RegexOptions.IgnoreCase).Trim())
                                          : d.ofm_default_program_session : d.ofm_program_session,
                                      ofm_licence_category = Regex.Replace(d.ofm_licence_typename, @"group\s+\d+\b", "", RegexOptions.IgnoreCase).Trim()

                                  };

            JsonObject updateSessionDetail;


            foreach (var eachRec in enrichedRecords)
            {
                var isUpdate = deserializedopLicenceDetailData?.FirstOrDefault(d => d.ofm_licence_detailid == eachRec.Detail.ofm_licence_detailid && string.IsNullOrEmpty(d.ofm_program_session) && string.IsNullOrEmpty(d.ofm_default_program_session));
                if (isUpdate != null)
                {
                    updateSessionDetail = new JsonObject {
                { "ofm_default_program_session", eachRec.ofm_program_session }
                };


                    updateSessionDetailRequests.Add(new D365UpdateRequest(new D365EntityReference("ofm_licence_details", (Guid)eachRec.Detail.ofm_licence_detailid), updateSessionDetail));
                }
            }

            if (updateSessionDetailRequests.Count > 0)
            {
                var updateSessionDetailResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateSessionDetailRequests, null);
                if (updateSessionDetailResults.Errors.Any())
                {
                    var sendError = ProcessResult.Failure(ProcessId, updateSessionDetailResults.Errors, updateSessionDetailResults.TotalProcessed, updateSessionDetailResults.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to update default program session: {error}", JsonValue.Create(sendError)!.ToString());

                    return await Task.FromResult(sendError.SimpleProcessResult);
                }
            }

            var licensecategorygroup = enrichedRecords.GroupBy(x => new
            {
                LicenceId = x.Detail.ofm_licence.ofm_licenceid,
                LicenceCategory = x.ofm_licence_category,
                ProgramSessionName = x.ofm_program_session

            }).Select(g => new
            {
                LicenceName = g.Key.LicenceId,
                LicenceCategoryName = g.Key.LicenceCategory,
                ProgramSessionName = g.Key.ProgramSessionName,
                OperationalSpacespercategory = g.Max(x => x.Detail.ofm_operational_spaces),
                SpaceUnderThree = g.Max(x => x.Detail.ofm_star_spaces_under_three_years),
                SpacesThreetoFive = g.Max(x => x.Detail.ofm_star_spaces_three_to_five_years),
                SpacesSixtoTwelve = g.Max(x => x.Detail.ofm_star_spaces_six_to_twelve_years),


            });




            foreach (var eachRec in licensecategorygroup.DistinctBy(i => i.LicenceName))
            {
                Total_Under_Three += licensecategorygroup.Where(i => i.LicenceName == eachRec.LicenceName).Sum(i => i.SpaceUnderThree);
                Total_Three_to_Five += licensecategorygroup.Where(i => i.LicenceName == eachRec.LicenceName).Sum(i => i.SpacesThreetoFive);

                var updateLicense = new JsonObject {
                {"ofm_star_space_under_three", licensecategorygroup.Where(i=>i.LicenceName == eachRec.LicenceName).Sum(i=>i.SpaceUnderThree)},
                {"ofm_star_space_three_to_five_year", licensecategorygroup.Where(i=>i.LicenceName == eachRec.LicenceName).Sum(i=>i.SpacesThreetoFive)},
                {"ofm_star_space_six_to_twelve_years", licensecategorygroup.Where(i=>i.LicenceName == eachRec.LicenceName).Sum(i=>i.SpacesSixtoTwelve)}
             };


                updateLicenseRequest.Add(new D365UpdateRequest(new D365EntityReference("ofm_licences", (Guid)eachRec.LicenceName), updateLicense));
            }

            if (updateLicenseRequest.Count > 0)
            {
                var updateLicenseResults = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updateLicenseRequest, null);
                if (updateLicenseResults.Errors.Any())
                {
                    var sendError = ProcessResult.Failure(ProcessId, updateLicenseResults.Errors, updateLicenseResults.TotalProcessed, updateLicenseResults.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to update default program session: {error}", JsonValue.Create(sendError)!.ToString());

                    return await Task.FromResult(sendError.SimpleProcessResult);
                }
            }



            // Final total operational spaces for facility
            Total_Operational_Spaces = (int)licensecategorygroup.Sum(v => v.OperationalSpacespercategory);


            if (Total_Operational_Spaces != 0)
            {
                Total_Star_Percentage = Math.Round((Total_Under_Three + Total_Three_to_Five) / Total_Operational_Spaces * 100, 0, MidpointRounding.AwayFromZero);
                School_Space_Ratio = Math.Round((decimal)licensecategorygroup.Where(type => type.LicenceCategoryName.Contains("Group Child Care (School-Age)")).Sum(x => x.OperationalSpacespercategory) / Total_Operational_Spaces,2, MidpointRounding.AwayFromZero);

                }
            else
            {
                _logger.LogError("Total_Operational_Spaces is null or zero. Cannot compute percentage.");
            }


            var updateApplicationRecord = new JsonObject {
                                {"ofm_total_operational_spaces", Total_Operational_Spaces},
                                {"ofm_group_child_care_under_36_months_op", (int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Under 36 Month")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_group_child_care_30_months_school_age_op", (int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("30 Months to School-Age")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_group_multi_age_child_care_op", (int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Group Multi-Age Child Care")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_preschool_4_hours_op", (int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Preschool (4 Hours Max)")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_in_home_multi_age_child_care_op",(int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Home Multi-Age Child Care")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_family_child_care_op", (int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Family Child Care")).Sum(x=>x.OperationalSpacespercategory) },
                                {"ofm_group_child_care_school_age_op",(int)licensecategorygroup.Where(type=>type.LicenceCategoryName.Contains("Group Child Care (School-Age)")).Sum(x=>x.OperationalSpacespercategory)},
                                {"ofm_star_total_percentage",Total_Star_Percentage},
                                {"ofm_school_age_spaces_ratio",School_Space_Ratio}
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
            }
            catch (Exception ex)
            {
                _logger.LogError(CustomLogEvent.Process, "Failed to Update Operational space and star %", ex.InnerException.Message);

            }
            return ProcessResult.Completed(ProcessId).SimpleProcessResult;
            //return await Task.FromResult(ProcessResult.Completed(ProcessId).SimpleProcessResult);
        }
    }
}
