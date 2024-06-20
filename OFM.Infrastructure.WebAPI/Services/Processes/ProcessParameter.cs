using Newtonsoft.Json;
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
    [property: JsonPropertyName("application")]
    public ApplicationParameter? Application { get; set; }
    [property: JsonPropertyName("ofm_allowance")]
    public SupplementaryApplicationParameter? SupplementaryApplication { get; set; }
    [property: JsonPropertyName("funding")]
    public FundingParameter? Funding { get; set; }
    [property: JsonPropertyName("fundingReport")]
    public FundingReportParameter? FundingReport { get; set; }


    [property: JsonPropertyName("project")]
    public ProjectParameter? Project { get; set; }
    
    [property: JsonPropertyName("ReportSections")]
    public string? ReportSections { get; set; }

    [property: JsonPropertyName("paymentfile")]
    public PaymentParameter? paymentfile { get; set; }

    [property: JsonPropertyName("providerreport")]
    public ProviderReportParameter? ProviderReport { get; set; }

    public record PaymentParameter
    {
       
        [property: JsonPropertyName("paymentfileid")]
        public string? paymentfileId { get; set; }
    }

    public record ProviderReportParameter
    {

        [property: JsonPropertyName("providerreportId")]
        public string? providerreportId { get; set; }
    }
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

        [property: JsonPropertyName("communicationTypeNum")]
        public int? CommunicationTypeNum { get; set; }

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

    public record ApplicationParameter
    {
        [property: JsonPropertyName("applicationId")]
        public Guid? applicationId { get; set; }

        
    }
    public record SupplementaryApplicationParameter
    {
        [property: JsonPropertyName("fyYear")]
        public string? fyYear { get; set; }

    }
    public record FundingParameter
    {
        //[property: JsonPropertyName("facilityId")]
        //public string? FacilityId { get; set; }

        [property: JsonPropertyName("fundingId")]
        public string? FundingId { get; set; }

        [property: JsonPropertyName("supplementaryId")]
        public string? SupplementaryId { get; set; }
        
        [property: JsonPropertyName("ofm_monthly_province_base_funding_y1")]
        public string? ofm_monthly_province_base_funding_y1 { get; set; }

        [property: JsonPropertyName("previous_monthly_province_base_funding_y1")]
        public string? previous_monthly_province_base_funding_y1 { get; set; }

        [property: JsonPropertyName("isMod")]
        public bool? isMod { get; set; }
    }

    public record ProjectParameter
    {
        //[property: JsonPropertyName("facilityId")]
        //public string? FacilityId { get; set; }

        [property: JsonPropertyName("Project_Guid")]
        public string? ProjectId { get; set; }
    }

    public record FundingReportParameter
    {
        [property: JsonPropertyName("fundingReportId")]
        public string? FundingReportId { get; set; }

        [property: JsonPropertyName("batchFlag")]
        public bool? BatchFlag { get; set; }

        [property: JsonPropertyName("facilityId")]
        public string? FacilityId { get; set; }
    }
}

