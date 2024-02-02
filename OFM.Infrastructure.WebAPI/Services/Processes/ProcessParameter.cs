using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public record ProcessParameter
{
    //DO NOT change the optional properties
    [property: JsonPropertyName("triggeredBy")]
    public string? TriggeredBy { get; set; }
    [property: JsonPropertyName("triggeredOn")]
    public DateTime? TriggeredOn { get; set; }
    [property: JsonPropertyName("callerObjectId")]
    public Guid? CallerObjectId { get; set; }
    [property: JsonPropertyName("notification")]
    public EmailParameter? Notification { get; set; }
    [property: JsonPropertyName("applicationId")]
    public string? ApplicationId { get; set; }
    [property: JsonPropertyName("supplementalId")]
    public string? SupplementalId { get; set; }



    public record EmailParameter
    {
        [property: JsonPropertyName("templateId")]
        public Guid? TemplateId { get; set; }

        [property: JsonPropertyName("marketingListId")]
        public Guid? MarketingListId { get; set; }

        [property: JsonPropertyName("senderId")]
        public Guid? SenderId { get; set; }

        [property: JsonPropertyName("dueDate")]
        public DateTime? DueDate { get; set; }

        [property: JsonPropertyName("communicationTypeId")]
        public Guid? CommunicationTypeId { get; set; }

        [property: JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [property: JsonPropertyName("emailBody")]
        public string? EmailBody { get; set; }
    }
}