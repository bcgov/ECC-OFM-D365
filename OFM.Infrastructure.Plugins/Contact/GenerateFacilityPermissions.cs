using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Runtime.Remoting.Services;
using System.Text;
using System.Threading.Tasks;

namespace OFM.Infrastructure.Plugins.Contact
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class GenerateFacilityPermissions : PluginBase
    {
        public GenerateFacilityPermissions(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GenerateFacilityPermissions))
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

            localPluginContext.Trace("Start GenerateFacilityPremissions Plug-in");

            if (localPluginContext.Target.Contains(ECC.Core.DataContext.Contact.Fields.parentcustomerid))
            {
                // Getting latest data to get the value
                var newOrganization = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.parentcustomerid);

                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    var oldPermissions = crmContext.ofm_bceid_facilitySet.Where(permission => permission.ofm_bceid.Id == localPluginContext.Target.Id).ToList();
                    localPluginContext.Trace($"Total Old permissions count {oldPermissions.Count}");

                    oldPermissions.ForEach(record =>
                    {
                        if (record.Attributes.Contains(ofm_bceid_facility.Fields.statuscode))
                        {
                            var entity = new ofm_bceid_facility
                            {
                                Id = record.Id,
                                statecode = ofm_bceid_facility_statecode.Inactive,
                                statuscode = ofm_bceid_facility_StatusCode.Inactive
                            };

                            UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                            crmContext.Execute(updateRequest);

                        }
                    });

                    var newFacilityPermissions = crmContext.AccountSet.Where(facility => facility.parentaccountid.Id == newOrganization.Id).ToList();
                    localPluginContext.Trace($"Total New child facilities count {newFacilityPermissions.Count}");

                    newFacilityPermissions.ForEach(record =>
                    {
                        if (record.Attributes.Contains(Account.Fields.statuscode) &&
                            record.GetAttributeValue<OptionSetValue>(Account.Fields.statuscode).Value == Convert.ToInt32(Account_StatusCode.Active))
                        {
                            var entity = new ofm_bceid_facility
                            {
                                ofm_bceid = new EntityReference(ECC.Core.DataContext.Contact.EntityLogicalName, localPluginContext.Target.Id),
                                ofm_facility = new EntityReference(Account.EntityLogicalName, record.Id)
                            };

                            CreateRequest createRequest = new CreateRequest { Target = entity };
                            crmContext.Execute(createRequest);
                        }
                    });

                    localPluginContext.Trace("Completed with no errors.");
                }
            }
        }
    }
}