using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;
using OFM.Infrastructure.WebAPI.Caching;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public class D365TokenService : ID365TokenService
{
    private readonly IDistributedCache<D365Token> _cache;

    public D365TokenService(IDistributedCache<D365Token> cache)
    {
        _cache = cache;
    }
    public async Task<string> FetchAccessToken(string baseUrl, AZAppUser azSPN)
    {
        var cacheKey = $"D365Token_{azSPN.Id}";
        var (isCached, d365Token) = await _cache.TryGetValueAsync(cacheKey);

        if (!isCached)
        {
            string[] scopes = { baseUrl + "/.default" };
            string authority = $"https://login.microsoftonline.com/{azSPN.TenantId}";

            var clientApp = ConfidentialClientApplicationBuilder.Create(clientId: azSPN.ClientId)
                                                      .WithClientSecret(clientSecret: azSPN.ClientSecret)
                                                      .WithAuthority(new Uri(authority))
                                                      .Build();

            var builder = clientApp.AcquireTokenForClient(scopes);
            var acquiredResult = await builder.ExecuteAsync();

            d365Token = new D365Token { Value = acquiredResult.AccessToken, ExpiresOn = acquiredResult.ExpiresOn };
            await _cache.SetAsync(cacheKey, d365Token, d365Token.ExpiresInMinutes);
        }

        return d365Token?.Value ?? throw new NullReferenceException(nameof(D365Token));
    }
}

public record D365Token
{
    public required string Value { get; set; }
    public required DateTimeOffset ExpiresOn { get; set; }
    public double ExpiresInSeconds
    {
        get
        {
            var endDate = ExpiresOn.ToUniversalTime();
            var startDate = DateTime.UtcNow;

            return (endDate - startDate).TotalSeconds - 60; // expires 1 minute early
        }
    }
    public Int32 ExpiresInMinutes
    {
        get
        {
            var endDate = ExpiresOn.ToUniversalTime();
            var startDate = DateTime.UtcNow;

            return Convert.ToInt32((endDate - startDate).TotalMinutes) - 1; // expires 1 minute early
        }
    }
}

public record D365Data
{
    public required JsonNode Data { get; set; }
    public required DateTimeOffset ExpiresOn { get; set; }
    public double ExpiresInSeconds
    {
        get
        {
            var endDate = ExpiresOn.ToUniversalTime();
            var startDate = DateTime.UtcNow;

            return (endDate - startDate).TotalSeconds - 60; // expires 1 minute early
        }
    }
    public Int32 ExpiresInMinutes
    {
        get
        {
            var endDate = ExpiresOn.ToUniversalTime();
            var startDate = DateTime.UtcNow;

            return Convert.ToInt32((endDate - startDate).TotalMinutes) - 1; // expires 1 minute early
        }
    }
}

public interface ID365DataService
{
    D365Data FetchData(string requestUrl, string cacheKey);
    Task<D365Data> FetchDataAsync(string requestUrl, string cacheKey);
}

public class D365DataService : ID365DataService
{
    private readonly IDistributedCache<D365Data> _cache;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;

    public D365DataService(IDistributedCache<D365Data> cache, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        _cache = cache;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
    }

    public D365Data FetchData(string requestUrl, string cacheKey)
    {
        throw new NotImplementedException();
    }

    public async Task<D365Data> FetchDataAsync(string requestUrl, string key)
    {
        var cacheKey = $"D365Data_{key}";
        var (isCached, d365Data) = await _cache.TryGetValueAsync(cacheKey);

        if (!isCached)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, requestUrl, isProcess: true);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                //_logger.LogError(CustomLogEvent.Process, "Failed to query members on the contact list with the server error {responseBody}", responseBody.CleanLog());

                //return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();
            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    //_logger.LogInformation(CustomLogEvent.Process, "No records found");
                }
                d365Result = currentValue!;
            }
            //await _cache.SetAsync(cacheKey, jsonObject?.GetValue<dynamic>(), TimeSpan.FromHours(12).Hours);
            d365Data = new D365Data { Data = d365Result, ExpiresOn = (new DateTimeOffset(DateTime.Now)) };

            await _cache.SetAsync(cacheKey, d365Data, TimeSpan.FromHours(12).Hours);
        }
        return d365Data ?? throw new NullReferenceException(nameof(D365Data));

    }
}