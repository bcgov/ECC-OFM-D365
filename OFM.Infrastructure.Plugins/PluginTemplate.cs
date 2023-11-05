using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.Plugins;
using System;

namespace OFM.Infrastructure.Plugins
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class PluginTemplate : PluginBase
    {
        public PluginTemplate(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(PluginTemplate))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            // TODO: Implement your custom business logic

            // Check for the entity on which the plugin would be registered
            //if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            //{
            //    var entity = (Entity)context.InputParameters["Target"];

            //    // Check for entity name on which this plugin would be registered
            //    if (entity.LogicalName == "account")
            //    {

            //    }
            //}

            //var organizationId = localPluginContext.Latest.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.ParentCustomerId).Id;

            //if (localPluginContext.Target.Contains("ofm_qty") || localPluginContext.Target.Contains("ofm_price"))
            //{
            //    // Getting latest data to get the value
            //    var qty = localPluginContext.Latest.GetAttributeValue<int?>("ofm_qty").GetValueOrDefault();
            //    var price = (localPluginContext.Latest.GetAttributeValue<Money>("ofm_price") ?? new Money(0)).Value;
            //    var result = qty * price;

            //    // Set the Target + Latest
            //    localPluginContext.Set(ofm_total", result);
            //}
        }
    }
}
