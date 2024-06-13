using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OFM.Infrastructure.Plugins.Supplementary
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class UpdateSupplementarySchedule : PluginBase
    {
        public UpdateSupplementarySchedule(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(UpdateSupplementarySchedule))
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

            localPluginContext.Trace("Start UpdateSupplementarySchedule Plug-in");

            if (localPluginContext.Target.Contains(ECC.Core.DataContext.ofm_allowance.Fields.ofm_allowanceid))
            {
                // Getting latest data to get the value
                /*var newOrganization = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.parentcustomerid);*/

                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    var currentDate = DateTime.UtcNow;

                    localPluginContext.Trace($"CurrentDate {currentDate}");

                    // Set Condition Values
                    var query_statecode = 0;

                    // Instantiate QueryExpression query
                    QueryExpression query = new QueryExpression("ofm_supplementary_schedule");
                    query.ColumnSet.AllColumns = true;
                    FilterExpression filter = new FilterExpression(LogicalOperator.And);

                    FilterExpression filter1 = new FilterExpression(LogicalOperator.And);
                    filter1.Conditions.Add(new ConditionExpression("statecode", ConditionOperator.Equal, query_statecode));
                    filter1.Conditions.Add(new ConditionExpression("ofm_start_date", ConditionOperator.OnOrBefore, currentDate));

                    FilterExpression filter2 = new FilterExpression(LogicalOperator.Or);
                    filter2.Conditions.Add(new ConditionExpression("ofm_end_date", ConditionOperator.OnOrAfter, currentDate));
                    filter2.Conditions.Add(new ConditionExpression("ofm_end_date", ConditionOperator.Null));

                    filter.AddFilter(filter1);
                    filter.AddFilter(filter2);
                    query.Criteria = filter;

                    var result = localPluginContext.PluginUserService.RetrieveMultiple(query);
                    var supplementaryScheduleId = result.Entities.FirstOrDefault().Id;

                    localPluginContext.Trace($"SupplementarySchedule {supplementaryScheduleId}");

                    if (supplementaryScheduleId != null)
                    {
                        var entity = new ofm_allowance
                        {
                            Id = localPluginContext.Target.Id,
                            ofm_supplementary_schedule = new EntityReference(ECC.Core.DataContext.ofm_supplementary_schedule.EntityLogicalName, supplementaryScheduleId)
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