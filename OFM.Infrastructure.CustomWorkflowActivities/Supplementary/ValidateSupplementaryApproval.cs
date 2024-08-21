using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Activities.Statements;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace OFM.Infrastructure.CustomWorkflowActivities.Supplementary
{
    public sealed class ValidateSupplementaryApproval : CodeActivity
    {

        [ReferenceTarget("ofm_allowance")]
        [RequiredArgument]
        [Input("Supplementary")]
        public InArgument<EntityReference> supplementary { get; set; }

        [Output("isFDRApproved")]
        public OutArgument<Boolean> isFDRApproved { get; set; }

      
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: Supplementary Approval Validation", DateTime.Now.ToLongTimeString());
            var recordId = supplementary.Get(executionContext);

            try
            {
                RetrieveRequest request = new RetrieveRequest
                {
                    ColumnSet = new ColumnSet(new string[] { "ofm_application", "statuscode" }),
                    Target = new EntityReference(recordId.LogicalName, recordId.Id)
                };

                Entity entity = ((RetrieveResponse)service.Execute(request)).Entity;

                if (entity != null && entity.Attributes.Contains("statuscode") && entity.Attributes.Contains("ofm_application"))
                {
                    // Get related Application ID
                    if (entity["ofm_application"] is EntityReference applicationReference)
                    {
                        //Get related Application id and FA date 
                        var applicationId = entity.GetAttributeValue<EntityReference>("ofm_application").Id;
                        tracingService.Trace("{0}", "applicationId: ", applicationId);

                        var applicationStatecode = (int)ofm_application_statecode.Active;
                        //fetch related funding agreement with application id
                        var fundingQuery = new QueryExpression("ofm_funding")
                        {

                            ColumnSet = new ColumnSet(true),
                            Criteria =
                        {
                            
                            Conditions =
                            {
                                new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, applicationStatecode)
                            }
                        }
                        };
                        tracingService.Trace("Funding query: {0}", fundingQuery);
                        var result = service.RetrieveMultiple(fundingQuery);
                        tracingService.Trace("Funding result: {0}", result.Entities.Count);
                        if (result.Entities.Count > 0)
                        {
                            tracingService.Trace("Funding count: {0}", result.Entities.Count);

                            // Check if any of the retrieved funding records have a status code of ACTIVE
                            bool hasActiveFunding = result.Entities.Any(funding =>
                            {
                                var statusCode = funding.GetAttributeValue<OptionSetValue>(ofm_funding.Fields.statuscode);
                                return statusCode != null && statusCode.Value == (int)ofm_funding_StatusCode.Active;
                            });

                            tracingService.Trace("Has Active funding for this supplementary application: {0}", hasActiveFunding);
                            isFDRApproved.Set(executionContext, hasActiveFunding);
                            tracingService.Trace("Completed custom workflow successfully");
                        }
                        else
                        {
                            tracingService.Trace("No funding records for this instance");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exception in Custom Workflow: " + ex.Message, ex);
            }


        }
    }
    
}
