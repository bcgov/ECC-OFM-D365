using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.D365WebApi;

public interface ID365WebApiService
{
    Task<HttpResponseMessage> SendRetrieveRequestAsync(AZAppUser spn, string requestUrl, bool formatted = false, int pageSize = 50);
    Task<HttpResponseMessage> SendCreateRequestAsync(AZAppUser spn, string entitySetName, string requestBody);
    Task<HttpResponseMessage> SendPatchRequestAsync(AZAppUser spn, string requestUrl, string content);
    Task<HttpResponseMessage> SendDocumentPatchRequestAsync(AZAppUser spn, string requestUrl, byte[] content, string fileName);
    Task<HttpResponseMessage> SendDeleteRequestAsync(AZAppUser spn, string requestUrl);
    Task<HttpResponseMessage> SendSearchRequestAsync(AZAppUser spn, string requestBody);
    Task<HttpResponseMessage> SendBatchMessageAsync(AZAppUser spn, List<HttpRequestMessage> requestMessages, Guid? callerObjectId);
    Task<HttpResponseMessage> SendDocumentRequestAsync(AZAppUser spn, string entityNameSet, Guid id, Byte[] data, string fileName);
}