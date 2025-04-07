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
    public class UpdateApplicationNumber : PluginBase
    {
        public UpdateApplicationNumber(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(UpdateApplicationNumber))
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

            localPluginContext.Trace("Start UpdateApplicationNumber Plug-in");

            if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity)
            {
                Entity entity = (Entity)localPluginContext.PluginExecutionContext.InputParameters["Target"];
                var currentImage = localPluginContext.Current;

                if (currentImage != null)
                {

                    var applicationId = entity.GetAttributeValue<Guid>(ofm_application.Fields.Id);
                    var applicationNumber = entity.GetAttributeValue<string>(ofm_application.Fields.ofm_Application);
                    var applicationType = entity.GetAttributeValue<OptionSetValue>(ofm_application.Fields.ofm_application_type);
                    localPluginContext.Trace("***Debug application:*** " + applicationId);
                    localPluginContext.Trace("***Debug applicationName:*** " + applicationNumber);
                    localPluginContext.Trace("***Debug applicationType:*** " + applicationType.Value);

                    //if application is Renewal
                    if (applicationType.Value == (int)ECC.Core.DataContext.ecc_application_type.Renewal)
                    {
                        var applicationNumArr = applicationNumber.Split('-');
                        if (applicationNumArr.Length > 0)
                        {
                            var updateApplicationNum = "APP-R-" + applicationNumArr[1];

                            //deactive the current funding record
                            Entity applicationTable = new Entity("ofm_application");
                            applicationTable.Id = localPluginContext.Target.Id;
                            applicationTable["ofm_application"] = updateApplicationNum;
                            localPluginContext.PluginUserService.Update(applicationTable);
                        }
                    }

                }
            }
        }
    }
}