using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace OFM.Infrastructure.CustomWorkflowActivities.Funding
{
    public sealed class OutputStartAndEndDate : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }

        [Output("Start Date")]
        public OutArgument<DateTime> startDate { get; set; }

        [Output("End Date")]
        public OutArgument<DateTime> endDate { get; set; }

        [Output("Room Split")]
        public OutArgument<bool> roomSplit { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0} {1}", "Start Custom Workflow Activity: OutputStartAndEndDate", DateTime.Now.ToLongTimeString());
            var application = this.application.Get(executionContext);
            try
            {
                RetrieveRequest applicationRequest = new RetrieveRequest
                {
                    ColumnSet = new ColumnSet(new string[] { ofm_application.Fields.ofm_summary_submittedon }),
                    Target = new EntityReference(application.LogicalName, application.Id)
                };

                Entity d365Application = ((RetrieveResponse)service.Execute(applicationRequest)).Entity;
                if (d365Application != null && d365Application.Attributes.Count > 0 && d365Application.Attributes.Contains(ofm_application.Fields.ofm_summary_submittedon))
                {
                    var fetchXMLLicenceDetails = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                                <fetch>
                                                  <entity name=""ofm_application"">
                                                    <attribute name=""ofm_applicationid"" />
                                                    <order attribute=""ofm_application"" descending=""false"" />
                                                    <filter type=""and"">
                                                      <condition attribute=""ofm_applicationid"" operator=""eq"" uitype=""ofm_application"" value=""{application.Id}"" />
                                                    </filter>
                                                    <link-entity name=""account"" from=""accountid"" to=""ofm_facility"" link-type=""inner"" alias=""am"">
                                                      <link-entity name=""ofm_licence"" from=""ofm_facility"" to=""accountid"" link-type=""inner"" alias=""an"">
                                                        <filter type=""and"">
                                                            <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                                            <filter type=""or"">
                                                                <filter type=""and"">
                                                                    <condition attribute=""ofm_end_date"" operator=""null"" />
                                                                    <condition attribute=""ofm_start_date"" operator=""on-or-before"" value=""{d365Application.Attributes[ofm_application.Fields.ofm_summary_submittedon]}"" />
                                                                </filter>
                                                                <filter type=""and"">
                                                                    <condition attribute=""ofm_end_date"" operator=""on-or-after"" value=""{d365Application.Attributes[ofm_application.Fields.ofm_summary_submittedon]}"" />
                                                                    <condition attribute=""ofm_start_date"" operator=""on-or-before"" value=""{d365Application.Attributes[ofm_application.Fields.ofm_summary_submittedon]}"" />
                                                                </filter>
                                                            </filter>
                                                        </filter>
                                                        <link-entity name=""ofm_licence_detail"" from=""ofm_licence"" to=""ofm_licenceid"" link-type=""inner"" alias=""ao"">
                                                          <filter type=""and"">
                                                            <condition attribute=""ofm_apply_room_split_condition"" operator=""eq"" value=""1"" />
                                                            <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                                          </filter>
                                                        </link-entity>
                                                      </link-entity>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>";

                    EntityCollection licenceDetails = service.RetrieveMultiple(new FetchExpression(fetchXMLLicenceDetails));
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

                    var localTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)((ofm_application)d365Application)?.ofm_summary_submittedon, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));

                    var day = Convert.ToDateTime(localTime).Day;
                    var calculatedStartDate = new DateTime();
                    if (day < 15)
                    {
                        calculatedStartDate = localTime.AddMonths(1);
                        calculatedStartDate = new DateTime(calculatedStartDate.Year, calculatedStartDate.Month, 1, 0, 0, 0);
                    }
                    else
                    {
                        calculatedStartDate = localTime.AddMonths(2);
                        calculatedStartDate = new DateTime(calculatedStartDate.Year, calculatedStartDate.Month, 1, 0, 0, 0);
                    }
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
                    tracingService.Trace("Funding End Date: {0}", calculatedEndDate);

                    startDate.Set(executionContext, calculatedStartDate);
                    endDate.Set(executionContext, calculatedEndDate);
                    roomSplit.Set(executionContext, licenceDetails.Entities.Count > 0);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}