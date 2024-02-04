using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace OFM.Infrastructure.CustomWorkflowActivities.Supplementary
{
    public sealed class OutputStartAndEndDate : CodeActivity
    {
        [ReferenceTarget("ofm_allowance")]
        [RequiredArgument]
        [Input("Supplementary")]
        public InArgument<EntityReference> supplementary { get; set; }

        [Output("Start Date")]
        public OutArgument<DateTime> startDate { get; set; }

        [Output("End Date")]
        public OutArgument<DateTime> endDate { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: Supplementary OutputStartAndEndDate", DateTime.Now.ToLongTimeString());
            var recordId = supplementary.Get(executionContext);
            try
            {
                RetrieveRequest request = new RetrieveRequest();
                request.ColumnSet = new ColumnSet(new string[] { "ofm_submittedon" });
                request.Target = new EntityReference(recordId.LogicalName, recordId.Id);

                Entity entity = ((RetrieveResponse)service.Execute(request)).Entity;
                if (entity != null && entity.Attributes.Count > 0 && entity.Attributes.Contains("ofm_submittedon"))
                {
                    var dateSubmittedOn = entity.GetAttributeValue<DateTime>("ofm_submittedon");

                    RetrieveRequest timeZoneCode = new RetrieveRequest();
                    timeZoneCode.ColumnSet = new ColumnSet(new string[] { UserSettings.Fields.timezonecode });
                    timeZoneCode.Target = new EntityReference(UserSettings.EntityLogicalName, context.InitiatingUserId);
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

                    var convertedDateTime = TimeZoneInfo.ConvertTimeFromUtc(dateSubmittedOn, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));


                    var finalDate = new DateTime();

                    //Start date is the month following application
                    //(apply Feb 2 start is April 1)
                    //(apply Jan 31 start is Mar 1)

                    finalDate = convertedDateTime.AddMonths(2);
                    finalDate = new DateTime(finalDate.Year, finalDate.Month, 1, finalDate.Hour, finalDate.Minute, finalDate.Second);

                    startDate.Set(executionContext, finalDate);
                    endDate.Set(executionContext, finalDate.AddYears(1));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}