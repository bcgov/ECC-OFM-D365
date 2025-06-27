using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.Plugins.Account;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFM.Infrastructure.Plugins.Licence
{
    public class StorePreviousLicenceValue : PluginBase
    {
        public StorePreviousLicenceValue(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(StorePreviousLicenceValue))
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

            localPluginContext.Trace("Start StorePreviousLicenceNumber Plug-in");

            if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity)
            {
                Entity entity = (Entity)localPluginContext.PluginExecutionContext.InputParameters["Target"];
                Entity preImageLicence = localPluginContext.PluginExecutionContext.PreEntityImages["PreImage"];
                Entity postImageLicence = localPluginContext.PluginExecutionContext.PostEntityImages["PostImage"];

                if (preImageLicence != null && postImageLicence != null)
                {

                    string licenceId = preImageLicence.GetAttributeValue<Guid>("ofm_licenceid").ToString().Replace("{", "").Replace("}", "");
                    localPluginContext.Trace("***Debug application:*** " + licenceId);

                    if (preImageLicence.GetAttributeValue<string>("ofm_licence") != postImageLicence.GetAttributeValue<string>("ofm_licence"))
                    {
                        //Store Previous Licence Number for FA Mod PDF generation
                        Entity licenceUpdate = new Entity("ofm_licence");
                        licenceUpdate.Id = preImageLicence.GetAttributeValue<Guid>("ofm_licenceid");
                        licenceUpdate["ofm_previous_licence_number"] = preImageLicence.GetAttributeValue<string>("ofm_licence");
                        
                        localPluginContext.PluginUserService.Update(licenceUpdate);
                        localPluginContext.Trace("Saved Previous Licence Number");

                    }
                    else
                    {
                        localPluginContext.Trace("No changes to Licence Number");
                    }
                }
            }
        }
    }
}
