using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public record ProcessParameter
{
    //DO NOT change the optional properties
    public string? TriggeredBy { get; set; }
    public DateTime? TriggeredOn { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? SenderId { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? CommunicationTypeId { get; set; }
    public Guid? MarketingListId { get; set; }
    public string? Subject { get; set; }
    public string? EmailBody { get; set; }
    public Guid? CallerObjectId { get; set; }
}