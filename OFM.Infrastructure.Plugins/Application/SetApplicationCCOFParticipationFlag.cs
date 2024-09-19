using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using System.IdentityModel.Metadata;

namespace OFM.Infrastructure.Plugins.Application
{
    public class SetApplicationCCOFParticipationFlag : PluginBase
    {
        public SetApplicationCCOFParticipationFlag(string unsecureConfiguration, string secureConfiguration)
             : base(typeof(SetApplicationCCOFParticipationFlag))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new InvalidPluginExecutionException(nameof(localPluginContext), new Dictionary<string, string>() { ["failedRecordId"] = localPluginContext.Target.Id.ToString() });
            }

            localPluginContext.Trace("Start SetApplicationCCOFParticipationFlag Plug-in");

            if (localPluginContext.Target.Contains(ofm_application.Fields.statuscode))
            {
                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    
                   
                    //Get facility program start date
                    var application = localPluginContext.PluginUserService.Retrieve(ofm_application.EntityLogicalName, localPluginContext.Target.Id, new ColumnSet(true));

                    var status = application.GetAttributeValue<OptionSetValue>(ofm_application.Fields.statuscode);

                    localPluginContext.Trace($"status {status.Value}");

                    if (status.Value == (int) ofm_application_StatusCode.Submitted)
                    {
                        EntityReference facility = application.GetAttributeValue<EntityReference>(ofm_application.Fields.ofm_facility);
                        if (facility != null)
                        {
                            var facilityStartDate = localPluginContext.PluginUserService.Retrieve(facility.LogicalName, facility.Id, new ColumnSet(true)).GetAttributeValue<DateTime>(Account.Fields.ccof_facilitystartdate);
                            localPluginContext.Trace($"facilityStartDate {facilityStartDate}");

                            //Compare the application submission time and facility program start date
                            var applicationSubmittedOn = application.GetAttributeValue<DateTime>(ofm_application.Fields.ofm_summary_submittedon);

                            var  entity = new ofm_application
                            {
                                Id = localPluginContext.Target.Id
                            };

                            entity["ofm_system_ccof_tdad_participation"] = applicationSubmittedOn >= facilityStartDate.AddYears(1);

                            UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                            crmContext.Execute(updateRequest);
                        }
                    }
                }
            }
        }
    }
}
