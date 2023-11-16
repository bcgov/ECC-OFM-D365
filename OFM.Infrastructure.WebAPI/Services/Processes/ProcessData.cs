using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class ProcessData
{
    public ProcessData(JsonNode data)
    {
        Data = data;
    }

    public JsonNode Data { get; }
}