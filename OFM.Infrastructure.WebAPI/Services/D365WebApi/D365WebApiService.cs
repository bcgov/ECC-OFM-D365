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

    public async Task<HttpResponseMessage> SendRetrieveRequestAsync(AZAppUser spn, string requestUrl, bool formatted = false, int pageSize = 50)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("Prefer", "odata.maxpagesize=" + pageSize.ToString());
        if (formatted)
            request.Headers.Add("Prefer", "odata.include-annotations=OData.Community.Display.V1.FormattedValue");

        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.CRUD, spn);

        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendCreateRequestAsync(AZAppUser spn, string entitySetName, string requestBody)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, entitySetName)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendUpdateRequestAsync(AZAppUser spn, string requestUrl, string body)
    {
        var message = new HttpRequestMessage(HttpMethod.Patch, requestUrl);
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.CRUD, spn);
        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendDeleteRequestAsync(AZAppUser spn, string endPoint)
    {
        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.CRUD, spn);

        return await client.DeleteAsync(endPoint);
    }

    public async Task<HttpResponseMessage> SendSearchRequestAsync(AZAppUser spn, string body)
    {
        var message = new HttpRequestMessage()
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Post
        };

        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.Search, spn);

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

        var client = await _authenticationService.GetHttpClientAsync(D365RequestType.Batch, spn);

        return await client.SendAsync(request);
    }
}