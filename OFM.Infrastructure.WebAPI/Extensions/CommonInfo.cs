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
    public const string Operation = "OFM.Portal.Operation";
    public const string Contact = "OFM.D365.Contact";
    public const string Request = "OFM.D365.Request";
    public const string Process = "OFM.D365.Process";
    public const string Batch = "OFM.D365.Batch";
}

public class CustomLogEvents
{
    #region Portal events

    public const int ProviderProfile = 1001;
    public const int Operation = 1100;
    public const int Document = 1200;
    public const int Batch = 1500;

    #endregion

    #region D365 events

    public const int Process = 2000;

    #endregion
}