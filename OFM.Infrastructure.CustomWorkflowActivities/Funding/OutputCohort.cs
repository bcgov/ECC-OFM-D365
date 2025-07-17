using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Workflow.Runtime.Tracking;

namespace OFM.Infrastructure.CustomWorkflowActivities.Funding
{
    public sealed class OutputCohort : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }

        [Output("Cohort")]
        public OutArgument<string> cohort { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: OutputCohort", DateTime.Now.ToLongTimeString());
            var application = this.application.Get(executionContext);
            try
            {
                var cohortNum = String.Empty;
                
                RetrieveRequest applicationRequest = new RetrieveRequest
                {
                    ColumnSet = new ColumnSet(new string[] { ofm_application.Fields.ofm_summary_submittedon }),
                    Target = new EntityReference(application.LogicalName, application.Id)
                };

                Entity d365Application = ((RetrieveResponse)service.Execute(applicationRequest)).Entity;
                if (d365Application != null && d365Application.Attributes.Count > 0 && d365Application.Attributes.Contains(ofm_application.Fields.ofm_summary_submittedon))
                {
                    var submittedOn = d365Application.GetAttributeValue<DateTime>(ofm_application.Fields.ofm_summary_submittedon);

                    //Get the intake based on application submitted on time, intake could be closed when funding is approved so status not in the filter
                    //Intakes will not be overlapped, the result is unique
                    var fetchXMLIntake = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                                <fetch>
                                                    <entity name=""ofm_intake"">
                                                    <attribute name=""ofm_cohort"" />
                                                    <attribute name=""ofm_caption"" />
                                                    <attribute name=""ofm_end_date"" />
                                                    <attribute name=""ofm_start_date"" />
                                                    <attribute name=""statecode"" />
                                                    <attribute name=""statuscode"" />
                                                    <filter>
                                                        <condition attribute=""ofm_start_date"" operator=""le"" value=""{submittedOn}"" />
                                                        <condition attribute=""ofm_end_date"" operator=""ge"" value=""{submittedOn}"" />
                                                    </filter>
                                                    </entity>
                                                </fetch>";

                    EntityCollection intake = service.RetrieveMultiple(new FetchExpression(fetchXMLIntake));
                    if(intake != null && intake.Entities.Count > 0)
                    {
                        cohortNum = intake[0].GetAttributeValue<string>("ofm_cohort");
                    }

                    cohort.Set(executionContext, cohortNum);
                    tracingService.Trace("{0}{1}", "Intake", intake[0].GetAttributeValue<string>("ofm_caption"));
                    tracingService.Trace("{0}{1}", "Cohort", cohortNum);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}