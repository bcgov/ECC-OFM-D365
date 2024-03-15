using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Messages;

public class SendEmailFromTemplateRequest : HttpRequestMessage
{
    public SendEmailFromTemplateRequest(JsonObject record, D365AuthSettings d365AuthSettings)
    {
        var path = $"{d365AuthSettings.WebApiUrl}SendEmailFromTemplate";
        Method = HttpMethod.Post;
        RequestUri = new Uri(path, UriKind.Absolute);
        Content = new StringContent(
                content: record.ToJsonString(),
                encoding: System.Text.Encoding.UTF8,
                mediaType: "application/json");

        if (Headers != null)
        {
            if (!Headers.Contains("OData-MaxVersion"))
            {
                Headers.Add("OData-MaxVersion", "4.0");
            }
            if (!Headers.Contains("OData-Version"))
            {
                Headers.Add("OData-Version", "4.0");
            }
            if (!Headers.Contains("Accept"))
            {
                Headers.Add("Accept", "application/json");
            }
        }
    }
}