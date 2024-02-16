using OFM.Infrastructure.WebAPI.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace OFM.Infrastructure.WebAPI.Extensions;

public enum BatchMethodName { GET, POST, PATCH, DELETE }
public enum D365ServiceType { Search, Batch, CRUD }

public static class Setup
{
    public static class AppUserType
    {
        public static string Portal => "P";
        public static string System => "S";
        public static string Notification => "N";
    }

    public static class Process
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

            public const Int16 SendNotificationsId = 205;
            public const string SendNotificationsName = "Send bulk emails on-demand";
        }

        public static class Funding
        {
            public const Int16 CalculateBaseFundingId = 300;
            public const string CalculateBaseFundingName = "Calculate the envelope funding amounts";

            public const Int16 CalculateSupplementaryFundingId = 305;
            public const string CalculateSupplementaryFundingName = "Calculate the supplementary funding amounts";

            public const Int16 CalculateDefaultAllocationId = 310;
            public const string CalculateDefaultAllocationName = "Calculate the default room allocation in the room split scenario";
        }

        public static class ProviderProfile
        {
            public const Int16 VerifyGoodStandingId = 400;
            public const string VerifyGoodStandingName = "Verify Good Standing Status for Organization";

            public const Int16 VerifyGoodStandingBatchId = 405;
            public const string VerifyGoodStandingBatchName = "Verify Good Standing Status for Organizations in batch";
        }

        public static class Payment
        {
            public const Int16 SendPaymentRequestId = 500;
            public const string SendPaymentRequestName = "Send Payment Request and Invoices to BC Pay";
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

    public static readonly JsonSerializerOptions s_readOptionsRelaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static JsonSerializerOptions s_writeOptionsForLogs
    {
        get
        {
            var encoderSettings = new TextEncoderSettings();
            encoderSettings.AllowCharacters('\u0022', '\u0436', '\u0430', '\u0026', '\u0027');
            encoderSettings.AllowRange(UnicodeRanges.All);

            return new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(encoderSettings),
                WriteIndented = true
            };
        }
    }

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

public class CustomLogEvent
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