using Newtonsoft.Json.Linq;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Handlers;
using OFM.Infrastructure.WebAPI.Models.ApplicationScore;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ApplicationScore;
// Centralized Dataverse Queries

public interface IDataverseRepository
{
    Task<FundingApplication> GetFundingApplicationAsync(Guid applicationId);
    Task<IEnumerable<FundingApplication>> GetUnprocessedApplicationsAsync();
    Task<IEnumerable<FundingApplication>> GetModifiedApplicationsAsync(DateTime lastProcessedTime);
    Task<IEnumerable<ScoreParameter>> GetScoreParametersAsync(Guid calculatorId);
    Task<Facility> GetFacilityDataAsync(Guid schoolId);
    Task<LicenseSpaces> GetLicenseDataAsync(Guid schoolId);
    Task<ACCBIncomeIndicator> GetIncomeDataAsync(string postalCode, Guid calculatorId);
    Task<IEnumerable<ApprovedParentFee>> GetFeeDataAsync(Guid facilityId);
    Task<PopulationCentre> GetPopulationDataAsync(string city);
    Task<IEnumerable<FortyPercentileThresholdFee>> GetThresholdDataAsync(string postalCode, Guid calculatorId);
    Task<SchoolDistrict> GetSchoolDistrictDataAsync(string postalCode);
    Task CreateScoreAsync(JsonObject score);
    Task UpdateApplicationAsync(Guid applicationId, JsonObject application);
    Task UpsertApplicationScoreAsync(string Keys, JsonObject application);
}

// Dataverse Repository Implementation
public class DataverseRepository(ID365AppUserService appUserService, ID365WebApiService d365WebApiService) : IDataverseRepository
{
    public async Task<FundingApplication> GetFundingApplicationAsync(Guid applicationId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.FundingApplicationQuery, applicationId)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return new FundingApplication(json);
    }

    public async Task<IEnumerable<FundingApplication>> GetUnprocessedApplicationsAsync()
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{DataverseQueries.UnprocessedApplicationsQuery}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new FundingApplication(v.AsObject()));
    }

    public async Task<IEnumerable<FundingApplication>> GetModifiedApplicationsAsync(DateTime lastProcessedTime)
    {
        var formattedTime = lastProcessedTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ModifiedApplicationsQuery, formattedTime)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new FundingApplication(v.AsObject()));
    }

    public async Task<IEnumerable<ScoreParameter>> GetScoreParametersAsync(Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ScoreParametersQuery, calculatorId)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Select(v => new ScoreParameter(v.AsObject()));
    }

    public async Task<Facility> GetFacilityDataAsync(Guid schoolId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.facilityQuery, schoolId)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return new Facility(json);
    }

    public async Task<LicenseSpaces?> GetLicenseDataAsync(Guid schoolId)
    {
        var output = new JsonObject();
        var outputObjects = new List<JsonObject>();
        List<Task<HttpResponseMessage>> taks = new List<Task<HttpResponseMessage>>();
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.TotalOperationSpaces, schoolId)}"));
        taks.Add(d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.MaxChildSpaces, schoolId)}"));
        var responses = await Task.WhenAll(taks.ToArray());
        responses.ToList().ForEach(async x => { 
            x.EnsureSuccessStatusCode(); 
            var json = await x.Content.ReadFromJsonAsync<JsonObject>();
            outputObjects.Add(json["value"].AsArray().First().AsObject());
        });

        output = output.MergeJsonObjects(outputObjects);
        return new LicenseSpaces(output.AsObject());
    }

    public async Task<ACCBIncomeIndicator?> GetIncomeDataAsync(string postalCode, Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.IncomeDataQuery, postalCode)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new ACCBIncomeIndicator(values.First().AsObject()) : null;
    }

    public async Task<IEnumerable<ApprovedParentFee>> GetFeeDataAsync(Guid facId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.AppovedFeeDataQuery, facId)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();       
        return values.Select(v => new ApprovedParentFee(v.AsObject()));
    }

    public async Task<IEnumerable<FortyPercentileThresholdFee>> GetThresholdDataAsync(string postalCode, Guid calculatorId)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.ThresholdFeeDataQuery, calculatorId, postalCode)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();        
        return values.Select(v => new FortyPercentileThresholdFee(v.AsObject()));
    }






    public async Task CreateScoreAsync(JsonObject score)
    {
        var jsonContent = new StringContent(score.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, $"{DataverseQueries.CreateScoreEndpoint}", JsonSerializer.Serialize(score));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateApplicationAsync(Guid applicationId, JsonObject application)
    {
        var jsonContent = new StringContent(application.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.UpdateApplicationEndpoint, applicationId)}", JsonSerializer.Serialize(application));
        response.EnsureSuccessStatusCode();
    }
    public async Task UpsertApplicationScoreAsync(string Keys, JsonObject applicationScore)
    {
        var jsonContent = new StringContent(applicationScore.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.UpsertApplicationScoreEndpoint, Keys)}", JsonSerializer.Serialize(applicationScore));
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"HTTP Failure: {responseBody}");
        }
    }

    public async Task<PopulationCentre?> GetPopulationDataAsync(string city)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.PopulationCentreQuery, city)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new PopulationCentre(values.First().AsObject()) : null;
    }
    public async Task<SchoolDistrict?> GetSchoolDistrictDataAsync(string postalCode)
    {
        var response = await d365WebApiService.SendRetrieveRequestAsync(appUserService.AZSystemAppUser, $"{string.Format(DataverseQueries.SchoolDistrictQuery, postalCode)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var values = json["value"]?.AsArray() ?? new JsonArray();
        return values.Any() ? new SchoolDistrict(values.First().AsObject()) : null;
    }
    
}