using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore
{
    public class P805CreateScoreCalculatorVersionProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IDataverseRepository dataverseRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider, IOptionsSnapshot<NotificationSettings> notificationSettings) : ID365ProcessProvider
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private ProcessParameter? _processParams;
        public short ProcessId => Setup.Process.ApplicationScore.CloneScoreCalculatorId;
        public string ProcessName => Setup.Process.ApplicationScore.CloneScoreCalculatorName;

        private string RetrieveApplicationCalculator
        {
            get
            {

                var fetchXml = @$"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                                  <entity name=""ofm_application_score_calculator_version"">
                                    <all-attributes />
                                    <order attribute=""ofm_name"" descending=""false"" />
                                    <filter>
                                    <condition attribute=""statuscode"" operator=""eq"" value=""1"" />
                                    <condition attribute=""ofm_application_score_calculator_versionid"" operator=""eq"" value=""{ _processParams?.ScoreCalculatorVersionId}""/>
                                    </filter>                                                                          
                                    </link-entity>
                                  </entity>
                                </fetch>";

                var requestUri = $"""
                               ofm_application_score_calculator_versions?fetchXml={fetchXml.CleanCRLF()}
                               """;
                return requestUri.CleanCRLF();
            }
        }





        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetDataAsync");

            var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, RetrieveApplicationCalculator, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query calculator with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No calculator found with query {requestUri}", RetrieveApplicationCalculator.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var processingResult = ProcessResult.Completed(CustomLogEvent.Process);
            _processParams = processParams;
            var startTime = _timeProvider.GetTimestamp();
            var applicationsToProcess = new List<OFMApplication>();
            try
            {
                if (!processParams.ScoreCalculatorVersionId.HasValue)
                    throw new ArgumentException("No Score Calculator Version ID is passed");

                var processData = await GetDataAsync();

                var calcVersion = processData.Data?.AsArray()?.First()?.AsObject();
                var originalCalcId = calcVersion.GetPropertyValue<Guid>("_ofm_application_score_calculator_value");

                //Calculator
                var calc = new JsonObject();
                calc["ofm_name"] = calcVersion.GetPropertyValue<string>("ofm_name");
                var calcId = await dataverseRepository.CreateScoreCalculatorAsync(calc);
                //Categories
                var scoreCategories = await dataverseRepository.GetScoreCategories(originalCalcId, null);
                var categories = await dataverseRepository.CreateScoreCategoriesBatchAsync(scoreCategories.categories?.Select(x => x.Clone(calcId)));

                //Parameters
                var scoreParams = await dataverseRepository.GetScoreParametersAsync(originalCalcId);
                await dataverseRepository.CreateScoreParametersBatchAsync(scoreParams?.Select(x => x.Clone(calcId, categories.Where(y => y.CategoryName == x.CategoryName)?.First()?.CategoryId.Value)));

                //ACCB
                if (calcVersion.GetPropertyValue<bool>("ofm_copy_accb"))
                {                   
                    var accbs = await dataverseRepository.GetACCBData(originalCalcId, null);
                    await dataverseRepository.CreateAccbIncomesBatchAsync(accbs.accbs?.Select(x => x.Clone(calcId)));
                    while (accbs.moreData.Value)
                    {
                        accbs = await dataverseRepository.GetACCBData(originalCalcId, accbs.nextPage);
                        await dataverseRepository.CreateAccbIncomesBatchAsync(accbs.accbs?.Select(x => x.Clone(calcId)));
                    }
                }

                //40P Fees
                if (calcVersion.GetPropertyValue<bool>("ofm_copy_forty_percentile_fees"))
                {
                    var thresholdFees = await dataverseRepository.GetThresholdDataAsync(originalCalcId);
                    await dataverseRepository.CreateThresholdFeesBatchAsync(thresholdFees?.Select(x => x.Clone(calcId)));
                }
                //Population Centres
                if (calcVersion.GetPropertyValue<bool>("ofm_copy_population_centres"))
                {
                    var centres = await dataverseRepository.GetPopulationCentres(originalCalcId, null);
                    await dataverseRepository.CreatePopulationCentresBatchAsync(centres.populationCentres?.Select(x => x.Clone(calcId)));
                    while (centres.moreData.Value)
                    {
                        centres = await dataverseRepository.GetPopulationCentres(originalCalcId, centres.nextPage);
                        await dataverseRepository.CreatePopulationCentresBatchAsync(centres.populationCentres?.Select(x => x.Clone(calcId)));
                    }
                }

                //SchoolDistricts
                if (calcVersion.GetPropertyValue<bool>("ofm_copy_school_district"))
                {
                    var districts = await dataverseRepository.GetSchoolDistricts(originalCalcId, null);
                    await dataverseRepository.AssociateSchoolDistrictsBatchAsync(districts.districts?.Select(x => x.Clone(calcId)), calcId);
                    while (districts.moreData.Value)
                    {
                        districts = await dataverseRepository.GetSchoolDistricts(originalCalcId, districts.nextPage);
                        await dataverseRepository.AssociateSchoolDistrictsBatchAsync(districts.districts?.Select(x => x.Clone(calcId)), calcId);
                    }
                }

                JsonObject jsonSuccess = new JsonObject();
                jsonSuccess["statuscode"] = 506580002;
                jsonSuccess["ofm_error_details"] = null;
                jsonSuccess["ofm_completed_on"] = DateTime.Now.ToLongDateString();
                await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"ofm_application_score_calculator_versions({processParams.ScoreCalculatorVersionId})", jsonSuccess.ToString());
                processingResult = ProcessResult.Success(CustomLogEvent.Process,1);
                _logger.LogInformation(CustomLogEvent.Process, "Successfully created application score calculator version with process Params {params}", processParams);

            }
            catch (Exception ex)
            {
                JsonObject jsonError = new JsonObject();
                jsonError["ofm_error_details"] = ex.ToString();
                jsonError["statuscode"] = 506580003;
                jsonError["ofm_completed_on"] = DateTime.Now.ToLongDateString();
                await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"ofm_application_score_calculator_versions({ processParams.ScoreCalculatorVersionId})", jsonError.ToString());
                processingResult = ProcessResult.Failure(CustomLogEvent.Process, new List<string> { string.Format($"Error processsing application score calculation version with process Params {0}: {1}", JsonSerializer.Serialize(processParams), ex) }, 0, applicationsToProcess!.Count);
                _logger.LogError(CustomLogEvent.Process, "Error processsing application score calculation version with process Params {params}: {ex}", processParams, ex);
            }
            finally
            {
                var serializeOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(processingResult, serializeOptions);
                var endTime = _timeProvider.GetTimestamp();
                _logger.LogInformation(CustomLogEvent.Process, "Create Application Score Calculator Version finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);
            }
            return processingResult.SimpleProcessResult;
        }
    }
}
