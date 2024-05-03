using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public class D365WebAPIService : ID365WebApiService
{
    private readonly ILogger _logger;
    private readonly ID365AuthenticationService _authenticationService;
    private readonly D365AuthSettings _d365AuthSettings;

    public D365WebAPIService(ILoggerFactory loggerFactory, IOptionsSnapshot<D365AuthSettings> d365AuthSettings, ID365AuthenticationService authenticationService)
    {
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _d365AuthSettings = d365AuthSettings.Value;
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    }

    public async Task<HttpResponseMessage> SendRetrieveRequestAsync(AZAppUser spn, string requestUri, bool formatted = false, int pageSize = 50, bool isProcess = false)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!isProcess)
            request.Headers.Add("Prefer", "odata.maxpagesize=" + pageSize.ToString());

        if (formatted)
            request.Headers.Add("Prefer", "odata.include-annotations=OData.Community.Display.V1.FormattedValue");

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendCreateRequestAsync(AZAppUser spn, string entitySetName, string requestBody)
    {
        HttpRequestMessage message = new(HttpMethod.Post, entitySetName)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        message.Headers.Add("Prefer", "return=representation");

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendPatchRequestAsync(AZAppUser spn, string requestUri, string requestBody)
    {
        HttpRequestMessage message = new(HttpMethod.Patch, requestUri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        message.Headers.Add("Prefer", "return=representation");

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(message);
    }

    public async Task<HttpResponseMessage> SendDocumentRequestAsync(AZAppUser spn, string entityNameSet, Guid id, Byte[] data, string fileName)
    {
        UploadFileRequest request;
        if (entityNameSet.Equals("ofm_payment_file_exchanges"))
        {
            request = new(new EntityReference(entityNameSet, id), columnName: "ofm_input_document_memo", data, fileName);
        }
        else
        {
            request = new(new EntityReference(entityNameSet, id), columnName: "ofm_file", data, fileName);
        }
        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetDocumentRequestAsync(AZAppUser spn, string entityNameSet, Guid id)
    {
        DownloadFileRequest request;
        if (entityNameSet.Equals("ofm_payment_file_exchanges"))
        {
            request = new(new EntityReference(entityNameSet, id), columnName: "ofm_feedback_document_memo",false);
        }
        else
        {
            request = new(new EntityReference(entityNameSet, id), columnName: "ofm_file", false);
        }
        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
    }


    public async Task<HttpResponseMessage> SendDeleteRequestAsync(AZAppUser spn, string requestUri)
    {
        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.DeleteAsync(requestUri);
    }

    public async Task<HttpResponseMessage> SendSearchRequestAsync(AZAppUser spn, string body)
    {
        var message = new HttpRequestMessage()
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Post
        };

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Search, spn);

        return await client.SendAsync(message);
    }

    public async Task<BatchResult> SendBatchMessageAsync(AZAppUser spn, List<HttpRequestMessage> requestMessages, Guid? callerObjectId)
    {
        BatchRequest batchRequest = new(_d365AuthSettings)
        {
            Requests = requestMessages,
            ContinueOnError = true
        };
        if (callerObjectId != null)
            batchRequest.Headers.Add("CallerObjectId", callerObjectId.ToString());

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Batch, spn);
        BatchResponse batchResponse = await SendAsync<BatchResponse>(batchRequest, client);

        Int16 processed = 0;
        List<string> errors = new();

        if (batchResponse.IsSuccessStatusCode)
            batchResponse.HttpResponseMessages.ForEach(async res =>
            {
                if (res.IsSuccessStatusCode)
                {
                    processed++;
                }
                else
                {
                    errors.Add(await res.Content.ReadAsStringAsync());
                }
            });

        if (errors.Any())
        {
            var batchResult = BatchResult.Failure(errors, 0, 0);

            if (errors.Count < requestMessages.Count)
                batchResult = BatchResult.PartialSuccess(null, errors, processed, requestMessages.Count);

            _logger.LogError(CustomLogEvent.Batch, "Batch operation finished with an error {error}", JsonValue.Create<BatchResult>(batchResult));

            return batchResult;
        }

        return BatchResult.Success(null, processed, requestMessages.Count); ;
    }

    public async Task<HttpResponseMessage> SendBulkEmailTemplateMessageAsync(AZAppUser spn, JsonObject contentBody, Guid? callerObjectId)
    {
        BulkEmailTemplateRequest request = new(contentBody, _d365AuthSettings) { };
        if (callerObjectId != null)
            request.Headers.Add("CallerObjectId", callerObjectId.ToString());

        HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.CRUD, spn);

        return await client.SendAsync(request);
    }

    #region Helpers

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpClient client)
    {
        //// Session token used by elastic tables to enable strong consistency
        //// See https://learn.microsoft.com/power-apps/developer/data-platform/use-elastic-tables?tabs=webapi#sending-the-session-token
        //if (!string.IsNullOrWhiteSpace(_sessionToken) && request.Method == HttpMethod.Get)
        //{
        //    request.Headers.Add("MSCRM.SessionToken", _sessionToken);
        //}

        //HttpClient client = await _authenticationService.GetHttpClientAsync(D365ServiceType.Batch, spn);


        // Set the access token using the function from the Config passed to the constructor
        //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await config.GetAccessToken());

        // Get the named HttpClient from the IHttpClientFactory
        //var client = GetHttpClientFactory().CreateClient(WebAPIClientName);

        HttpResponseMessage response = await client.SendAsync(request);

        //// Capture the current session token value
        //// See https://learn.microsoft.com/power-apps/developer/data-platform/use-elastic-tables?tabs=webapi#getting-the-session-token
        //if (response.Headers.Contains("x-ms-session-token"))
        //{
        //    _sessionToken = response.Headers.GetValues("x-ms-session-token")?.FirstOrDefault()?.ToString();
        //}

        // Throw an exception if the request is not successful
        if (!response.IsSuccessStatusCode)
        {
            D365ServiceException exception = await ParseError(response);
            throw exception;
        }
        return response;
    }

    /// <summary>
    /// Processes requests with typed responses
    /// </summary>
    /// <typeparam name="T">The type derived from HttpResponseMessage</typeparam>
    /// <param name="request">The request</param>
    /// <param name="client"></param>
    /// <returns></returns>
    public async Task<T> SendAsync<T>(HttpRequestMessage request, HttpClient client) where T : HttpResponseMessage
    {
        HttpResponseMessage response = await SendAsync(request, client);

        // 'As' method is Extension of HttpResponseMessage see Extensions.cs
        return response.As<T>();
    }

    public async Task<D365ServiceException> ParseError(HttpResponseMessage response)
    {
        string requestId = string.Empty;
        if (response.Headers.Contains("REQ_ID"))
        {
            requestId = response.Headers.GetValues("REQ_ID").FirstOrDefault();
        }

        var content = await response.Content.ReadAsStringAsync();
        ODataError? oDataError = null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            oDataError = JsonSerializer.Deserialize<ODataError>(content, options);
        }
        catch (Exception)
        {
            // Error may not be in correct OData Error format, so keep trying...
        }

        if (oDataError?.Error != null)
        {
            var exception = new D365ServiceException(oDataError.Error.Message)
            {
                ODataError = oDataError,
                Content = content,
                ReasonPhrase = response.ReasonPhrase,
                HttpStatusCode = response.StatusCode,
                RequestId = requestId
            };
            return exception;
        }
        else
        {
            try
            {
                ODataException oDataException = JsonSerializer.Deserialize<ODataException>(content);

                D365ServiceException otherException = new(oDataException.Message)
                {
                    Content = content,
                    ReasonPhrase = response.ReasonPhrase,
                    HttpStatusCode = response.StatusCode,
                    RequestId = requestId
                };
                return otherException;

            }
            catch (Exception)
            {

            }

            //When nothing else works
            D365ServiceException exception = new(response.ReasonPhrase)
            {
                Content = content,
                ReasonPhrase = response.ReasonPhrase,
                HttpStatusCode = response.StatusCode,
                RequestId = requestId
            };
            return exception;
        }
    }

    #endregion
}