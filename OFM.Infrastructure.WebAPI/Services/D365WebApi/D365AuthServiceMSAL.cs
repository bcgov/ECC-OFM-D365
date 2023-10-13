using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;
using System.Net.Http.Headers;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

/// <summary>
/// New and preferred Authentication Service with MSAL library
/// </summary>
public class D365AuthServiceMSAL : ID365AuthenticationService
{
    private readonly D365AuthSettings _authSettings;
    private readonly ID365TokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;

    public D365AuthServiceMSAL(IOptions<D365AuthSettings> authSettings, ID365TokenService tokenService, IHttpClientFactory factory)
    {
        _authSettings = authSettings.Value;
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _httpClientFactory = factory;
    }

    public async Task<HttpClient> GetHttpClientAsync(D365RequestType requestType, AZAppUser spn)
    {
        var accessToken = await _tokenService.FetchAccessToken(_authSettings.BaseUrl, spn);
        var baseAddress = requestType switch
        {
            D365RequestType.Search => $"{_authSettings.BaseUrl}api/search/{_authSettings.SearchVersion}/query",
            D365RequestType.Batch => $"{_authSettings.BaseUrl}api/data/{_authSettings.ApiVersion}/$batch",
            _ => _authSettings.WebApiUrl,
        };
        var client = _httpClientFactory.CreateClient(_authSettings.HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.BaseAddress = new Uri(baseAddress);

        return client;
    }
}