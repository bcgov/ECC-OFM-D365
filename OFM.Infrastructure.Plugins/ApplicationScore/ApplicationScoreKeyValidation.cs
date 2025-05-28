using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Services;

namespace OFM.Infrastructure.Plugins.Application
{
    /// <summary>
    /// Validation Plugin for Application Score Parameter
    /// </summary>
    public class ApplicationScoreKeyValidation : PluginBase
    {
        public ApplicationScoreKeyValidation(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(UpdateApplicationNumber))
        {
           
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            try
            {
                if (localPluginContext == null)
                {
                    throw new InvalidPluginExecutionException(nameof(localPluginContext), new Dictionary<string, string>() { ["failedRecordId"] = localPluginContext.Target.Id.ToString() });
                }

                localPluginContext.Trace("Start ApplicationScoreKeyValidation Plug-in");

                // Check if the target entity is ApplicationScoringParameters
                if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity entity)
                {
                    localPluginContext.Trace("ofm_application_score_parameter entity is passed");
                    if (entity.LogicalName != "ofm_application_score_parameter") return;
                    var entity2 = localPluginContext.PluginUserService.Retrieve("ofm_application_score_parameter", entity.Id, new ColumnSet("ofm_comparison_operator", "ofm_score", "ofm_key", "ofm_application_score_calculator"));
                    QueryByAttribute queryIntake = new QueryByAttribute("ofm_intake");
                    queryIntake.ColumnSet = new ColumnSet("ofm_start_date","ofm_end_date");
                    queryIntake.AddAttributeValue("ofm_application_score_calculator", entity2.GetAttributeValue<EntityReference>("ofm_application_score_calculator")?.Id);
                    var intakes = localPluginContext.PluginUserService.RetrieveMultiple(queryIntake);
                    localPluginContext.Trace($"total inakes are related to application score calculator: {intakes.Entities.Count}");
                    if (intakes.Entities.Count > 0)
                    {
                        
                        foreach (var intake in intakes.Entities)
                        {
                            if (intake.GetAttributeValue<DateTime>("ofm_start_date") == DateTime.MinValue)
                                continue;


                            if (((DateTime)intake["ofm_start_date"]) < DateTime.UtcNow && (!intake.Contains("ofm_end_date") || intake.GetAttributeValue<DateTime>("ofm_end_date") == DateTime.MinValue))
                            {
                                throw new InvalidPluginExecutionException("Intake for the Application Score Calculator is in progress and Application Score Parameters cannot be modified");
                            }
                            if (((DateTime)intake["ofm_start_date"]) < DateTime.UtcNow && intake.Contains("ofm_end_date") && intake.GetAttributeValue<DateTime>("ofm_end_date") > DateTime.UtcNow)
                            {

                                throw new InvalidPluginExecutionException("Intake for the Application Score Calculator is in progress and Application Score Parameters cannot be modified");

                            }
                        }
                    }

                    
                    
                    // Extract ofm_key and ofm_comparisonoperator values
                    string key = entity2.Contains("ofm_key") ? entity2["ofm_key"]?.ToString() : null;
                    int? comparisonOperator = entity2.Contains("ofm_comparison_operator")
                        ? entity2.GetAttributeValue<OptionSetValue>("ofm_comparison_operator")?.Value
                        : null;

                    // Ensure both fields are provided
                    if (key == null || comparisonOperator == null)
                    {
                        throw new InvalidPluginExecutionException("Key and Comparison Operator are required.");
                    }
                    /*
                     * { 1, "Equal" },
            { 2, "LessThan" },
            { 3, "LessThanOrEqual" },
            { 4, "GreaterThan" },
            { 5, "GreaterThanOrEqual" },
            { 6, "Contains" },
            { 7, "Between" }
                     */

                    // Validate ofm_key based on comparison operator
                    switch (comparisonOperator)
                    {
                        // Equal, NotEqual, Contains: Key must be non-empty text
                        case 1: // Equal
                        case 8: // NotEqual
                        case 6: // Contains
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                throw new InvalidPluginExecutionException("Key cannot be empty for Equal, NotEqual, or Contains operators.");
                            }
                            break;

                        // LessEqual, LessThan, GreaterThan, GreaterEqual: Key must be a valid decimal
                        case 3: // LessEqual
                        case 2: // LessThan
                        case 4: // GreaterThan
                        case 5: // GreaterEqual
                            if (!decimal.TryParse(key, out _))
                            {
                                throw new InvalidPluginExecutionException("Key must be a valid number/decimal for LessEqual, LessThan, GreaterThan, or GreaterEqual operators.");
                            }
                            break;
                        // Between: Key must be two comma-separated decimals (low,high) with low < high
                        case 7: // Between
                            var keys = key.Split(',');
                            if (keys.Length != 2 || !decimal.TryParse(keys[0], out var low) || !decimal.TryParse(keys[1], out var high) || low >= high)
                            {
                                throw new InvalidPluginExecutionException("Key must be two comma-separated decimal numbers (low,high) with low < high for Between operator.");
                            }
                            break;

                        default:
                            throw new InvalidPluginExecutionException($"Invalid Comparison Operator value: {comparisonOperator}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error for debugging
                localPluginContext?.Trace($"Error occured while processing ApplicationScoreKeyValidation plugin: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error validating Key: {ex.Message}", ex);
            }


        }
    }
}