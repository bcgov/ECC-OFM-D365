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
    public class GenerateFacilityPremissions : PluginBase
    {
        public GenerateFacilityPremissions(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GenerateFacilityPremissions))
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
            //localPluginContext.Trace($"localPluginContext.PluginExecutionContext.Stage {localPluginContext.PluginExecutionContext.Stage}");

            if (localPluginContext.Target.Contains(ECC.Core.DataContext.Contact.Fields.ParentCustomerId))
            {
                // Getting latest data to get the value
                //var oldOrganization = localPluginContext.Current.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.ParentCustomerId);
                var newOrganization = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.ParentCustomerId);

                //localPluginContext.Trace($"Old organization {oldOrganization.Id}");
                //localPluginContext.Trace($"New organization {newOrganization.Id}");

                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    var oldPermissions = crmContext.OfM_BcEId_FacilitySet.Where(permission => permission.OfM_BcEId.Id == localPluginContext.Target.Id).ToList();
                    localPluginContext.Trace($"Total Old permissions count {oldPermissions.Count}");

                    oldPermissions.ForEach(record =>
                    {
                        if (record.Attributes.Contains(OfM_BcEId_Facility.Fields.StatusCode))
                        {
                            var entity = new OfM_BcEId_Facility
                            {
                                Id = record.Id,
                                StateCode = OfM_BcEId_Facility_StateCode.Inactive,
                                StatusCode = OfM_BcEId_Facility_StatusCode.Inactive
                            };

                            UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                            crmContext.Execute(updateRequest);

                            //localPluginContext.Trace($"Facility {record.OfM_Facility.Name} removed from permissions for contact {localPluginContext.Target.Id}");
                        }
                    });

                    var newFacilityPermissions = crmContext.AccountSet.Where(facility => facility.ParentAccountId.Id == newOrganization.Id).ToList();
                    localPluginContext.Trace($"Total New child facilities count {newFacilityPermissions.Count}");

                    newFacilityPermissions.ForEach(record =>
                    {
                        if (record.Attributes.Contains(Account.Fields.StatusCode) &&
                            record.GetAttributeValue<OptionSetValue>(Account.Fields.StatusCode).Value == Convert.ToInt32(Account_StatusCode.Active))
                        {
                            var entity = new OfM_BcEId_Facility
                            {
                                OfM_BcEId = new EntityReference(ECC.Core.DataContext.Contact.EntityLogicalName, localPluginContext.Target.Id),
                                OfM_Facility = new EntityReference(Account.EntityLogicalName, record.Id)
                            };

                            CreateRequest createRequest = new CreateRequest { Target = entity };
                            crmContext.Execute(createRequest);

                            //localPluginContext.Trace($"Facility {record.Id} added to permissions for contact {localPluginContext.Target.Id}");
                        }
                    });

                    localPluginContext.Trace("Completed with no errors.");
                }
            }
        }
    }
}