using OFM.Infrastructure.WebAPI.Models;
using System.Text.Json;

namespace OFM.Infrastructure.WebAPI.Extensions;

public enum BatchMethodName { GET, POST, PATCH, DELETE }
public enum D365RequestType { Search, Batch, CRUD }

public static class CommonInfo
{
    public static class AppUserType
    {
        public static string Portal => "P";
        public static string System => "S";
    }
    public static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions s_readOptions = new()
    {
        AllowTrailingCommas = true
    };

    public static AppSettings GetAppSettings(IConfiguration config)
    {
        var appSettingsSection = config.GetSection(nameof(AppSettings));
        var appSettings = appSettingsSection.Get<AppSettings>();

        return appSettings ?? throw new KeyNotFoundException(nameof(AppSettings));
    }

    public static AuthenticationSettings GetAuthSettings(IConfiguration config)
    {
        var authSettingsSection = config.GetSection(nameof(AuthenticationSettings));
        var authSettings = authSettingsSection.Get<AuthenticationSettings>();
 
        return authSettings ?? throw new KeyNotFoundException(nameof(AuthenticationSettings));
    }
}