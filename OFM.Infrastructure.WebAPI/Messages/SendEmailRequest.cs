using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Messages;

public class SendEmailRequest : HttpRequestMessage
{
    public SendEmailRequest(Guid id, JsonObject record)
    {
        var path = $"emails({id})/Microsoft.Dynamics.CRM.SendEmail";

        Method = HttpMethod.Post;

        Content = new StringContent(
                content: record.ToJsonString(),
                encoding: System.Text.Encoding.UTF8,
                mediaType: "application/json");

        RequestUri = new Uri(
          uriString: Setup.PrepareUri(path),
          uriKind: UriKind.Relative);

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