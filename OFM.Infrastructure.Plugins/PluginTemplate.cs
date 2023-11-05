using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.Plugins;
using System;
using System.Collections.Generic;

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
                throw new InvalidPluginExecutionException(nameof(localPluginContext), new Dictionary<string, string>() { ["failedRecordId"] = localPluginContext.Target.Id.ToString() });
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

            //var organizationId = localPluginContext.Target.GetAttributeValue<EntityReference>(ECC.Core.DataContext.Contact.Fields.ParentCustomerId).Id;

            //if (localPluginContext.Target.Contains("ofm_qty") || localPluginContext.Target.Contains("ofm_price"))
            //{
            //    // Getting latest data to get the value
            //    var qty = localPluginContext.Latest.GetAttributeValue<int?>("ofm_qty").GetValueOrDefault();
            //    var price = (localPluginContext.Latest.GetAttributeValue<Money>("ofm_price") ?? new Money(0)).Value;
            //    var result = qty * price;

            //    // Set the Target + Latest
            //    localPluginContext.Set(ofm_total", result);
            //}

            //https://learn.microsoft.com/en-us/power-apps/developer/data-platform/write-plugin-multiple-operation?tabs=multiple

            // Verify input parameters
            if (context.InputParameters.Contains("Targets") && context.InputParameters["Targets"] is EntityCollection entityCollection)
            {
                localPluginContext.Trace($"Input parameters verified.");

                // Verify expected entity images from step registration
                if (context.PreEntityImagesCollection.Length == entityCollection.Entities.Count)
                {
                    localPluginContext.Trace($"expected entity images from step registration verified. entityCollection.Entities.Count {entityCollection.Entities.Count}");

                    int count = 0;
                    foreach (Entity entity in entityCollection.Entities)
                    {
                        EntityImageCollection entityImages = context.PreEntityImagesCollection[count];

                        // Verify expected entity image from step registration
                        if (entityImages.TryGetValue("example_preimage", out Entity preImage))
                        {
                            bool entityContainsSampleName = entity.Contains("sample_name");
                            bool entityImageContainsSampleName = preImage.Contains("sample_name");
                            bool entityImageContainsSampleDescription = preImage.Contains("sample_description");
                            
                            localPluginContext.Trace($"Verifing expected entity image from step registration entity {entity.LogicalName}");

                            if (entityContainsSampleName && entityImageContainsSampleName && entityImageContainsSampleDescription)
                            {
                                // Verify that the entity 'description' values are different
                                if (entity["ofm_first_name"] != preImage["ofm_first_name"])
                                {
                                    string newName = (string)entity["description"];
                                    string oldName = (string)preImage["description"];
                                    string message = $"\\r\\n - 'description' changed from '{oldName}' to '{newName}'.";

                                    // If the 'description' is included in the update, do not overwrite it, just append to it.
                                    if (entity.Contains("description"))
                                    {
                                        entity["description"] = entity["description"] += message;
                                    }
                                    else // The sample description is not included in the update, overwrite with current value + addition.
                                    {
                                        entity["description"] = preImage["description"] += message;
                                    }

                                    // Not tracing Success for brevity. There is a limit to what tracelog can display.
                                    localPluginContext.Trace($"Appended to 'description': \"{message}\" for item {count} ");
                                }
                                else
                                {
                                    localPluginContext.Trace($"Expected entity and preImage 'sample_name' values to be different. Both are {entity["sample_name"]} for item {count}");
                                }
                            }
                            else
                            {
                                if (!entityContainsSampleName)
                                    localPluginContext.Trace($"Expected entity sample_name attribute not found for item {count}.");
                                if (!entityImageContainsSampleName)
                                    localPluginContext.Trace($"Expected preImage entity sample_name attribute not found for item {count}.");
                                if (!entityImageContainsSampleDescription)
                                    localPluginContext.Trace($"Expected preImage entity sample_description attribute not found for item {count}.");
                            }
                        }
                        else
                        {
                            localPluginContext.Trace($"Expected PreEntityImage: 'example_preimage' not found for item {count}.");
                        }

                        count++;
                    }
                }
                else
                {
                    localPluginContext.Trace($"Expected PreEntityImagesCollection to contain Entity images for each Entity.");
                }
            }
            else
            {
                if (!context.InputParameters.Contains("Targets"))
                    localPluginContext.Trace($"Expected InputParameter: 'Targets' not found.");
                if (!(context.InputParameters["Targets"] is EntityCollection))
                    localPluginContext.Trace($"Expected InputParameter: 'Targets' is not EntityCollection.");
            }
        }
    }
}
