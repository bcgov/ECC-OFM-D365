using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OFM.Infrastructure.Plugins.Licence_Details
{
    public class CopyTimeFieldsIntoTextField : PluginBase
    {
        public CopyTimeFieldsIntoTextField(string unsecureConfiguration, string secureConfiguration) : base(typeof(CopyTimeFieldsIntoTextField))
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
            if (localPluginContext.PluginExecutionContext.Depth == 1)
            {
                var licence_details = localPluginContext.PluginUserService.Retrieve(OfM_Licence_Detail.EntityLogicalName, localPluginContext.Target.Id, new ColumnSet(true));
                localPluginContext.Trace("Start CopyTimeFieldsIntoTextField Plug-in");

                if ((localPluginContext.PluginExecutionContext.MessageName == "Create" && localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_From)
                    && localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_To)) || localPluginContext.PluginExecutionContext.MessageName == "Update"
                    && (localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_From) || localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_To) 
                    || localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Licence_Type)))
                {
                    var operationHoursFrom = localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_From) ? localPluginContext.Target.GetAttributeValue<DateTime?>(OfM_Licence_Detail.Fields.OfM_Operation_Hours_From) : null;
                    var operationHoursTo = localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_Hours_To) ? localPluginContext.Target.GetAttributeValue<DateTime?>(OfM_Licence_Detail.Fields.OfM_Operation_Hours_To) : null;

                    using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                    {
                        var timeZoneCode = localPluginContext.PluginUserService.Retrieve(UserSettings.EntityLogicalName, localPluginContext.PluginExecutionContext.InitiatingUserId, new ColumnSet(UserSettings.Fields.TimeZoneCode));
                        var timeZoneQuery = new QueryExpression()
                        {
                            EntityName = TimeZoneDefinition.EntityLogicalName,
                            ColumnSet = new ColumnSet(TimeZoneDefinition.Fields.StandardName),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression(TimeZoneDefinition.Fields.TimeZoneCode, ConditionOperator.Equal,timeZoneCode.GetAttributeValue<int>(UserSettings.Fields.TimeZoneCode))
                                }
                            }
                        };
                        var result = localPluginContext.PluginUserService.RetrieveMultiple(timeZoneQuery);

                        var operations_From_Hours = operationHoursFrom != null ? ConvertTimeIntoSpecificTimeZone(operationHoursFrom, result) :
                            ConvertTimeIntoSpecificTimeZone(licence_details.GetAttributeValue<DateTime>(OfM_Licence_Detail.Fields.OfM_Operation_Hours_From), result);
                        var operations_To_Hours = operationHoursTo != null ? ConvertTimeIntoSpecificTimeZone(operationHoursTo, result) :
                            ConvertTimeIntoSpecificTimeZone(licence_details.GetAttributeValue<DateTime>(OfM_Licence_Detail.Fields.OfM_Operation_Hours_To), result);

                        var isFullTime = (operations_From_Hours != null && operations_To_Hours != null) ? (Convert.ToDateTime(operations_From_Hours).AddHours(4) <= Convert.ToDateTime(operations_To_Hours) ? true : false) : false;
                        var entity = new OfM_Licence_Detail
                        {
                            Id = localPluginContext.Target.Id,
                            OfM_Operation_From_Time = operations_From_Hours,
                            OfM_Operations_To_Time = operations_To_Hours,
                            OfM_Care_Type = isFullTime ? ECc_Care_Types.FullTime : ECc_Care_Types.PartTime
                        };

                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);
                    }
                }
                else if ((localPluginContext.PluginExecutionContext.MessageName == "Create" && localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_From_Time)
                    && localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operations_To_Time)) || localPluginContext.PluginExecutionContext.MessageName == "Update"
                    && (localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_From_Time) || localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operations_To_Time)
                    || localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Licence_Type)))
                {
                    var operationHoursFrom = localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operation_From_Time) ?
                        Convert.ToDateTime(localPluginContext.Target.GetAttributeValue<string>(OfM_Licence_Detail.Fields.OfM_Operation_From_Time)) :
                        Convert.ToDateTime(licence_details.GetAttributeValue<string>(OfM_Licence_Detail.Fields.OfM_Operation_From_Time));

                    var operationHoursTo = localPluginContext.Target.Contains(OfM_Licence_Detail.Fields.OfM_Operations_To_Time) ?
                        Convert.ToDateTime(localPluginContext.Target.GetAttributeValue<string>(OfM_Licence_Detail.Fields.OfM_Operations_To_Time)) :
                        Convert.ToDateTime(licence_details.GetAttributeValue<string>(OfM_Licence_Detail.Fields.OfM_Operations_To_Time));

                    var timeSpan = (operationHoursFrom != null && operationHoursTo != null) ? operationHoursFrom.AddHours(4) <= operationHoursTo ? true : false : false;
                    using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                    {
                        var entity = new OfM_Licence_Detail
                        {
                            Id = localPluginContext.Target.Id,
                            OfM_Operation_Hours_From = operationHoursFrom,
                            OfM_Operation_Hours_To = operationHoursTo,
                            OfM_Care_Type = timeSpan ? ECc_Care_Types.FullTime : ECc_Care_Types.PartTime
                        };

                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);
                    }
                }
            }
        }
        protected string ConvertTimeIntoSpecificTimeZone(DateTime? operationHoursFrom, EntityCollection result)
        {
            return string.Format("{0:hh:mm tt}", TimeZoneInfo.ConvertTimeFromUtc((DateTime)operationHoursFrom,
                            TimeZoneInfo.FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields.StandardName)).FirstOrDefault().ToString())));
        }
    }
}