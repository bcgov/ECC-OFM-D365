using System.Text.Json.Serialization;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;

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
    public required RenewalReminderOptions RenewalReminderOptions { get; set; }
    public required string DefaultSenderId { get; set; }
    public required EmailTemplate[] EmailTemplates { get; set; }
    public required CommunicationTypes CommunicationTypes { get; set; }
    public required SafeList EmailSafeList { get; set; }
    public required string fundingUrl { get; set; }
    public required string fundingTabUrl { get; set; }


    public record SafeList
    {
        public required bool Enable { get; set; }
        public required string[] Recipients { get; set; }
        public required string DefaultContactId { get; set; }
    }
}

public record UnreadEmailOptions
{
    public Int16 FirstReminderInDays { get; set; }
    public Int16 SecondReminderInDays { get; set; }
    public Int16 ThirdReminderInDays { get; set; }
    public Int16 TimeOffsetInDays { get; set; }
}

public record RenewalReminderOptions
{
    public Int16 FirstReminderInDays { get; set; }
    public Int16 SecondReminderInDays { get; set; }
    public Int16 ThirdReminderInDays { get; set; }
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
    public required Guid CallerObjectId { get; set; }
    
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
    public int NoDuration { get; set; }
    public string batchtaskprocess { get; set; }
    public string singletaskprocess { get; set; }
    public required TaskActivity TaskActivity { get; set; }
   

}
public record TaskActivity
{
    public required string subject { get; set; }
    public required string description { get; set; }
    
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
    public required string oracleBatchNumber { get; set; }
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


[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(D365AuthSettings))]
public partial class D365AuthSettingsSerializationContext : JsonSerializerContext
{
}