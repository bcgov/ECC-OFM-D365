using OFM.Infrastructure.WebAPI.Models;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Messages;

public class BulkEmailTemplateRequest: HttpRequestMessage
{
    public BulkEmailTemplateRequest(JsonObject record, D365AuthSettings d365AuthSettings)
    {
        var path = $"{d365AuthSettings.WebApiUrl}SendTemplate";

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

    public static string PrepareUri(string requertUrl)
    {
        if (!requertUrl.StartsWith("/"))
            requertUrl = "/" + requertUrl;

        if (requertUrl.ToLowerInvariant().Contains("/api/data/v"))
        {
            requertUrl = requertUrl.Substring(requertUrl.IndexOf("/api/data/v"));
            requertUrl = requertUrl.Substring(requertUrl.IndexOf('v'));
            requertUrl = requertUrl.Substring(requertUrl.IndexOf('/'));
        }

        return requertUrl;
    }
}