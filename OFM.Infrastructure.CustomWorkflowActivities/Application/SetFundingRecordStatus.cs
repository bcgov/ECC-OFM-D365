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
                    //Recalculate Start Date and End Date for FA, rule: Adding two month (Verified anytime between January 1-31, the funding details record start date will be March 1.)
                    if (fundingRecords.Entities.Count > 0 && fundingRecords[0] != null)
                    {
                        var id = fundingRecords[0].Id;
                        tracingService.Trace("\nActive funding record to be updated with FA Review status: " + id);

                        //Get the current time
                        RetrieveRequest timeZoneCode = new RetrieveRequest
                        {
                            ColumnSet = new ColumnSet(new string[] { UserSettings.Fields.timezonecode }),
                            Target = new EntityReference(UserSettings.EntityLogicalName, context.InitiatingUserId)
                        };

                        Entity timeZoneResult = ((RetrieveResponse)service.Execute(timeZoneCode)).Entity;

                        var timeZoneQuery = new QueryExpression()
                        {
                            EntityName = TimeZoneDefinition.EntityLogicalName,
                            ColumnSet = new ColumnSet(TimeZoneDefinition.Fields.standardname),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression(TimeZoneDefinition.Fields.timezonecode, ConditionOperator.Equal,((UserSettings)timeZoneResult).timezonecode)
                                }
                            }
                        };

                        var result = service.RetrieveMultiple(timeZoneQuery);

                        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo
                            .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                            .standardname)).FirstOrDefault().ToString()));

                        //Start Date: Adding two months to current time
                       var calculatedStartDate = localTime.AddMonths(2);
                       calculatedStartDate = new DateTime(calculatedStartDate.Year, calculatedStartDate.Month, 1, 0, 0, 0);


                        var tempDate = new DateTime();
                        var cutOffDate = new DateTime(2024, 10, 01); //FA Start Date after Oct 1, 2024 should have 2 year term instead of 3)
                        tracingService.Trace("Funding Start Date: {0}", calculatedStartDate);
                        tracingService.Trace("Funding CutOff Date: {0}. Funding start date after October 1, 2024, will have 2 years instead of 3 years funding terms", cutOffDate);

                        if (calculatedStartDate > cutOffDate)
                        {
                            tempDate = calculatedStartDate.AddYears(2).AddDays(-1);
                        }
                        else
                        {
                            tempDate = calculatedStartDate.AddYears(3).AddDays(-1);
                        }

                        var calculatedEndDate = new DateTime(tempDate.Year, tempDate.Month, tempDate.Day, 23, 59, 0);


                        var entityToUpdate = new ofm_funding
                        {
                            Id = id,
                            statuscode = ofm_funding_StatusCode.FAReview,
                            ofm_start_date = calculatedStartDate,
                            ofm_end_date = calculatedEndDate
                        };
                        service.Update(entityToUpdate);

                        tracingService.Trace("\nChange sucessfully active funding detail record status from Draft to FA review and Recalculate start date");
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