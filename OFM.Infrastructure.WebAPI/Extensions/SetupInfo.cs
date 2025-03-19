using OFM.Infrastructure.WebAPI.Models;
using System.ComponentModel.DataAnnotations;
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
        public static class Requests
        {
            public const Int16 CloseInactiveRequestsId = 100;
            public const string CloseInactiveRequestsName = "Cancel inactive requests";
        }

        public static class Emails
        {
            public const Int16 SendEmailRemindersId = 200;
            public const string SendEmailRemindersName = "Send nightly email reminders";

            public const Int16 SendNotificationsId = 205;
            public const string SendNotificationsName = "Send bulk emails on-demand";

            public const Int16 SendFundingNotificationsId = 210;
            public const string SendFundingNotificationsName = "Create emails on Status change of Funding record to FASignaturePending when a Ministry EA approves the funding";

            public const Int16 SendSupplementaryNotificationsId = 215;
            public const string SendSupplementaryNotificationsName = "Create supplementary email reminders";

            public const Int16 CreateEmailRemindersId = 220;
            public const string CreateEmailRemindersName = "Create Email Reminders for Supplementary Application";

            public const Int16 CreateECENotificationsId = 225;
            public const string CreateECENotificationsName = "Create Email Reminders for Not Good ECE";

            public const Int16 CreateApplicationNotificationsId = 230;
            public const string CreateApplicationNotificationsName = "Create Email for Application Ineligilble";

            public const Int16 CreateExpenseApplicationNotificationsId = 235;
            public const string CreateExpenseApplicationNotificationsName = "Create Email for Irregular Expense Approval/Denial";
           
            public const Int16 CreateAllowanceApprovalDenialNotificationId = 240;
            public const string CreateAllowanceApprovalDenialNotificationName = "Create email and pdf for supplementary Approval/Denial";

            public const Int16 CreateMonthlyReportNotificationId = 245;
            public const string CreateMonthlyReportNotificationName = "Create email for Published Monthly Report";
        }

        public static class Fundings
        {
            public const Int16 CalculateBaseFundingId = 300;
            public const string CalculateBaseFundingName = "Calculate the envelope funding amounts";

            public const Int16 CalculateSupplementaryFundingId = 305;
            public const string CalculateSupplementaryFundingName = "Calculate the supplementary funding amounts";

            public const Int16 CalculateDefaultSpacesAllocationId = 310;
            public const string CalculateDefaultSpacesAllocationName = "Calculate the default spaces allocation in the room split scenario";
        }

        public static class ProviderProfiles
        {
            public const Int16 VerifyGoodStandingId = 400;
            public const string VerifyGoodStandingName = "Verify Good Standing Status for Organization";

            public const Int16 VerifyGoodStandingBatchId = 405;
            public const string VerifyGoodStandingBatchName = "Verify Good Standing Status for Organizations in batch";

            public const Int16 GetDBAId = 410;
            public const string GetDBAName = "Get DBA Information for Organization";

            public const Int16 GetDBABatchId = 415;
            public const string GetDBABatchName = "Get DBA Information for Organization in batch";
        }

        public static class Payments
        {
            public const Int16 SendPaymentRequestId = 500;
            public const string SendPaymentRequestName = "Send Payment Request and Invoices to BC Pay";

            public const Int16 GeneratePaymentLinesId = 505;
            public const string GeneratePaymentLinesName = "Generate Payment Lines";

            public const Int16 GetPaymentResponseId = 510;
            public const string GetPaymentResponseName = "Get Payment Feedback and Invoices to BC Pay";

            public const Int16 GeneratePaymentLinesForIrregularExpenseId = 515;
            public const string GeneratePaymentForIrregularExpenseName = "Generate Payment Lines for Irregular Expense";

            public const Int16 GeneratePaymentLinesForSupplementaryAllowanceId = 520;
            public const string GeneratePaymentLinesForSupplementaryAllowanceName = "Generate Payment Lines for Irregular Expense";

            public const Int16 GenerateTopUpPaymentId = 525;
            public const string GenerateTopUpPaymentName = "Generate Payment Lines for Top-Up";
        }

        public static class FundingReports
        {
            public const Int16 CloneFundingReportResponseId = 600;
            public const string CloneFundingReportResponseName = "Clone Provider Report Responses";

            public const Int16 CloseDuedReportsId = 605;
            public const string CloseDuedReportsName = "Automatically Close Provider Reports at the Due Date";

            public const Int16 CreateMonthlyReportId = 615;
            public const string CreateMonthlyReportName = "Automatically Create Monthly Reports";

        }
        public static class Reporting
        {
            public const Int16 CreateUpdateQuestionId = 610;
            public const string CreateUpdateQuestionName = "Create/Update Question from Customer Voice to Reporting Custom Tables.";
        }

        public static class DataImports
        {
            public const Int16 ProcessProviderCertificatesId = 700;
            public const string ProcessProviderCertificatesName = "Process and update Provider Certificates";
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
            requertUrl = requertUrl[requertUrl.IndexOf("/api/data/v")..];
            requertUrl = requertUrl[requertUrl.IndexOf('v')..];
            requertUrl = requertUrl[requertUrl.IndexOf('/')..];
        }

        return requertUrl;
    }

    public static int FieldLength(this Type modelClass, string propertyName)
    {
        int fieldLen = 0;
        StringLengthAttribute strLen = modelClass.GetProperty(propertyName).GetCustomAttributes(typeof(StringLengthAttribute), false).Cast<StringLengthAttribute>().SingleOrDefault();
        if (strLen != null)
        {
            fieldLen = strLen.MaximumLength;
        }
        return fieldLen;
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