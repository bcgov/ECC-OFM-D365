using Microsoft.Xrm.Sdk;

namespace OFM.Infrastructure.WebAPI.Messages;

/// <summary>
/// Contains the data to update file column
/// </summary>
public sealed class DownloadFileRequest : HttpRequestMessage
{
    public DownloadFileRequest(
        EntityReference entityReference,
        string columnName,
        bool returnFullSizedImage = false)
    {
       

        Method = HttpMethod.Get;
        RequestUri = new Uri(
            uriString: $"{entityReference.Path}/{columnName}/$value",
            uriKind: UriKind.Relative);
      
    }
}