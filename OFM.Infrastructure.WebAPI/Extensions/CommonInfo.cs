using OFM.Infrastructure.WebAPI.Models;
using System.Text.Json;

namespace OFM.Infrastructure.WebAPI.Extensions;

public enum BatchMethodName { GET, POST, PATCH, DELETE }
public enum D365ServiceType { Search, Batch, CRUD }

public static class CommonInfo
{
    public static class AppUserType
    {
        public static string Portal => "P";
        public static string System => "S";
    }

    public static class ProcessInfo
    {
        public static class Request
        {
            public const Int16 CloseInactiveRequestsId = 100;
            public const string CloseInactiveRequestsName = "Cancel inactive requests";
        }

        public static class Email
        {
            public const Int16 SendEmailRemindersId = 200;
            public const string SendEmailRemindersName = "Send nightly email reminders";
        }
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

public class LogCategory
{
    public const string ProviderProfile = "OFM.Portal.ProviderProfile";
    public const string Operations = "OFM.Portal.Operations";
    public const string D365Contact = "OFM.D365.Contact";
    public const string D365Requests = "OFM.D365.Requests";
    public const string BatchProcesses = "OFM.D365.BatchProcesses";
}

public class CustomLogEvents
{
    #region Portal events

    public const int ProviderProfile = 1001;
    public const int Operations = 1100;
    public const int Documents = 1200;

    #endregion

    #region D365 events

    public const int BatchProcesses = 2000;

    #endregion
}