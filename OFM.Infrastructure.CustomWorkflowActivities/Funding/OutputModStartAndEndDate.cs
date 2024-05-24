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
        [ReferenceTarget("ofm_funding")]
        [RequiredArgument]
        [Input("Initial Funding")]
        public InArgument<EntityReference> initialFunding { get; set; }

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
            var funding = initialFunding.Get(executionContext);

            try
            {
                RetrieveRequest fundingRequest = new RetrieveRequest
                {
                    ColumnSet = new ColumnSet(new string[] { ofm_funding.Fields.ofm_end_date, ofm_funding.Fields.ofm_application }),
                    Target = new EntityReference(funding.LogicalName, funding.Id)
                };

                Entity d365Funding = ((RetrieveResponse)service.Execute(fundingRequest)).Entity;

                if (d365Funding != null && d365Funding.Attributes.Count > 0 && d365Funding.Attributes.Contains(ofm_funding.Fields.ofm_end_date))
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
                    endDate.Set(executionContext, ((ofm_funding)d365Funding).ofm_end_date);

                    #region Room Split: Also returns the room split condition to follow-up with the room-split setup the funding record

                    var fetchXMLLicenceDetails = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                                <fetch>
                                                  <entity name=""ofm_application"">
                                                    <attribute name=""ofm_applicationid"" />
                                                    <order attribute=""ofm_application"" descending=""false"" />
                                                    <filter type=""and"">
                                                      <condition attribute=""ofm_applicationid"" operator=""eq"" uitype=""ofm_application"" value=""{((ofm_funding)d365Funding)?.ofm_application.Id}"" />
                                                    </filter>
                                                    <link-entity name=""account"" from=""accountid"" to=""ofm_facility"" link-type=""inner"" alias=""am"">
                                                      <link-entity name=""ofm_licence"" from=""ofm_facility"" to=""accountid"" link-type=""inner"" alias=""an"">
                                                        <link-entity name=""ofm_licence_detail"" from=""ofm_licence"" to=""ofm_licenceid"" link-type=""inner"" alias=""ao"">
                                                          <filter type=""and"">
                                                            <condition attribute=""ofm_apply_room_split_condition"" operator=""eq"" value=""1"" />
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