using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using System.Net.Http.Headers;
using System.Text;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public class D365WebAPIService : ID365WebApiService
{
    private readonly ID365AuthenticationService _authenticationService;

    public D365WebAPIService(ID365AuthenticationService authenticationService)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    public async Task<HttpResponseMessage> SendRetrieveRequestAsync(AZAppUser spn, string requestUri, bool formatted = false, int pageSize = 50)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("Prefer", "odata.maxpagesize=" + pageSize.ToString());
        if (formatted)
            request.Headers.Add("Prefer", "odata.include-annotations=OData.Community.Display.V1.FormattedValue");

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendCreateRequestAsync(AZAppUser spn, string entitySetName, string requestBody)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, entitySetName)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendPatchRequestAsync(AZAppUser spn, string requestUri, string requestBody)
    {
        var message = new HttpRequestMessage(HttpMethod.Patch, requestUri);
        message.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);
        return await client.SendAsync(message);
    }
    
    public async Task<HttpResponseMessage> SendDeleteRequestAsync(AZAppUser spn, string requestUri)
    {
        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.DeleteAsync(requestUri);
    }

    public async Task<HttpResponseMessage> SendSearchRequestAsync(AZAppUser spn, string body)
    {
        var message = new HttpRequestMessage()
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Post
        };

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Search, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendBatchMessageAsync(AZAppUser spn, string body, string batchName, Guid? callerObjectId)
    {
        HttpRequestMessage request = new()
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Post
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/mixed;boundary=" + batchName);

        if (callerObjectId != null)
            request.Headers.Add("CallerObjectId", callerObjectId.ToString());

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Batch, spn);

        return await client.SendAsync(request);
    }
}