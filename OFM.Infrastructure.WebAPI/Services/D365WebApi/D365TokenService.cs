using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;
using OFM.Infrastructure.WebAPI.Caching;
using OFM.Infrastructure.WebAPI.Models;

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