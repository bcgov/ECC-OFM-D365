using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.Processes;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public interface ID365WebApiService
{
    Task<HttpResponseMessage> SendRetrieveRequestAsync(AZAppUser spn, string requestUrl, bool formatted = false, int pageSize = 50);
    Task<HttpResponseMessage> SendCreateRequestAsync(AZAppUser spn, string entitySetName, string requestBody);
    Task<HttpResponseMessage> SendPatchRequestAsync(AZAppUser spn, string requestUrl, string content);
    Task<HttpResponseMessage> SendDeleteRequestAsync(AZAppUser spn, string requestUrl);
    Task<HttpResponseMessage> SendSearchRequestAsync(AZAppUser spn, string requestBody);
    Task<BatchResult> SendBatchMessageAsync(AZAppUser spn, List<HttpRequestMessage> requestMessages, Guid? callerObjectId);
    Task<HttpResponseMessage> SendDocumentRequestAsync(AZAppUser spn, string entityNameSet, Guid id, Byte[] data, string fileName);
    Task<HttpResponseMessage> SendBulkEmailTemplateMessageAsync(AZAppUser spn, JsonObject requestMessage, Guid? callerObjectId);
    Task<D365ServiceException> ParseError(HttpResponseMessage response);
}