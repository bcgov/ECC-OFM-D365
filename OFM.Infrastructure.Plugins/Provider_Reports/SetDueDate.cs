using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OFM.Infrastructure.Plugins.Provider_Reports
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class SetDueDate : PluginBase
    {
        public SetDueDate(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(SetDueDate))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new InvalidPluginExecutionException(nameof(localPluginContext), new Dictionary<string, string>() { ["failedRecordId"] = localPluginContext.Target.Id.ToString() });
            }

            localPluginContext.Trace("Start SetDueDate Plug-in");

            if (localPluginContext.Target.Contains(ECC.Core.DataContext.ofm_survey_response.Fields.ofm_survey_responseid) && !localPluginContext.Target.Contains(ECC.Core.DataContext.ofm_survey_response.Fields.ofm_current_version))
            {
                // Getting latest data to get the value

                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    var currentDate = DateTime.UtcNow;

                    localPluginContext.Trace($"CurrentDate {currentDate}");

                    //Set the duedate base on fiscal year and report month

                    var fiscal_year_ref = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.ofm_survey_response.Fields.ofm_fiscal_year);
                    var report_month_ref = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.ofm_survey_response.Fields.ofm_reporting_month);

     
                    if (fiscal_year_ref != null && report_month_ref != null)
                    {
                        var fiscal_year = crmContext.ofm_fiscal_yearSet.Where(year => year.Id == fiscal_year_ref.Id).FirstOrDefault();
                        var fiscal_year_start = fiscal_year.GetAttributeValue<DateTime>(ofm_fiscal_year.Fields.ofm_start_date);
                        var fiscal_year_end = fiscal_year.GetAttributeValue<DateTime>(ofm_fiscal_year.Fields.ofm_end_date);

                        //converted to PST to compare
                        var fiscal_year_start_in_PST = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(fiscal_year_start, "Pacific Standard Time");
                        var fiscal_year_end_in_PST = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(fiscal_year_end, "Pacific Standard Time");

                        localPluginContext.Trace($"fiscal_year_start_in_PST {fiscal_year_start_in_PST}");
                        localPluginContext.Trace($"fiscal_year_end_in_PST {fiscal_year_end_in_PST}");

                        var report_month = crmContext.ofm_monthSet.Where(month => month.Id == report_month_ref.Id).FirstOrDefault();
                        var report_month_name = report_month.GetAttributeValue<string>(ofm_month.Fields.ofm_name);

                        localPluginContext.Trace($"report_month_name {report_month_name}");

                        int month_num = DateTime.ParseExact(report_month_name, "MMMM", CultureInfo.CurrentCulture).Month;
                        var report_month_date = (fiscal_year_start_in_PST <= new DateTime(fiscal_year_start.Year, month_num, 01, 0, 0, 0) && fiscal_year_end_in_PST >= new DateTime(fiscal_year_start.Year, month_num, 01, 0, 0, 0)) ? new DateTime(fiscal_year_start.Year, month_num, 01, 23, 59, 0): new DateTime(fiscal_year_end.Year, month_num, 01, 23, 59, 0);
                        localPluginContext.Trace($"report_month_date {report_month_date}");

                        var duedateInPST = report_month_date.AddMonths(2).AddDays(-1);

                        TimeZoneInfo PSTZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

                        var duedateInUTC = TimeZoneInfo.ConvertTimeToUtc(duedateInPST, PSTZone);

                        localPluginContext.Trace($"duedateInPST {duedateInPST}");
                        localPluginContext.Trace($"duedateInUTC {duedateInUTC}");

                        //Update the duedate
                        var entity = new ofm_survey_response
                        {
                            Id = localPluginContext.Target.Id,
                            ofm_duedate = duedateInUTC
                        };
                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);
                    }
                  
                    localPluginContext.Trace("Completed with no errors.");
                }
            }
        }
    }
}