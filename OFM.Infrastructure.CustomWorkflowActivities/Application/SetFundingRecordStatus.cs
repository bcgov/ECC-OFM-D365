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
                Entity applicationRcord = service.Retrieve("ofm_application", recordId, new ColumnSet("statuscode"));
                int statusReason = applicationRcord.GetAttributeValue<OptionSetValue>("statuscode").Value;

                tracingService.Trace("Checking Application record StatusReason value:{0} ", statusReason);
                if (applicationRcord != null && applicationRcord.Attributes.Count > 0 && statusReason == 5)        // 5 - approved (application)
                {
					tracingService.Trace("\nThe Application Record - logical name: {0}, id:{1}", applicationRcord.LogicalName, applicationRcord.Id);
					var fetchData = new
					{
						ofm_application = recordId.ToString(),
						statecode = "0",                                                                           // 0 - active (funding)
                        statuscode = "3"                                                                           // 3 - Draft (funding)
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

                    //Change Active Funding record status: Draft (3) --> FA Review(4)
                    if (fundingRecords.Entities.Count > 0 && fundingRecords[0] != null)
                    {
						var id = fundingRecords[0].Id;
						tracingService.Trace("\nActive funding record to be updated with FA Review status: " + id);

                        Entity fundingRecordTable = new Entity("ofm_funding");
						fundingRecordTable.Id = id;
						fundingRecordTable["statuscode"] = new OptionSetValue(4);                                    // 4 - FA Review (funding)
                        service.Update(fundingRecordTable);

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
