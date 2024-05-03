using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace OFM.Infrastructure.CustomWorkflowActivities.Application
{
    public sealed class AutoAssignSupplmentary : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            
            var applicationRecordId = context.PrimaryEntityId;
            tracingService.Trace("Begin auto assign supplementary, application id: {0}", applicationRecordId);
            try
            {
                Entity entity = service.Retrieve("ofm_application", applicationRecordId, new ColumnSet("ownerid", "statuscode"));
                OptionSetValue statusReason = entity.GetAttributeValue<OptionSetValue>("statuscode");
                int statusReasonValue = statusReason.Value;
                tracingService.Trace("Checking condtions StatusReason value:{0}", statusReasonValue);
                
                //check if the application is not approved - Submitted, In Review, Awaiting Provider
                if (entity != null && entity.Attributes.Count > 0 && (statusReasonValue == (int)ECC.Core.DataContext.ofm_application_StatusCode.Submitted || statusReasonValue == (int)ECC.Core.DataContext.ofm_application_StatusCode.InReview || statusReasonValue == (int)ECC.Core.DataContext.ofm_application_StatusCode.AwaitingProvider))
                {
                    // get related supplementary applications
                    //statecode should be Active(0) and status should not be Draft(1) 
                    var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                <fetch>
                                  <entity name=""ofm_allowance"">
                                    <filter>
                                      <condition attribute=""statecode"" operator=""eq"" value=""{(int)ECC.Core.DataContext.ofm_allowance_statecode.Active}"" />
                                      <condition attribute=""statuscode"" operator=""ne"" value=""{(int)ECC.Core.DataContext.ofm_allowance_StatusCode.Draft}"" />
                                    </filter>
                                    <link-entity name=""ofm_application"" from=""ofm_applicationid"" to=""ofm_application"">
                                      <filter>
                                        <condition attribute=""ofm_applicationid"" operator=""eq"" value=""{applicationRecordId}"" />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>";
                    EntityCollection supplementaryApplications = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    
                    if (supplementaryApplications.Entities.Count>0) {

                        //assign the supplmentary to the application owner and change status to In review
                        foreach (var supplementary in supplementaryApplications.Entities)
                        {
                            Entity supplmentaryTable = new Entity("ofm_allowance");
                            supplmentaryTable.Id = supplementary.Id;
                            supplmentaryTable["statuscode"] = new OptionSetValue((int)ECC.Core.DataContext.ofm_allowance_StatusCode.InReview);
                            supplmentaryTable["ownerid"] = entity.GetAttributeValue<EntityReference>("ownerid");
                            service.Update(supplmentaryTable);
                        }
                    }
                }
                tracingService.Trace("Workflow activity end.");
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}
