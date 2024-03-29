﻿using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Services.Processes;

public class Location
{
    [property: JsonProperty("@odata.type")]
    public string ODataType => "Microsoft.Dynamics.CRM.expando";

    // Gets or sets the latitude of the Location.
    public double? Latitude { get; set; }

    [JsonProperty("Latitude@odata.type")]
    public static string LatitudeType => "Double";

    // Gets or sets the longitude of the Location.
    public double? Longitude { get; set; }

    [JsonProperty("Longitude@odata.type")]
    public static string LongitudeType => "Double";
}

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
    public OrganizationParameter? Organization { get; set; }
    [property: JsonPropertyName("funding")]
    public FundingParameter? Funding { get; set; }

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

    public record FundingParameter
    {
        //[property: JsonPropertyName("facilityId")]
        //public string? FacilityId { get; set; }

        [property: JsonPropertyName("fundingId")]
        public string? FundingId { get; set; }

        [property: JsonPropertyName("supplementaryId")]
        public string? SupplementaryId { get; set; }
    }
}