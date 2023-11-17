using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Messages;

public class BatchRequest : HttpRequestMessage
{
    public BatchRequest(D365AuthSettings d365AuthSettings)
    {
        Method = HttpMethod.Post;
        RequestUri = new Uri($"{d365AuthSettings.WebApiUrl}/$batch", UriKind.Absolute);
        Content = new MultipartContent("mixed", $"batch_{Guid.NewGuid().ToString()}");
        ServiceBaseAddress = new Uri($"{d365AuthSettings.WebApiUrl}");

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

    private bool continueOnError;
    private readonly Uri? ServiceBaseAddress;

    /// <summary>
    /// Sets the Prefer: odata.continue-on-error request header for the request.
    /// </summary>
    public bool ContinueOnError
    {
        get
        {
            return continueOnError;
        }
        set
        {
            if (continueOnError != value)
            {
                if (value)
                {
                    Headers.Add("Prefer", "odata.continue-on-error");
                }
                else
                {
                    Headers.Remove("Prefer");
                }
            }
            continueOnError = value;
        }
    }

    /// <summary>
    /// Sets the ChangeSets to be included in the request.
    /// </summary>
    public List<ChangeSet> ChangeSets
    {
        set
        {
            value.ForEach(changeSet =>
            {
                MultipartContent content = new("mixed", $"changeset_{Guid.NewGuid()}");

                int count = 1;
                changeSet.Requests.ForEach(request =>
                {
                    HttpMessageContent messageContent = ToMessageContent(request);
                    messageContent.Headers.Add("Content-ID", count.ToString());

                    content.Add(messageContent);

                    count++;
                });
                //Add to the content
                ((MultipartContent)Content).Add(content);

            });
        }
    }

    /// <summary>
    /// Sets any requests to be sent outside of any ChangeSet
    /// </summary>
    public List<HttpRequestMessage> Requests
    {
        set
        {
            value.ForEach(request =>
            {
                //Add to the content
                ((MultipartContent)Content).Add(ToMessageContent(request));

            });
        }
    }

    /// <summary>
    /// Converts a HttpRequestMessage to HttpMessageContent
    /// </summary>
    /// <param name="request">The HttpRequestMessage to convert.</param>
    /// <returns>HttpMessageContent with the correct headers.</returns>
    private HttpMessageContent ToMessageContent(HttpRequestMessage request)
    {
        //Relative URI is not allowed with MultipartContent
        request.RequestUri = new Uri(
           baseUri: ServiceBaseAddress!,
           relativeUri: $"/api/data/v9.2/{request.RequestUri.ToString()}");

        if (request.Content != null)
        {
            if (request.Content.Headers.Contains("Content-Type"))
            {
                request.Content.Headers.Remove("Content-Type");
            }
            request.Content.Headers.Add("Content-Type", "application/json;type=entry");
        }

        HttpMessageContent messageContent = new(request);

        if (messageContent.Headers.Contains("Content-Type"))
        {
            messageContent.Headers.Remove("Content-Type");
        }
        messageContent.Headers.Add("Content-Type", "application/http");
        messageContent.Headers.Add("Content-Transfer-Encoding", "binary");

        return messageContent;
    }
}
