using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public record ProcessParameter
{
    //DO NOT change the optional properties
    public string? TriggeredBy { get; set; }
    public DateTime? TriggeredOn { get; set; }
    public Guid? CallerObjectId { get; set; }
}