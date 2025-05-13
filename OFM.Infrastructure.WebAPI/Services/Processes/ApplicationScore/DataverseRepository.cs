using Newtonsoft.Json.Linq;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net.Http;
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
    Task<LicenseSpaces> GetLicenseDataAsync(Guid facilityId, DateTime? submittedOn);
    /// <summary>
    /// Get the ACCB data
    /// </summary>
    /// <param name="postalCode">Postal code of the facilty</param>
    /// <param name="calculatorId">GUID of the application score calculator</param>
    /// <returns></returns>
    Task<ACCBIncomeIndicator> GetIncomeDataAsync(string postalCode, Guid calculatorId);
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
    Task<PopulationCentre> GetPopulationDataAsync(string city , Guid calculatorId);
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
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ScoreParametersQuery, calculatorId)}", formatted: true, pageSize: 5000);
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
        return new Facility(json);
    }

    public async Task<LicenseSpaces?> GetLicenseDataAsync(Guid facilityId, DateTime? submittedOn)
    {

        var output = new JsonObject();
        var outputObjects = new List<JsonObject>();
        List<Task<HttpResponseMessage>> taks = new List<Task<HttpResponseMessage>>();
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.TotalOperationSpaces, facilityId, submittedOn)}"));
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.MaxChildSpaces, facilityId, submittedOn)}"));
        var responses = await Task.WhenAll(taks.ToArray());
        responses.ToList().ForEach(async r => {
            if (!r.IsSuccessStatusCode)
            {
                var responseBody = await r.Content.ReadAsStringAsync();
                throw new Exception($"GetLicenseDataAsync(Guid {facilityId}, DateTime? {submittedOn}):HTTP Failure: {responseBody}");
            }
            var json = await r.Content.ReadFromJsonAsync<JsonObject>();
            outputObjects.Add(json["value"].AsArray().First().AsObject());
        });

        output = output.MergeJsonObjects(outputObjects);
        return new LicenseSpaces(output.AsObject());        
    }

    public async Task<ACCBIncomeIndicator?> GetIncomeDataAsync(string postalCode, Guid calculatorId)
    {
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
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ThresholdFeeDataQuery, calculatorId)}",formatted: true, pageSize: 5000);
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
                throw new Exception($"Upsert Application Score failed with request with Key = { Keys } and score Object = { applicationScore}  renponse {responseBody}");
            }
        
    }
    public async Task UpsertApplicationScoresBatchAsync(Dictionary<string, JsonObject> scores)
    {
        var batchRequests = scores.Select(x => new PatchRequest($"{string.Format(DataverseQueries.UpsertApplicationScoreEndpoint, x.Key)}", x.Value)).ToList<HttpRequestMessage>();
        var response = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser,batchRequests, null);
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
    
    
}