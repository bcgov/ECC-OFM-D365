using FixedWidthParserWriter;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace OFM.Infrastructure.WebAPI.Models;

public record AppSettings
{
    public required APIKey[] ApiKeys { get; set; }
    public required Int16 PageSize { get; set; }
    public required Int16 MaxPageSize { get; set; }
    public required bool RetryEnabled { get; set; }
    /// <summary>
    /// Maximum number of times to re-try when service protection limits hit
    /// </summary>
    public required Int16 MaxRetries { get; set; }
    public required TimeSpan AutoRetryDelay { get; set; }
    public required Int16 MinsToCache { get; set; }
}

public record DocumentSettings
{
    public int MaxFileSize { get; set; }
    public required string[] AcceptedFommat { get; set; }
    public required string FileSizeErrorMessage { get; set; }
    public required string FileFormatErrorMessage { get; set; }
}

public record NotificationSettings
{
    public required UnreadEmailOptions UnreadEmailOptions { get; set; }
    public required string DefaultSenderId { get; set; }
    public required EmailTemplate[] EmailTemplates { get; set; }
    public required CommunicationTypes CommunicationTypes { get; set; }
}

public record UnreadEmailOptions
{
    public Int16 FirstReminderInDays { get; set; }
    public Int16 SecondReminderInDays { get; set; }
    public Int16 ThirdReminderInDays { get; set; }
    public Int16 TimeOffsetInDays { get; set; }
}

public record CommunicationTypes
{
    public required Int16 ActionRequired { get; set; }
    public required Int16 DebtLetter { get; set; }
    public required Int16 Reminder { get; set; }
    public required Int16 FundingAgreement { get; set; }
    public required Int16 Information { get; set; }
}

public class EmailTemplate
{
    public int TemplateNumber { get; set; }
    public string TemplateId { get; set; }
    public string Description { get; set; }
}

public record ProcessSettings
{
    public required Int16 MaxRequestInactiveDays { get; set; }
    public required string ClosingReason { get; set; }
}


public record D365AuthSettings
{
    /// <summary>
    /// A function provided by the client application to return an access token.
    /// </summary>
    public Func<Task<string>>? GetAccessToken { get; set; }
    public required string BaseUrl { get; set; } // Dynamics Base URL
    public required string ResourceUrl { get; set; }
    public required string WebApiUrl { get; set; }
    public required string BatchUrl { get; set; }
    public required string BaseServiceUrl { get; set; } // Dynamics Base Service URL for Dataverse Search, Batch Operations etc.
    public required string RedirectUrl { get; set; }
    public required string ApiVersion { get; set; }
    public required Int16 TimeOutInSeconds { get; set; }
    public required string SearchVersion { get; set; }
    public required List<AZAppUser> AZAppUsers { get; set; }
    public required string HttpClientName { get; set; }
}

public record AZAppUser
{
    public required Int16 Id { get; set; }
    public required string TenantId { get; set; }
    public required string ClientId { get; set; } // Azure Registered Application ID
    public required string ClientSecret { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
}

public record APIKey
{
    public required Int16 Id { get; set; }
    public required string KeyName { get; set; }
    public required string KeyValue { get; set; }
}

public record AuthenticationSettings
{
    public required Schemes Schemes { get; set; }
}

public record Schemes
{
    public required ApiKeyScheme ApiKeyScheme { get; set; }
}

public record ApiKeyScheme
{
    public required string ApiKeyName { get; set; }
    public required ApiKey[] Keys { get; set; }
    public required string ApiKeyErrorMesssage { get; set; }
}

public record ApiKey
{
    public required int Id { get; set; }
    public required string ClientName { get; set; }
    public required string Value { get; set; }
}
public record ExternalServices
{
    public required BCRegistrySettings BCRegistryApi { get; set; }
    public required BCCASApi BCCASApi { get; set; }
}

public record BCRegistrySettings
{
    public bool Enable { get; set; }
    public required string BusinessSearchUrl { get; set; }
    public required string RegistrySearchUrl { get; set; }
    public required string KeyName { get; set; }
    public required string KeyValue { get; set; }
    public int MinsToCache { get; set; }
}

public record BCCASApi
{
    public bool Enable { get; set; }
    public required string Url { get; set; }
    public required string KeyName { get; set; }
    public required string KeyValue { get; set; }
    public int MinsToCache { get; set; }
    public int transactionCount { get; set; }
    public required string cGIBatchNumber { get; set; }
    public required string clientCode { get; set; }
    public required string feederNumber { get; set; }
    public required string trailertransactionType { get; set; }
    public required string messageVersionNumber { get; set; }
    public required string transactionType { get; set; }
    public required string batchType { get; set; }
    public required string delimiter { get; set; }
    public required InvoiceHeader InvoiceHeader { get; set; }
    public required InvoiceLines InvoiceLines { get; set; }

}


    public record InvoiceHeader
{
     public required string feederNumber { get; set; }    
    public required string headertransactionType { get; set; }    
    public required string batchType { get; set; }
    public required string delimiter { get; set; }
    [StringLength(9)]
    public required string supplierNumber { get; set; }
    [StringLength(3)]
    public required string supplierSiteNumber { get; set; }
    [StringLength(50)]
     public required string invoiceNumber { get; set; }
    [StringLength(2)]
    public required string invoiceType { get; set; }
    [StringLength(8)]
    public required string invoiceDate { get; set; }
    [StringLength(8)]
    public required string invoiceRecDate { get; set; }
    [StringLength(8)]
    public required string goodsDate { get; set; }
    [StringLength(20)]
    public required string PONumber { get; set; }
    [StringLength(9)]
    public required string payGroupLookup { get; set; }
    [StringLength(4)]
    public required string remittanceCode { get; set; }
    [StringLength(15)]
    public required string grossInvoiceAmount { get; set; }
    [StringLength(3)]
    public required string CAD { get; set; }
    [StringLength(50)]
    public required string termsName { get; set; }
    [StringLength(60)]
    public required string description { get; set; }
    [StringLength(30)]
    public required string oracleBatchName { get; set; }   
    public required string payflag { get; set; }
    [StringLength(110)]
    public required string flow { get; set; }
    [StringLength(9)]
    public required string SIN { get; set; }
    public List<InvoiceLines>? invoiceLines { get; set; }
}

public record InvoiceLines
{

    public required string feederNumber { get; set; }
    public required string batchType  { get; set; }
    public required string delimiter { get; set; }
    public required string linetransactionType { get; set; }
    [StringLength(50)]
    public required string invoiceNumber  { get; set; }
    [StringLength(4)]
    public required string invoiceLineNumber  { get; set; }
    [StringLength(9)]
    public required string supplierNumber  { get; set; }
    [StringLength(3)]
    public required string supplierSiteNumber  { get; set; }
    [StringLength(4)]
    public required string committmentLine  { get; set; }
    [StringLength(15)]
    public required string lineAmount  { get; set; }
    [StringLength(1)]
    public required string lineCode  { get; set; }
    [StringLength(50)]
    public required string distributionACK  { get; set; }
    [StringLength(55)]
    public required string lineDescription { get; set; }
    [StringLength(8)]
    public required string effectiveDate { get; set; }
    [StringLength(10)]
    public required string quantity  { get; set; }
    [StringLength(15)]
    public required string unitPrice { get; set; }
    [StringLength(163)]
    public required string optionalData  { get; set; }
    [StringLength(30)]
    public required string distributionSupplierNumber { get; set; }
    [StringLength(110)]
    public required string flow { get; set; }
  
}

public class feedbackParam
{
    [CustomFileField(StartsWith ="APBH",Start =4,Length =4)]
    public string BHCode { get; set; }

    [CustomFileField(StartsWith = "APBH", Start = 8, Length = 150)]
    public string BHError { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 4, Length = 9)]
    public string IHSupplier { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 16, Length = 50)]
    public string IHInvoice { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 411, Length = 4)]
    public string IHCode { get; set; }

    [CustomFileField(StartsWith = "APIH", Start = 415, Length = 150)]
    public string IHError { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 531, Length = 4)]
    public string ILCode { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 4, Length = 9)]
    public string ILSupplier { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 16, Length = 50)]
    public string ILInvoice { get; set; }

    [CustomFileField(StartsWith = "APIL", Start = 535, Length = 150)]
    public string ILError { get; set; }

    [CustomFileField(StartsWith = "APBT")]
    public string BatchTrailer { get; set; }
   

}



[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(D365AuthSettings))]
public partial class D365AuthSettingsSerializationContext : JsonSerializerContext
{
}