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

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: OutputStartAndEndDate", DateTime.Now.ToLongTimeString());
            var recordId = application.Get(executionContext);
            try
            {
                RetrieveRequest request = new RetrieveRequest();
                request.ColumnSet = new ColumnSet(new string[] { ofm_application.Fields.ofm_summary_submittedon});
                request.Target = new EntityReference(recordId.LogicalName, recordId.Id);

                Entity entity = ((RetrieveResponse)service.Execute(request)).Entity;
                if (entity != null && entity.Attributes.Count > 0 && entity.Attributes.Contains(ofm_application.Fields.ofm_summary_submittedon))
                {
                    var dateSubmittedOn = ((ofm_application)entity).ofm_summary_submittedon;

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

                    var convertedDateTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)dateSubmittedOn, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));

                    var day = Convert.ToDateTime(convertedDateTime).Day;
                    var finalDate = new DateTime();
                    if (day < 15)
                    {
                        finalDate = convertedDateTime.AddMonths(1);
                        finalDate = new DateTime(finalDate.Year, finalDate.Month, 1, finalDate.Hour, finalDate.Minute, finalDate.Second);
                    }
                    else
                    {
                        finalDate = convertedDateTime.AddMonths(2);
                        finalDate = new DateTime(finalDate.Year, finalDate.Month, 1, finalDate.Hour, finalDate.Minute, finalDate.Second);
                    }
                    var intermediateDate = finalDate.AddYears(3).AddDays(-1);
                    var end_date = new DateTime(intermediateDate.Year, intermediateDate.Month, intermediateDate.Day, 23, 59, 0);
                    startDate.Set(executionContext, finalDate);
                    endDate.Set(executionContext, end_date);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}