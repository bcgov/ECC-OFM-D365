using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore;

/// <summary>
/// Interface to declare all methods used to access Dataverse API
/// </summary>
public interface IDataverseRepository
{
    /// <summary>
    /// This Method returns the Funding Application object to be used P800 process
    /// </summary>
    /// <param name="applicationId">The GUID of the ofm_application to retrieve from Web API</param>
    /// <returns></returns>
    Task<OFMApplication> GetFundingApplicationAsync(Guid applicationId);
    /// <summary>
    /// This Method returns the list of unprocessed Funding Applications to be used P800 process
    /// </summary>
    /// <param name="lastProcessedTime">The last timetstamp from which application needs to retrieved from web api</param>
    /// <returns></returns>
    Task<IEnumerable<OFMApplication>> GetUnprocessedApplicationsAsync(DateTime lastProcessedTime);
    /// <summary>
    /// This Method returns the list of modified Funding Applications to be used P800 process
    /// </summary>
    /// <param name="lastProcessedTime">The last timetstamp from which application needs to retrieved from web api</param>
    /// <returns></returns>
    Task<IEnumerable<OFMApplication>> GetModifiedApplicationsAsync(DateTime lastProcessedTime);
    /// <summary>
    /// This Method returns the list of Application score Parameters associated with an Aplication Calculator
    /// </summary>
    /// <param name="calculatorId">The GUID of ofm_application_score_calculator</param>
    /// <returns></returns>
    Task<IEnumerable<ScoreParameter>> GetScoreParametersAsync(Guid calculatorId);
    /// <summary>
    /// Get The Facility
    /// </summary>
    /// <param name="facilityId">GUID of the facility record</param>
    /// <returns></returns>
    Task<Facility> GetFacilityDataAsync(Guid facilityId);
    /// <summary>
    /// Get License Data for Total Operational Spaces and Max ChildCare spaces
    /// </summary>
    /// <param name="facilityId">GUID of the facility</param>
    /// <param name="submittedOn">Application submitted date</param>
    /// <returns></returns>
    Task<LicenseSpaces> GetLicenseDataAsync(Guid applicationId);
    /// <summary>
    /// Get the ACCB data
    /// </summary>
    /// <param name="postalCode">Postal code of the facilty</param>
    /// <param name="calculatorId">GUID of the application score calculator</param>
    /// <returns></returns>
    Task<ACCBIncomeIndicator> GetIncomeDataAsync(string postalCode, Guid calculatorId);


    Task<IEnumerable<ApplicationScoreValue>> GetApplicationScores(Guid ApplicationId);

    /// <summary>
    /// Get Approved Parent Fees of the facility
    /// </summary>
    /// <param name="facilityId">GUID of the facility</param>
    /// <returns></returns>
    Task<IEnumerable<ApprovedParentFee>> GetFeeDataAsync(Guid facilityId);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="city"></param>
    /// <param name="calculatorId">GUID of the application score calculator</param>
    /// <returns></returns>
    Task<PopulationCentre> GetPopulationDataAsync(string city, Guid calculatorId);
    /// <summary>
    /// Get the 40 Percetile Table for Parent Fees
    /// </summary>
    /// <param name="calculatorId">GUID of the application score calculator</param>
    /// <returns></returns>
    Task<IEnumerable<FortyPercentileThresholdFee>> GetThresholdDataAsync(Guid calculatorId);
    /// <summary>
    /// Get School District By Postal Code
    /// </summary>
    /// <param name="postalCode">postal code</param>
    /// <param name="calculatorId">GUID of the application score calculator</param>
    /// <returns></returns>
    Task<SchoolDistrict> GetSchoolDistrictDataAsync(string postalCode, Guid calculatorId);
    /// <summary>
    /// Create Application Score
    /// </summary>
    /// <param name="score"></param>
    /// <returns></returns>
    Task CreateScoreAsync(JsonObject score);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="scores"></param>
    /// <returns></returns>
    Task UpsertApplicationScoresBatchAsync(Dictionary<string, JsonObject> scores);
    /// <summary>
    /// Update Application table
    /// </summary>
    /// <param name="applicationId">Guid of ofm_application record</param>
    /// <param name="application"></param>
    /// <returns></returns>
    Task UpdateApplicationAsync(Guid applicationId, JsonObject application);
    /// <summary>
    /// Upsert the Application Scores
    /// </summary>
    /// <param name="Keys">Alternate Keys to upsert</param>
    /// <param name="applicationScore">application score Object</param>
    /// <returns></returns>
    Task UpsertApplicationScoreAsync(string Keys, JsonObject applicationScore);


    Task<(IEnumerable<ScoreCategory> categories, bool? moreData, string nextPage)> GetScoreCategories(Guid calculatorId, string? nextPage);
    Task<(IEnumerable<ACCBIncomeIndicator> accbs, bool? moreData, string nextPage)> GetACCBData(Guid calculatorId, string? nextPage);
    Task<(IEnumerable<SchoolDistrict> districts, bool? moreData, string nextPage)> GetSchoolDistricts(Guid calculatorId, string? nextPage);
    Task<(IEnumerable<PopulationCentre> populationCentres, bool? moreData, string nextPage)> GetPopulationCentres(Guid calculatorId, string? nextPage);
    Task CreateAccbIncomesBatchAsync(IEnumerable<JsonObject> accbs);
    Task<IEnumerable<ScoreCategory>> CreateScoreCategoriesBatchAsync(IEnumerable<JsonObject> categories);
    Task CreateSchoolDistrictsBatchAsync(IEnumerable<JsonObject> schoolDistricts, Guid calculatorId);
    Task<Guid> CreateScoreCalculatorAsync(JsonObject scoreCalculator);
    Task CreateScoreParametersBatchAsync(IEnumerable<JsonObject> parameters);
    Task CreateThresholdFeesBatchAsync(IEnumerable<JsonObject> fees);
    Task CreatePopulationCentresBatchAsync(IEnumerable<JsonObject> centres);

}

// Dataverse Repository Implementation
public class DataverseRepository(ID365AppUserService appUserService, ID365WebApiService d365WebApiService) : IDataverseRepository
{
    public async Task<OFMApplication> GetFundingApplicationAsync(Guid applicationId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.FundingApplicationQuery, applicationId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetFundingApplicationAsync({applicationId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return new OFMApplication(json);
    }
    public async Task<IEnumerable<ApplicationScoreValue>> GetApplicationScores(Guid applicationId)
    {
        
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ApplicationScoresQuery, applicationId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetApplicationScores({applicationId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new ApplicationScoreValue(v.AsObject()));
    }
    public async Task<IEnumerable<OFMApplication>> GetUnprocessedApplicationsAsync(DateTime lastTime)
    {
        var formattedTime = lastTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.UnprocessedApplicationsQuery, formattedTime)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetUnprocessedApplicationsAsync({lastTime}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new OFMApplication(v.AsObject()));
    }

    public async Task<IEnumerable<OFMApplication>> GetModifiedApplicationsAsync(DateTime lastProcessedTime)
    {
        var formattedTime = lastProcessedTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ModifiedApplicationsQuery, formattedTime)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetModifiedApplicationsAsync({lastProcessedTime}):HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new OFMApplication(v.AsObject()));
    }

    public async Task<IEnumerable<ScoreParameter>> GetScoreParametersAsync(Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ScoreParametersQuery, calculatorId)}", formatted: true, pageSize: 1000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetScoreParametersAsync({calculatorId}):HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new ScoreParameter(v.AsObject()));
    }

    public async Task<Facility> GetFacilityDataAsync(Guid facilityId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.facilityQuery, facilityId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetFacilityDataAsync(Guid {facilityId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return new Facility(json["value"]?.AsArray()?.First()?.AsObject());
    }

    public async Task<LicenseSpaces?> GetLicenseDataAsync(Guid applicationId)
    {

        var output = new JsonObject();
        var outputObjects = new List<JsonObject>();
        List<Task<HttpResponseMessage>> taks = new List<Task<HttpResponseMessage>>();
     
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.TotalOperationSpaces,applicationId)}"));
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.MaxChildSpaces, applicationId)}"));
        var responses = await Task.WhenAll(taks.ToArray());
        responses.ToList().ForEach(async r =>
        {
            if (!r.IsSuccessStatusCode)
            {
                var responseBody = await r.Content.ReadAsStringAsync();
                throw new Exception($"GetLicenseDataAsync(Guid {applicationId}):HTTP Failure: {responseBody}");
            }
            var json = await r.Content.ReadFromJsonAsync<JsonObject>();
            outputObjects.Add(json["value"].AsArray().First().AsObject());
        });

        output = output.MergeJsonObjects(outputObjects);
        return new LicenseSpaces(output.AsObject());
    }

    public async Task<ACCBIncomeIndicator?> GetIncomeDataAsync(string postalCode, Guid calculatorId)
    {
        postalCode = postalCode?.Replace(" ", string.Empty);
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.IncomeDataQuery, postalCode, calculatorId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetIncomeDataAsync(string {postalCode}, Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new ACCBIncomeIndicator(values.First().AsObject()) : null;
    }

    public async Task<IEnumerable<ApprovedParentFee>> GetFeeDataAsync(Guid facId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.AppovedFeeDataQuery, facId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetFeeDataAsync(Guid {facId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new ApprovedParentFee(v.AsObject()));
    }

    public async Task<IEnumerable<FortyPercentileThresholdFee>> GetThresholdDataAsync(Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ThresholdFeeDataQuery, calculatorId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetThresholdDataAsync(Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new FortyPercentileThresholdFee(v.AsObject()));
    }

    public async Task CreateScoreAsync(JsonObject score)
    {
        var jsonContent = new StringContent(score.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, $"{DataverseQueries.CreateScoreEndpoint}", JsonSerializer.Serialize(score));
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"CreateScoreAsync(JsonObject {score}): HTTP Failure: {responseBody}");
        }
    }

    public async Task UpdateApplicationAsync(Guid applicationId, JsonObject application)
    {
        var jsonContent = new StringContent(application.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.UpdateApplicationEndpoint, applicationId)}", JsonSerializer.Serialize(application));
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"UpdateApplicationAsync(Guid {applicationId}, JsonObject {application}): HTTP Failure: {responseBody}");
        }
    }
    public async Task UpsertApplicationScoreAsync(string Keys, JsonObject applicationScore)
    {

        var jsonContent = new StringContent(applicationScore.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.UpsertApplicationScoreEndpoint, Keys)}", JsonSerializer.Serialize(applicationScore));
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Upsert Application Score failed with request with Key = {Keys} and score Object = {applicationScore}  renponse {responseBody}");
        }

    }
    public async Task UpsertApplicationScoresBatchAsync(Dictionary<string, JsonObject> scores)
    {
        var batchRequests = scores.Select(x => new PatchRequest($"{string.Format(DataverseQueries.UpsertApplicationScoreEndpoint, x.Key)}", x.Value)).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Application Score failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }
    public async Task<PopulationCentre?> GetPopulationDataAsync(string city, Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.PopulationCentreQuery, city, calculatorId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetPopulationDataAsyncstring {city}, Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new PopulationCentre(values.First().AsObject()) : null;
    }
    public async Task<SchoolDistrict?> GetSchoolDistrictDataAsync(string postalCode, Guid calculatorId)
    {

        postalCode = postalCode?.Replace(" ", string.Empty);
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.SchoolDistrictQuery, postalCode, calculatorId)}", formatted: true, pageSize: 5000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetSchoolDistrictDataAsync(string {postalCode}, Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new SchoolDistrict(values.First().AsObject()) : null;
    }



    public async Task<(IEnumerable<SchoolDistrict> districts, bool? moreData, string nextPage)> GetSchoolDistricts(Guid calculatorId, string? nextPage)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, nextPage ?? $"{string.Format(DataverseQueries.AllSchoolDistrictQuery, calculatorId)}", formatted: true, pageSize: 1000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetSchoolDistricts(string Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return (values.Select(v => new SchoolDistrict(v.AsObject())), json.GetPropertyValue<string>("@odata.nextLink") != null, json.GetPropertyValue<string?>("@odata.nextLink"));
    }



    public async Task<(IEnumerable<PopulationCentre> populationCentres, bool? moreData, string nextPage)> GetPopulationCentres(Guid calculatorId, string? nextPage)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, nextPage ?? $"{string.Format(DataverseQueries.AllPopulationCentreQuery, calculatorId)}", formatted: true, pageSize: 1000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetPopulationCentres(Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return (values.Select(v => new PopulationCentre(v.AsObject())), json.GetPropertyValue<string>("@odata.nextLink") != null, json.GetPropertyValue<string?>("@odata.nextLink"));
    }



    public async Task CreateAccbIncomesBatchAsync(IEnumerable<JsonObject> accbs)
    {
        if (accbs == null || !accbs.Any())
            return;

        var batchRequests = accbs.Select(x => new CreateRequest("ofm_accbs", x)).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Application Score failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }
    public async Task<IEnumerable<ScoreCategory>> CreateScoreCategoriesBatchAsync(IEnumerable<JsonObject> categories)
    {
        if (categories == null || !categories.Any())
            return await Task.FromResult(new List<ScoreCategory>());

        var batchRequests = categories.Select(x =>
        {
            var req = new CreateRequest("ofm_application_score_categories", x);
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }).ToList<HttpRequestMessage>();

        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Application Score failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
        return response.Result.Select(x => new ScoreCategory(x));

    }
    public async Task CreateSchoolDistrictsBatchAsync(IEnumerable<JsonObject> schoolDistricts, Guid calcId)
    {
        if (schoolDistricts == null || !schoolDistricts.Any())
            return;
        var batchRequests = schoolDistricts.Select(x =>
        {
            return new CreateRequest("ofm_school_districts", x);
        }
        ).ToList<HttpRequestMessage>();
        
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Create of School District failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }
    public async Task<Guid> CreateScoreCalculatorAsync(JsonObject scoreCalculator)
    {

        var response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"ofm_application_score_calculators(ofm_name='{scoreCalculator.GetPropertyValue<string>("ofm_name") }')", scoreCalculator.ToString());
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Create of Application Score Calculator failed: Batch HTTP Failure: {await response.Content.ReadAsStringAsync()}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return json.GetPropertyValue<Guid>("ofm_application_score_calculatorid");
    }
    public async Task CreateScoreParametersBatchAsync(IEnumerable<JsonObject> parameters)
    {
        if (parameters == null || !parameters.Any())
            return;

        var batchRequests = parameters.Select(x =>
        {
            var req = new CreateRequest("ofm_application_score_parameteres", x);
            req.Headers.Add("Prefer", "return=representation");
            return req;
        }).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Application Score Parameter failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }
    public async Task CreateThresholdFeesBatchAsync(IEnumerable<JsonObject> fees)
    {
        if (fees == null || !fees.Any())
            return;
        var batchRequests = fees.Select(x => new CreateRequest("ofm_forty_percentile_fees", x)).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Threshold fees failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }
    public async Task CreatePopulationCentresBatchAsync(IEnumerable<JsonObject> centres)
    {
        if (centres == null || !centres.Any())
            return;

        var batchRequests = centres.Select(x => new CreateRequest("ofm_population_centres", x)).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, batchRequests, null);
        if (!response.CompletedWithNoErrors)
        {
            throw new Exception($"Upsert of Population Centres failed: Batch HTTP Failure: {string.Join(" | ", response.Errors)}");
        }
    }

    public async Task<(IEnumerable<ScoreCategory> categories, bool? moreData, string nextPage)> GetScoreCategories(Guid calculatorId, string? nextPage)
    {

        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, nextPage ?? $"{string.Format(DataverseQueries.AllScoreCategoryQuery, calculatorId)}", formatted: true, pageSize: 1000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetScoreCategories(Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return (values.Select(v => new ScoreCategory(v.AsObject())), json.GetPropertyValue<string?>("@odata.nextLink") != null, json.GetPropertyValue<string?>("@odata.nextLink"));
    }

    public async Task<(IEnumerable<ACCBIncomeIndicator> accbs, bool? moreData, string nextPage)> GetACCBData(Guid calculatorId, string? nextPage)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, nextPage ?? $"{string.Format(DataverseQueries.AllACCBDataQuery, calculatorId)}", pageSize: 1000);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"GetScoreCategories(Guid {calculatorId}): HTTP Failure: {responseBody}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return (values.Select(v => new ACCBIncomeIndicator(v.AsObject())), json.GetPropertyValue<string?>("@odata.nextLink") != null, json.GetPropertyValue<string?>("@odata.nextLink"));
    }
}