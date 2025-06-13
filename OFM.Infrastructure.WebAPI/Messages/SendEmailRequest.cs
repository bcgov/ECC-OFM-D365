using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Messages;

public class SendEmailRequest : HttpRequestMessage
{
    public SendEmailRequest(Guid id, JsonObject record)
    {
        var path = $"/api/data/v9.2/emails({id})/Microsoft.Dynamics.CRM.SendEmail";

        Method = HttpMethod.Post;

        Content = new StringContent(
                content: record.ToJsonString(),
                encoding: System.Text.Encoding.UTF8,
                mediaType: "application/json");

        RequestUri = new Uri(path, UriKind.Relative);

        //RequestUri = new Uri(
        //  uriString: Setup.PrepareUri(path),
        //  uriKind: UriKind.Absolute);

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