using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Models;
public record AppSettings
{
    public required APIKey[] ApiKeys { get; set; }
    public required Int16 PageSize { get; set; }
    public required Int16 MaxPageSize { get; set; }
    public required int MaxFileSize { get; set; }
    public required string FileFommats { get; set; }
    public required string FileSizeErrorMessage { get; set; }
    public required string FileFormatErrorMessage { get; set; }
    public required bool RetryEnabled { get; set; }
    /// <summary>
    /// Maximum number of times to re-try when service protection limits hit
    /// </summary>
    public required Int16 MaxRetries { get; set; }
    public required TimeSpan AutoRetryDelay { get; set; }
    public Int16 MinsToCache { get; set; }
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

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(D365AuthSettings))]
public partial class D365AuthSettingsSerializationContext : JsonSerializerContext
{
}