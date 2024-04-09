using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OFM.Infrastructure.Plugins.Application
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class SetApplicationRoomSplitIndicator : PluginBase
    {
        public SetApplicationRoomSplitIndicator(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(SetApplicationRoomSplitIndicator))
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

            localPluginContext.Trace("Start SetApplicationRoomSplitIndicator Plug-in");

            if (localPluginContext.Target.Contains(ofm_application.Fields.ofm_room_split_indicator))
            {
                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    var ofm_licence_detail_statecode = (int)ECC.Core.DataContext.ofm_licence_detail_statecode.Active;
                    var ofm_apply_room_split_condition = true;
                    var ofm_licence_statecode = (int)ECC.Core.DataContext.ofm_licence_statecode.Active;

                    var ofm_application_id = localPluginContext.Target.Id.ToString();

                    var query = new QueryExpression("ofm_licence_detail");
                    query.ColumnSet.AllColumns = true;
                    query.Criteria.AddCondition("statecode", ConditionOperator.Equal, ofm_licence_detail_statecode);
                    query.Criteria.AddCondition("ofm_apply_room_split_condition", ConditionOperator.Equal, ofm_apply_room_split_condition);
                    var query_ofm_licence = query.AddLink("ofm_licence", "ofm_licence", "ofm_licenceid");

                    query_ofm_licence.LinkCriteria.AddCondition("statecode", ConditionOperator.Equal, ofm_licence_statecode);
                    var query_ofm_licence_account = query_ofm_licence.AddLink("account", "ofm_facility", "accountid");

                    var query_ofm_licence_account_ofm_application = query_ofm_licence_account.AddLink("ofm_application", "accountid", "ofm_facility");

                    query_ofm_licence_account_ofm_application.LinkCriteria.AddCondition("ofm_applicationid", ConditionOperator.Equal, ofm_application_id);

                    var result = localPluginContext.PluginUserService.RetrieveMultiple(query);

                    var count = result.Entities.Count;
                    localPluginContext.Trace("count number: " + count);
                    if (result.Entities.Count > 0)
                    {
                        var entity = new ofm_application
                        {
                            Id = localPluginContext.Target.Id,
                            ofm_room_split_indicator = true
                        };
                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);
                    }
                }

            }
        }
    }
}