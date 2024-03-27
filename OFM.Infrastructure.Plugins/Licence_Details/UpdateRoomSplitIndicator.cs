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
    public class UpdateRoomSplitIndicator : PluginBase
    {
        public UpdateRoomSplitIndicator(string unsecureConfiguration, string secureConfiguration) : base(typeof(UpdateRoomSplitIndicator))
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
            if (localPluginContext.Target.Contains(ofm_licence_detail.Fields.ofm_apply_room_split_condition))
            {
                var licence_detail = localPluginContext.PluginUserService.Retrieve(ofm_licence_detail.EntityLogicalName, localPluginContext.Target.Id, new ColumnSet(true));
                localPluginContext.Trace("Start UpdateRoomSplitIndicator Plug-in");

                //Get related applications
                var ofm_application_statecode = (int)ECC.Core.DataContext.ofm_application_statecode.Active;
                var licence_detail_id = licence_detail.Id.ToString();

                var query = new QueryExpression("ofm_application");
                query.ColumnSet.AddColumns("ofm_room_split_indicator", "ofm_applicationid");
                query.Criteria.AddCondition("statecode", ConditionOperator.Equal, ofm_application_statecode);
                var query_account = query.AddLink("account", "ofm_facility", "accountid");

                var query_account_ofm_licence = query_account.AddLink("ofm_licence", "accountid", "ofm_facility");

                var query_account_ofm_licence_ofm_licence_detail = query_account_ofm_licence.AddLink(
                    "ofm_licence_detail",
                    "ofm_licenceid",
                    "ofm_licence");

                query_account_ofm_licence_ofm_licence_detail.LinkCriteria.AddCondition("ofm_licence_detailid", ConditionOperator.Equal, licence_detail_id);

                var result = localPluginContext.PluginUserService.RetrieveMultiple(query);

                if (result.Entities.Count > 0)
                {
                    var ofm_application_id = result.Entities.FirstOrDefault().Id.ToString();
                    localPluginContext.Trace(ofm_application_id);

                    var ofm_licence_detail_statecode = (int)ECC.Core.DataContext.ofm_licence_detail_statecode.Active;
                    var ofm_apply_room_split_condition = true;
                    var ofm_licence_statecode = (int)ECC.Core.DataContext.ofm_licence_statecode.Active;

                    var room_split_indicator = false;

                    var licence_detail_query = new QueryExpression("ofm_licence_detail");
                    licence_detail_query.ColumnSet.AllColumns = true;
                    licence_detail_query.Criteria.AddCondition("statecode", ConditionOperator.Equal, ofm_licence_detail_statecode);
                    licence_detail_query.Criteria.AddCondition("ofm_apply_room_split_condition", ConditionOperator.Equal, ofm_apply_room_split_condition);
                    var query_ofm_licence = licence_detail_query.AddLink("ofm_licence", "ofm_licence", "ofm_licenceid");

                    query_ofm_licence.LinkCriteria.AddCondition("statecode", ConditionOperator.Equal, ofm_licence_statecode);
                    var query_ofm_licence_account = query_ofm_licence.AddLink("account", "ofm_facility", "accountid");

                    var query_ofm_licence_account_ofm_application = query_ofm_licence_account.AddLink("ofm_application", "accountid", "ofm_facility");

                    query_ofm_licence_account_ofm_application.LinkCriteria.AddCondition("ofm_applicationid", ConditionOperator.Equal, ofm_application_id);

                    var licence_detail_result = localPluginContext.PluginUserService.RetrieveMultiple(licence_detail_query);

                    if (licence_detail_result.Entities.Count > 0)
                    {
                        room_split_indicator = true;
                    }

                    //Update application records
                    foreach (var item in result.Entities) 
                    {
                        if (item.GetAttributeValue<Boolean>(ofm_application.Fields.ofm_room_split_indicator) != room_split_indicator)
                        {

                            using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                            {
                                var entity = new ofm_application
                                {
                                    Id = item.Id,
                                    ofm_room_split_indicator = room_split_indicator
                                };

                                UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                                crmContext.Execute(updateRequest);
                            }
                        }
                    }
                }
                
            }
        }
    }
}