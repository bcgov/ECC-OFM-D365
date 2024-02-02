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

    [property: JsonPropertyName("organization")]
    public OrganizationParameter? organization { get; set; }

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

    public record OrganizationParameter
    {
        [property: JsonPropertyName("organizationId")]
        public Guid? organizationId { get; set; }

        [property: JsonPropertyName("legalName")]
        public string? legalName { get; set; }

        [property: JsonPropertyName("incorporationNumber")]
        public string? incorporationNumber { get; set; }
    }

}