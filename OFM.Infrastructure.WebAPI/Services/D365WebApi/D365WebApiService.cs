using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using System.Net.Http.Headers;
using System.Text;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public class D365WebAPIService : ID365WebApiService
{
    private readonly ID365AuthenticationService _authenticationService;
    private readonly D365AuthSettings _d365AuthSettings;

    public D365WebAPIService(ID365AuthenticationService authenticationService, IOptions<D365AuthSettings> d365AuthSettings)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _d365AuthSettings = d365AuthSettings.Value;
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
        message.Headers.Add("Prefer", "return=representation");

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendPatchRequestAsync(AZAppUser spn, string requestUri, string requestBody)
    {
        var message = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        message.Headers.Add("Prefer", "return=representation");

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendDocumentRequestAsync(AZAppUser spn, string entityNameSet,Guid id, Byte[] data, string fileName)
    {
        var request = new UploadFileRequest(new EntityReference(entityNameSet, id), columnName: "ofm_file", data, fileName);
        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
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

    public async Task<HttpResponseMessage> SendBatchMessageAsync(AZAppUser spn, List<HttpRequestMessage> requestMessages, Guid? callerObjectId)
    {
        BatchRequest batchRequest = new(_d365AuthSettings)
        {
            Requests = requestMessages,
            ContinueOnError = true
        };

        var client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Batch, spn);

        return await client.SendAsync(batchRequest);
    }
}