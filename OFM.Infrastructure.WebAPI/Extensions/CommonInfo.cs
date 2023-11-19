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
        public static string Notification => "N";
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

    public static string PrepareUri(string requertUrl)
    {
        if (!requertUrl.StartsWith("/"))
            requertUrl = "/" + requertUrl;

        if (requertUrl.ToLowerInvariant().Contains("/api/data/v"))
        {
            requertUrl = requertUrl.Substring(requertUrl.IndexOf("/api/data/v"));
            requertUrl = requertUrl.Substring(requertUrl.IndexOf('v'));
            requertUrl = requertUrl.Substring(requertUrl.IndexOf('/'));
        }

        return requertUrl;
    }
}

public class LogCategory
{
    public const string API = "OFM.API.ApiKey";

    public const string ProviderProfile = "OFM.Portal.ProviderProfile";
    public const string Operation = "OFM.Portal.Operation";
    public const string Document = "OFM.Portal.Document";

    public const string Contact = "OFM.D365.Contact";
    public const string Request = "OFM.D365.Request";
    public const string Process = "OFM.D365.Process";
    public const string Batch = "OFM.D365.Batch";
    public const string Email = "OFM.D365.Email";
}

public class CustomLogEvents
{
    public const int API = 1000;

    #region Portal events

    public const int ProviderProfile = 1001;
    public const int Operation = 1100;
    public const int Document = 1200;
    public const int Batch = 1500;

    #endregion

    #region D365 events

    public const int Process = 2000;
    public const int Email = 2050;

    #endregion
}

public class ProcessStatus
{
    public const string Successful = "Successful";
    public const string Completed = "Completed";
    public const string Partial = "Partially Completed";
    public const string Failed = "Failed";
}