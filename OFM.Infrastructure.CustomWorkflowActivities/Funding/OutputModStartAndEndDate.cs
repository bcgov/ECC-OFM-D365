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
    public sealed class OutputModStartAndEndDate : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }

        [ReferenceTarget("ofm_funding")]
        [RequiredArgument]
        [Input("Funding")]
        public InArgument<EntityReference> funding { get; set; }

        [Output("Start Date")]
        public OutArgument<DateTime> startDate { get; set; }

        [Output("End Date")]
        public OutArgument<DateTime> endDate { get; set; }

        [Output("Room Split")]
        public OutArgument<bool> applyRoomSplit { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: OutputMODStartAndEndDate", DateTime.Now.ToLongTimeString());
            var application = this.application.Get(executionContext);
            var funding = this.funding.Get(executionContext);

            try
            {
                var fundingRequest = new QueryExpression()
                {
                    EntityName = ofm_funding.EntityLogicalName,
                    ColumnSet = new ColumnSet(new string[] { ofm_funding.Fields.ofm_end_date, ofm_funding.Fields.ofm_application, ofm_funding.Fields.ofm_version_number }),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                                {
                                    new ConditionExpression(ofm_funding.Fields.ofm_application, ConditionOperator.Equal, application.Id),
                                    new ConditionExpression("ofm_fundingid", ConditionOperator.NotEqual, funding.Id)
                                }
                    },
                    Orders =
                    {
                        new OrderExpression("ofm_version_number", OrderType.Descending)
                    }
                };

                var d365Funding = service.RetrieveMultiple(fundingRequest);

                tracingService.Trace("{0}{1}", "Fundings:", d365Funding.Entities.Count);
                tracingService.Trace("{0}{1}", "Latest Funding: ", d365Funding[0].Id);

                if (d365Funding != null && d365Funding.Entities.Count > 0 && d365Funding[0].Attributes.Contains(ofm_funding.Fields.ofm_end_date))
                {
            
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

                    var calculatedStartDate = new DateTime();
                    if (localTime.Day < 15)
                    {
                        calculatedStartDate = localTime.AddMonths(1);
                        calculatedStartDate = new DateTime(calculatedStartDate.Year, calculatedStartDate.Month, 1, 0, 0, 0);
                    }
                    else
                    {
                        calculatedStartDate = localTime.AddMonths(2);
                        calculatedStartDate = new DateTime(calculatedStartDate.Year, calculatedStartDate.Month, 1, 0, 0, 0);
                    }

                    startDate.Set(executionContext, calculatedStartDate);
                    endDate.Set(executionContext, d365Funding[0].Attributes[ofm_funding.Fields.ofm_end_date]);

                    #region Room Split: Also returns the room split condition to follow-up with the room-split setup the funding record

                    RetrieveRequest applicationRequest = new RetrieveRequest
                    {
                        ColumnSet = new ColumnSet(new string[] { ofm_application.Fields.ofm_summary_submittedon }),
                        Target = new EntityReference(application.LogicalName, application.Id)
                    };

                    Entity d365Application = ((RetrieveResponse)service.Execute(applicationRequest)).Entity;
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

                    applyRoomSplit.Set(executionContext, licenceDetails.Entities.Count > 0);

                    #endregion
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}