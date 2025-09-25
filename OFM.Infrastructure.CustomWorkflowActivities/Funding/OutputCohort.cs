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
        [ReferenceTarget("ofm_cohort")]
        public OutArgument<EntityReference> cohort { get; set; }

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
                EntityReference cohortNum = null;

                RetrieveRequest applicationRequest = new RetrieveRequest
                {
                    ColumnSet = new ColumnSet(new string[] { ofm_application.Fields.ofm_summary_submittedon, ofm_application.Fields.ofm_facility }),
                    Target = new EntityReference(application.LogicalName, application.Id)
                };

                Entity d365Application = ((RetrieveResponse)service.Execute(applicationRequest)).Entity;
                if (d365Application != null && d365Application.Attributes.Count > 0 && d365Application.Attributes.Contains(ofm_application.Fields.ofm_summary_submittedon))
                {
                    var submittedOn = d365Application.GetAttributeValue<DateTime>(ofm_application.Fields.ofm_summary_submittedon);
                    var originalFacilityID = d365Application.GetAttributeValue<EntityReference>(ofm_application.Fields.ofm_facility)?.Id;
                    string facilityID = String.Empty;
                    if (originalFacilityID != null)
                    {
                        facilityID = originalFacilityID.ToString().Replace("{", "").Replace("}", "");
                    }

                    //Get the intake based on application submitted on time
                    //Intakes will not be overlapped, the result is unique (Should only be in 1 Limited Intake or 1 Open Intake; can't be both)
                    //Intake type 1 = Open, 2 = Limited


                    //Get Open Intake
                    var fetchXMLOpenIntake = $@"<fetch>
                                                    <entity name=""ofm_intake"">
                                                    <attribute name=""ofm_cohortid"" />
                                                    <attribute name=""ofm_caption"" />
                                                    <attribute name=""ofm_end_date"" />
                                                    <attribute name=""ofm_start_date"" />
                                                    <attribute name=""statecode"" />
                                                    <attribute name=""statuscode"" />
                                                    <filter>
                                                        <condition attribute=""ofm_start_date"" operator=""le"" value=""{submittedOn}"" />
                                                        <condition attribute=""ofm_end_date"" operator=""ge"" value=""{submittedOn}"" />
                                                        <condition attribute=""statuscode"" operator=""eq"" value=""1"" />
                                                        <condition attribute=""ofm_intake_type"" operator=""eq"" value=""1"" />
                                                    </filter>
                                                    </entity>
                                                </fetch>";

                    //Get Limited Intake
                    var fetchXMLLimitedIntake = $@"<fetch>
                                                      <entity name=""ofm_intake"">
                                                        <attribute name=""ofm_cohortid"" />
                                                        <attribute name=""ofm_intake_type"" />
                                                        <attribute name=""ofm_caption"" />
                                                        <attribute name=""ofm_end_date"" />
                                                        <attribute name=""ofm_start_date"" />
                                                        <attribute name=""statecode"" />
                                                        <attribute name=""statuscode"" />
                                                        <filter>
                                                          <condition attribute=""ofm_start_date"" operator=""le"" value=""{submittedOn}"" />
                                                          <condition attribute=""ofm_end_date"" operator=""ge"" value=""{submittedOn}"" />
                                                          <condition attribute=""statuscode"" operator=""eq"" value=""1"" />
                                                          <condition attribute=""ofm_intake_type"" operator=""eq"" value=""2"" />
                                                        </filter>
                                                        <link-entity name=""ofm_facility_intake"" from=""ofm_intake"" to=""ofm_intakeid"" link-type=""inner"" alias=""facility"">
                                                          <attribute name=""ofm_facility"" />
                                                          <filter>
                                                            <condition attribute=""ofm_facility"" operator=""eq"" value=""{facilityID}"" />
                                                          </filter>
                                                        </link-entity>
                                                      </entity>
                                                    </fetch>";

                    EntityCollection openIntake = service.RetrieveMultiple(new FetchExpression(fetchXMLOpenIntake));
                    EntityCollection limitedIntake = service.RetrieveMultiple(new FetchExpression(fetchXMLLimitedIntake));

                    if (openIntake != null && limitedIntake != null && openIntake.Entities.Count > 0 && limitedIntake.Entities.Count > 0)
                    {
                        //Open and Limited Intake should not overlap
                        tracingService.Trace("Multiple Intakes Matched");
                    }
                    else if (openIntake != null && openIntake.Entities.Count == 1)
                    {
                        cohortNum = openIntake[0].GetAttributeValue<EntityReference>("ofm_cohortid");

                        cohort.Set(executionContext, cohortNum);
                        tracingService.Trace("{0}{1}", "Open Intake: ", openIntake[0].GetAttributeValue<string>("ofm_caption"));
                        tracingService.Trace("{0}{1}", "Cohort: ", cohortNum?.Name.ToString());
                    }
                    else if (limitedIntake != null && limitedIntake.Entities.Count == 1)
                    {
                        cohortNum = limitedIntake[0].GetAttributeValue<EntityReference>("ofm_cohortid");

                        cohort.Set(executionContext, cohortNum);
                        tracingService.Trace("{0}{1}", "Closed Intake: ", limitedIntake[0].GetAttributeValue<string>("ofm_caption"));
                        tracingService.Trace("{0}{1}", "Cohort: ", cohortNum?.Name.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}