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
    public sealed class SetFundingRecordStatus : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);

            //var recordId = context.PrimaryEntityId;
            var recordId = application.Get(executionContext).Id;

            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: Application - SetFundingRecordStatus", DateTime.Now.ToLongTimeString());
            try
            {
                ofm_application applicationRcord = (ofm_application)service.Retrieve(ofm_application.EntityLogicalName, recordId, new ColumnSet("statuscode"));
                int statusReason = applicationRcord.GetAttributeValue<OptionSetValue>("statuscode").Value;

                tracingService.Trace("Checking Application record StatusReason value:{0} ", statusReason);
                if (applicationRcord != null && applicationRcord.Attributes.Count > 0 && statusReason == (int)ofm_application_StatusCode.Verified)
                {
                    tracingService.Trace("\nThe Application Record - logical name: {0}, id:{1}", applicationRcord.LogicalName, applicationRcord.Id);
                    var fetchData = new
                    {
                        ofm_application = recordId.ToString(),
                        statecode = $"{(int)ofm_funding_statecode.Active}",
                        statuscode = $"{(int)ofm_funding_StatusCode.Draft}"
                    };
                    var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
									<fetch>
									  <entity name=""ofm_funding"">
										<attribute name=""ofm_application"" />
										<attribute name=""statecode"" />
										<attribute name=""statuscode"" />
										<attribute name=""ofm_version_number"" />
										<filter>
										  <condition attribute=""ofm_application"" operator=""eq"" value=""{fetchData.ofm_application}"" />
										  <condition attribute=""statecode"" operator=""eq"" value=""{fetchData.statecode}"" />
										  <condition attribute=""statuscode"" operator=""eq"" value=""{fetchData.statuscode}"" />
										</filter>
										<order attribute=""ofm_version_number"" descending=""true"" />
									  </entity>
									</fetch>";

                    EntityCollection fundingRecords = service.RetrieveMultiple(new FetchExpression(fetchXml));

                    //Change Active Funding record status: Draft (1) --> FA Review (3)
                    if (fundingRecords.Entities.Count > 0 && fundingRecords[0] != null)
                    {
                        var id = fundingRecords[0].Id;
                        tracingService.Trace("\nActive funding record to be updated with FA Review status: " + id);

                        var entityToUpdate = new ofm_funding
                        {
                            Id = id,
                            statuscode = ofm_funding_StatusCode.FAReview
                        };
                        service.Update(entityToUpdate);

                        tracingService.Trace("\nChange sucessfully active funding detail record status from Draft to FA review.");
                    }
                    else
                    {
                        tracingService.Trace("\nNo active funding record found.");
                    }
                }
                else
                {
                    tracingService.Trace("\nNo application record found.");
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