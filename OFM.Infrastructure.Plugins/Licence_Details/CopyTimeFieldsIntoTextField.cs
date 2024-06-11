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
                var licence_details = localPluginContext.PluginUserService.Retrieve(ofm_licence_detail.EntityLogicalName, localPluginContext.Target.Id, new ColumnSet(true));
                localPluginContext.Trace("Start CopyTimeFieldsIntoTextField Plug-in");

                if ((localPluginContext.PluginExecutionContext.MessageName == "Create" && localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_from)
                    && localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_to)) || localPluginContext.PluginExecutionContext.MessageName == "Update"
                    && (localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_from) || localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_to)
                    || localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_licence_type)))
                {
                    var operationHoursFrom = localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_from) ? localPluginContext.Target.GetAttributeValue<DateTime?>(ofm_licence_detail.Fields.ofm_operation_hours_from) : null;
                    var operationHoursTo = localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_hours_to) ? localPluginContext.Target.GetAttributeValue<DateTime?>(ofm_licence_detail.Fields.ofm_operation_hours_to) : null;

                    using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                    {
                        var timeZoneCode = localPluginContext.PluginUserService.Retrieve(UserSettings.EntityLogicalName, localPluginContext.PluginExecutionContext.InitiatingUserId, new ColumnSet(UserSettings.Fields.timezonecode));
                        var timeZoneQuery = new QueryExpression()
                        {
                            EntityName = TimeZoneDefinition.EntityLogicalName,
                            ColumnSet = new ColumnSet(TimeZoneDefinition.Fields.standardname),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression(TimeZoneDefinition.Fields.timezonecode, ConditionOperator.Equal,timeZoneCode.GetAttributeValue<int>(UserSettings.Fields.timezonecode))
                                }
                            }
                        };
                        var result = localPluginContext.PluginUserService.RetrieveMultiple(timeZoneQuery);

                        var operations_From_Hours = operationHoursFrom != null ? ConvertTimeIntoSpecificTimeZone(operationHoursFrom, result) :
                            ConvertTimeIntoSpecificTimeZone(licence_details.GetAttributeValue<DateTime>(ofm_licence_detail.Fields.ofm_operation_hours_from), result);
                        var operations_To_Hours = operationHoursTo != null ? ConvertTimeIntoSpecificTimeZone(operationHoursTo, result) :
                            ConvertTimeIntoSpecificTimeZone(licence_details.GetAttributeValue<DateTime>(ofm_licence_detail.Fields.ofm_operation_hours_to), result);

                        var isFullTime = (operations_From_Hours != null && operations_To_Hours != null) ? (Convert.ToDateTime(operations_From_Hours).AddHours(4) < Convert.ToDateTime(operations_To_Hours) ? true : false) : false;
                        var entity = new ofm_licence_detail
                        {
                            Id = localPluginContext.Target.Id,
                            ofm_operation_from_time = operations_From_Hours,
                            ofm_operations_to_time = operations_To_Hours,
                            ofm_care_type = isFullTime ? ecc_care_types.FullTime : ecc_care_types.PartTime
                        };

                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);
                    }
                }
                else if ((localPluginContext.PluginExecutionContext.MessageName == "Create" && localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_from_time)
                    && localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operations_to_time)) || localPluginContext.PluginExecutionContext.MessageName == "Update"
                    && (localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_from_time) || localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operations_to_time)
                    || localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_care_type)))
                {
                    var operationHoursFrom = localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operation_from_time) ?
                        Convert.ToDateTime(localPluginContext.Target.GetAttributeValue<string>(ofm_licence_detail.Fields.ofm_operation_from_time)) :
                        Convert.ToDateTime(licence_details.GetAttributeValue<string>(ofm_licence_detail.Fields.ofm_operation_from_time));

                    var operationHoursTo = localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_operations_to_time) ?
                        Convert.ToDateTime(localPluginContext.Target.GetAttributeValue<string>(ofm_licence_detail.Fields.ofm_operations_to_time)) :
                        Convert.ToDateTime(licence_details.GetAttributeValue<string>(ofm_licence_detail.Fields.ofm_operations_to_time));

                    var timeSpan = (operationHoursFrom != null && operationHoursTo != null) ? operationHoursFrom.AddHours(4) < operationHoursTo ? true : false : false;
                    using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                    {
                        var entity = new ofm_licence_detail
                        {
                            Id = localPluginContext.Target.Id,
                            ofm_operation_hours_from = operationHoursFrom,
                            ofm_operation_hours_to = operationHoursTo,
                            ofm_care_type = timeSpan ? ecc_care_types.FullTime : ecc_care_types.PartTime
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
                            TimeZoneInfo.FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields.standardname)).FirstOrDefault().ToString())));
        }
    }
}