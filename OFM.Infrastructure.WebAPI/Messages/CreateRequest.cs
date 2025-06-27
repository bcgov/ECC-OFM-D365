using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Messages;
/// <summary>
/// Contains the data to create a record.
/// </summary>
public sealed class CreateRequest : HttpRequestMessage
{
    /// <summary>
    /// Intializes the CreateRequest
    /// </summary>
    /// <param name="entitySetName">The name of the entity set.</param>
    /// <param name="record">Contains the data for the record to create.</param>
    /// <param name="preventDuplicateRecord">Whether to throw an error when a duplicate record is detected.</param>
    /// <param name="partitionId">The partition key to use.</param>
    public CreateRequest(string entitySetName, JsonObject record, bool preventDuplicateRecord = false, string? partitionId = null)
    {
        string path;
        if (partitionId != null)
        {
            path = $"{entitySetName}?partitionid='{partitionId}'";
        }
        else
        {
            path = entitySetName;
        }

        Method = HttpMethod.Post;
        RequestUri = new Uri(
            uriString: path,
            uriKind: UriKind.Relative);

        Content = new StringContent(
                content: record.ToJsonString(),
                encoding: System.Text.Encoding.UTF8,
                mediaType: "application/json");
        if (preventDuplicateRecord)
        {
            //If duplicate detection enabled for table only
            Headers.Add("MSCRM.SuppressDuplicateDetection", "false");
        }
    }
}

public sealed class PatchRequest : HttpRequestMessage
{
    /// <summary>
    /// Intializes the Patch Request for Update and Upsert Requests
    /// </summary>
    /// <param name="entitySetName">The name of the entity set.</param>
    /// <param name="record">Contains the data for the record to update and upsert.</param>
    public PatchRequest(string entitySetName, JsonObject record)
    {
        string path;
        path = entitySetName;
        Method = HttpMethod.Patch;
        RequestUri = new Uri(
            uriString: path,
            uriKind: UriKind.Relative);

        Content = new StringContent(
                content: record.ToJsonString(),
                encoding: System.Text.Encoding.UTF8,
                mediaType: "application/json");
        
    }
}