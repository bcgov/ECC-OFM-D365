using Newtonsoft.Json;
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

    [property: JsonPropertyName("allowance")]
    public SupplementaryApplicationParameter? SupplementaryApplication { get; set; }

    [property: JsonPropertyName("expense")]
    public ExpenseApplicationParameter? ExpenseApplication { get; set; }

    [property: JsonPropertyName("funding")]
    public FundingParameter? Funding { get; set; }

    [property: JsonPropertyName("topup")]
    public TopupParameter? Topup { get; set; }

    [property: JsonPropertyName("fundingReport")]
    public FundingReportParameter? FundingReport { get; set; }

    [property: JsonPropertyName("project")]
    public CustomerVoiceProjectParameter? CustomerVoiceProject { get; set; }
    
    [property: JsonPropertyName("ReportSections")]
    public string? ReportSections { get; set; }

    [property: JsonPropertyName("paymentfile")]
   
    public PaymentParameter? PaymentFile { get; set; }

    [property: JsonPropertyName("dataImportId")]
    public Guid? DataImportId { get; set; }

    //created for P255 Create renewal reminders for Existing Fundings
    [property: JsonPropertyName("createExistingFundingReminders")]
    public bool? CreateExistingFundingReminders { get; set; }
    [property: JsonPropertyName("lastTimeStamp")]
    public DateTime? LastScoreCalculationTimeStamp { get; set; }
    [property: JsonPropertyName("scoreCalculatorVersionId")]
    public Guid? ScoreCalculatorVersionId { get; set; }

    #region Inner Parameter Record Objects

    public record PaymentParameter
    {  
        [property: JsonPropertyName("paymentfileid")]
        public string? paymentfileId { get; set; }
    }
 
    public record EmailParameter
    {
        [property: JsonPropertyName("templateId")]
        public Guid? TemplateId { get; set; }

        [property: JsonPropertyName("templateNumber")]
        public string? TemplateNumber { get; set; }

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

        [property: JsonPropertyName("reportStartDate")]
        public DateTime? ReportStartDate { get; set; }
    }

    public record OrganizationParameter
    {
        [property: JsonPropertyName("organizationId")]
        public Guid? organizationId { get; set; }
        [property: JsonPropertyName("facilityId")]
        public Guid? facilityId { get; set; }

        [property: JsonPropertyName("legalName")]
        public string? legalName { get; set; }

        [property: JsonPropertyName("incorporationNumber")]
        public string? incorporationNumber { get; set; }
    }

    public record ApplicationParameter
    {
        [property: JsonPropertyName("applicationId")]
        public Guid? applicationId { get; set; }
        [property: JsonPropertyName("createdOn")]
        public DateTime? createdOn { get; set; }
        [property: JsonPropertyName("submittedOn")]
        public DateTime? submittedOn { get; set; }
        [property: JsonPropertyName("facilityId")]
        public string? facilityId { get; set; }
    }

    public record SupplementaryApplicationParameter
    {
        [property: JsonPropertyName("allowanceId")]
        public Guid? allowanceId { get; set; }
    }

    public record ExpenseApplicationParameter
    {
        [property: JsonPropertyName("expenseId")]
        public Guid? expenseId { get; set; }
    }

    public record FundingParameter
    {
        [property: JsonPropertyName("fundingId")]
        public string? FundingId { get; set; }

        [property: JsonPropertyName("supplementaryId")]
        public string? SupplementaryId { get; set; }
        
        [property: JsonPropertyName("ofm_monthly_province_base_funding_y1")]
        public decimal? MonthlyBaseFundingAmount { get; set; }

        [property: JsonPropertyName("previous_monthly_province_base_funding_y1")]
        public decimal? PreviousMonthlyBaseFundingAmount { get; set; }

        [property: JsonPropertyName("isMod")]
        public bool? IsMod { get; set; }
    }

    public record TopupParameter
    {
        [property: JsonPropertyName("topupId")]
        public string? TopupId { get; set; }
    }

    public record CustomerVoiceProjectParameter
    {
        [property: JsonPropertyName("Project_Guid")]
        public string? ProjectId { get; set; }

        [property: JsonPropertyName("StartDate")]
        public DateTime? StartDate { get; set; }

        [property: JsonPropertyName("EndDate")]
        public DateTime? EndDate { get; set; }
    }

    public record FundingReportParameter
    {
        [property: JsonPropertyName("fundingReportId")]
        public string? FundingReportId { get; set; }

        [property: JsonPropertyName("batchFlag")]
        public bool? BatchFlag { get; set; }

        [property: JsonPropertyName("facilityId")]
        public string? FacilityId { get; set; }

        [property: JsonPropertyName("hrQuestions")]
        public string? HRQuestions { get; set; }
    }

    #endregion
}