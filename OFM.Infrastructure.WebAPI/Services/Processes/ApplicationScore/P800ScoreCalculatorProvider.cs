using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore
{
    public class P800ScoreCalculatorProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, IDataverseRepository dataverseRepository, ILoggerFactory loggerFactory, TimeProvider timeProvider, IOptionsSnapshot<NotificationSettings> notificationSettings) : ID365ProcessProvider
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly TimeProvider _timeProvider = timeProvider;
        private Guid schoolId;

        public short ProcessId => Setup.Process.ApplicationScore.ScoreCalculatorId;
        public string ProcessName => Setup.Process.ApplicationScore.ScoreCalculatorName;

        private string RetrieveApplicationCalculator
        {
            get
            {
                // Note: FetchXMl limit is 5000 records per request
                var fetchXml = @$"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                                  <entity name=""ofm_application_score_calculator"">
                                    <attribute name=""ofm_application_score_calculatorid"" />
                                    <attribute name=""ofm_name"" />
                                    <attribute name=""createdon"" />
                                    <order attribute=""ofm_name"" descending=""false"" />
                                    <link-entity name=""ofm_intake"" from=""ofm_application_score_calculator"" to=""ofm_application_score_calculatorid"" link-type=""inner"" alias=""intake"">
                                    <attribute name=""ofm_start_date"" />
                                    <attribute name=""ofm_end_date"" />
                                      
                                    </link-entity>
                                  </entity>
                                </fetch>";

                var requestUri = $"""
                               ofm_application_score_calculators?fetchXml={fetchXml.CleanCRLF()}
                               """;
                return requestUri.CleanCRLF();
            }
        }

        private IComparisonHandler comparisonChain
        {
            get
            {
                IComparisonHandler handler = new EqualHandler();
                handler
                .SetNext(new GreaterThanHandler())
                .SetNext(new LessThanHandler())
                .SetNext(new GreaterThanOrEqualHandler())
                .SetNext(new LessThanOrEqualHandler())
                .SetNext(new BetweenHandler());
                return handler;
            }
        }



        private async Task<(Guid appId, bool IsSuccess, string Message)> ProcessSingleApplication(Guid applicationId)
        {
            try
            {

                var application = await dataverseRepository.GetFundingApplicationAsync(applicationId);
                var applicationCalculator = await GetDataAsync();
                if (String.IsNullOrEmpty(applicationCalculator.Data.ToString()))
                {
                    throw new Exception("Failed to query records: No ofm_application_score_calculator exists in CRM");
                }
                var calculator = applicationCalculator.Data.AsArray().Where(c => c["intake.ofm_start_date"].GetValue<DateTime>() <= application?.SubmittedOn && c["intake.ofm_end_date"].GetValue<DateTime>() >= application?.SubmittedOn)?.FirstOrDefault();
                if (calculator == null)
                    throw new Exception(string.Format("Failed to query records: No ofm_application_score_calculator exists for application with id = {0} with submittedOn = {1}", applicationId, application.SubmittedOn));
                var calculatorId = Guid.Parse(calculator["ofm_application_score_calculatorid"].ToString());
                if (application == null) return (applicationId, false, "ofm_application not found.");
                var facilityId = application.FacilityId ?? Guid.Empty;
                if (facilityId == Guid.Empty) return (applicationId, false, "facility ID not found.");

                var scoreParameters = await dataverseRepository.GetScoreParametersAsync(calculatorId);
                if (!scoreParameters.Any()) return (applicationId, false, "No score parameters found.");

                var facilityData = await dataverseRepository.GetFacilityDataAsync(facilityId);
                var licenseData = await dataverseRepository.GetLicenseDataAsync(facilityId);
                var incomeData = await dataverseRepository.GetIncomeDataAsync(facilityData.PostalCode, calculatorId);
                var feeData = await dataverseRepository.GetFeeDataAsync(facilityId);
                var thresholdData = await dataverseRepository.GetThresholdDataAsync(facilityData.PostalCode, calculatorId);
                var populationData = await dataverseRepository.GetPopulationDataAsync(facilityData.City);
                var schoolDistrictData = await dataverseRepository.GetSchoolDistrictDataAsync(facilityData.PostalCode);
                var scores = new List<JsonObject>();
                var groupedParameters = scoreParameters.GroupBy(p => p.CategoryName);
                foreach (var categoryGroup in groupedParameters)
                {
                    var categoryName = categoryGroup.Key;
                    _logger.LogInformation($"Processing category: {categoryName} for application {applicationId}");
                    JsonObject scoreData = null;
                    foreach (var param in categoryGroup)
                    {
                        var key = param.Key;
                        var operatorValue = param.ComparisonOperator ?? 0;
                        var operatorStr = OperatorMapper.MapOperator(operatorValue);
                        var comparisonValue = param.ComparisonValue;
                        var scoreValue = param.Score ?? 0;
                        var scoreCategoryId = param.ScoreCategoryId;
                        var strategy = ScoreStrategyFactory.CreateStrategy(categoryName, operatorValue, comparisonValue);
                        // Evaluate the strategy
                        bool isMatch = false;
                        string? reason = default;
                        try
                        {
                            _logger.LogInformation($"Evaluating strategy for category: {categoryName}, key: {key}");
                            isMatch = await strategy.EvaluateAsync(comparisonChain, param, application, facilityData, licenseData, incomeData, feeData, thresholdData, populationData, schoolDistrictData);
                            _logger.LogInformation($"Strategy evaluation result: {isMatch}");
                        }
                        catch (ArgumentException ex)
                        {
                            _logger.LogError(CustomLogEvent.Process, $"Error calculating score for category {categoryName} in application {applicationId}: {ex.Message}");
                            reason = ex.Message;
                        }

                        // Create the score data object
                        scoreData = new JsonObject
                        {
                            ["ofm_category"] = categoryName,
                            ["ofm_application@odata.bind"] = $"ofm_applications({applicationId})",
                            ["ofm_application@odata.bind"] = $"ofm_applications({applicationId})",
                            ["ofm_application_score_category@odata.bind"] = $"ofm_application_score_categories({scoreCategoryId})",
                            ["ofm_score_processing_status"] = string.IsNullOrEmpty(reason) ? 2 : 3,
                            ["ofm_score_lastprocessed"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        };

                        if (isMatch)
                        {
                            _logger.LogInformation(CustomLogEvent.Process, "Match found for score paramter for category {category}", categoryName);
                            scoreData["ofm_calculated_score"] = scoreValue;
                            _logger.LogInformation($"Score generated for category: {categoryName} with score: {scoreValue}");
                            break; // Stop after the first match in this category
                        }
                        else
                        {
                            scoreData["ofm_calculated_score"] = 0;
                            //   scoreData["ofm_score_remarks"] = $"Condition not met for {categoryName} {operatorStr} {comparisonValue} : {reason}";
                            scoreData["ofm_score_remarks"] = $"{reason}";
                        }
                    }

                    // If no match was found, use the last evaluated score data (with reason)
                    if (scoreData != null)
                    {
                        scores.Add(scoreData);
                    }
                }

                foreach (var score in scores)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "Updating calculated score for application {applicationId} for category {category}", applicationId, score.GetPropertyValue<string>("ofm_category"));
                    await dataverseRepository.UpsertApplicationScoreAsync($"_ofm_application_value={applicationId},_ofm_application_score_category_value={score["ofm_application_score_category@odata.bind"]?.ToString().Replace("ofm_application_score_categories", "").Replace("(", "").Replace(")", "")}", score);
                    _logger.LogInformation(CustomLogEvent.Process, "Update completed for calculated score for application {applicationId} for category {category}", applicationId, score.GetPropertyValue<string>("ofm_category"));
                }

                return (applicationId, true, "Scores calculated and saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(CustomLogEvent.Process, $"Error calculating scores for application {applicationId}: {ex.Message}");
                return (applicationId, false, $"Error calculating scores: {ex}");
            }
        }



        public async Task<ProcessData> GetDataAsync()
        {
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetSupplementaryApplicationDataAsync");

            var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, RetrieveApplicationCalculator, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query supplementary applications to update with the server error {responseBody}", responseBody.CleanLog());

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No supplementary applications found with query {requestUri}", RetrieveApplicationCalculator.CleanLog());
                }
                d365Result = currentValue!;
            }

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

            return await Task.FromResult(new ProcessData(d365Result));
        }

        public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
        {
            var startTime = _timeProvider.GetTimestamp();
            var applicationsToProcess = new List<FundingApplication>();
            if (processParams.LastScoreCalculationTimeStamp.HasValue && processParams?.LastScoreCalculationTimeStamp.Value > new DateTime(1900, 1, 1, 0, 0, 0))
            {
                var unprocessedApplications = await dataverseRepository.GetUnprocessedApplicationsAsync();
                var modifiedApplications = await dataverseRepository.GetModifiedApplicationsAsync(processParams.LastScoreCalculationTimeStamp.Value);
                applicationsToProcess = unprocessedApplications.Concat(modifiedApplications).DistinctBy(app => app.Id).ToList();
            }
            if (!processParams.LastScoreCalculationTimeStamp.HasValue && processParams.Application != null && processParams.Application.applicationId != null)
            {

                var app = await dataverseRepository.GetFundingApplicationAsync(processParams.Application.applicationId.Value);
                applicationsToProcess.Add(app);


            }
            var tasks = applicationsToProcess.Select(app => ProcessSingleApplication(app.Id)).ToList();
            var results = await Task.WhenAll(tasks);
            foreach (var app in applicationsToProcess)
            {
                var appId = app.Id;
                var result = results.FirstOrDefault(r => r.appId == appId);
                var updateData = new JsonObject
                {
                    ["ofm_score_processing_status"] = result.IsSuccess ? 2 : 3,
                    ["ofm_score_lastprocessed"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                await dataverseRepository.UpdateApplicationAsync(appId, updateData);

            }
            var processingResult = ProcessResult.Success(ProcessId, applicationsToProcess!.Count);
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(processingResult, serializeOptions);

            var endTime = _timeProvider.GetTimestamp();
            _logger.LogInformation(CustomLogEvent.Process, "Application Score Calculation finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, json);

            return processingResult.SimpleProcessResult;
        }
    }
}
