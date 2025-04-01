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
    public sealed class CarryOverTopUpToMod : CodeActivity
    {
        [ReferenceTarget("ofm_application")]
        [RequiredArgument]
        [Input("Application")]
        public InArgument<EntityReference> application { get; set; }

        [ReferenceTarget("ofm_funding")]
        [RequiredArgument]
        [Input("Funding")]
        public InArgument<EntityReference> funding { get; set; }

        [RequiredArgument]
        [Input("FundingStartDate")]
        public InArgument<DateTime> funding_start_date { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: CarryOverTopUpToMod", DateTime.Now.ToLongTimeString());
            var application = this.application.Get(executionContext);
            var funding = this.funding.Get(executionContext);
            var funding_start_date = this.funding_start_date.Get(executionContext);

            //Get the active topup

            try
            {

                // Instantiate QueryExpression query
                var query = new QueryExpression("ofm_top_up_fund")
                {
                    // Add 5 columns to ofm_top_up_fund
                    ColumnSet = new ColumnSet("ofm_end_date", "ofm_funding", "ofm_programming_amount", "ofm_start_date", "statuscode"),
                    // Add filter to ofm_top_up_fund with 2 conditions
                    Criteria =
                    {
                        // Add 2 conditions to ofm_top_up_fund
                        Conditions =
                        {
                            new ConditionExpression("statuscode", ConditionOperator.Equal, ECC.Core.DataContext.ofm_top_up_fund_StatusCode.Approved),
                            new ConditionExpression("ofm_end_date", ConditionOperator.GreaterEqual, funding_start_date) //Compare the topup end date and funding mod start date
                        }
                    },
                    // Add 1 link-entity to query
                    LinkEntities =
                    {
                        // Add link-entity query_ofm_funding
                        new LinkEntity("ofm_top_up_fund", "ofm_funding", "ofm_funding", "ofm_fundingid", JoinOperator.Inner)
                        {
                            // Add 1 link-entity to query_ofm_funding
                            LinkEntities =
                            {
                                // Add link-entity query_ofm_funding_ofm_application
                                new LinkEntity(
                                    "ofm_funding",
                                    "ofm_application",
                                    "ofm_application",
                                    "ofm_applicationid",
                                    JoinOperator.Inner)
                                {
                                    // Add filter to ofm_application with 1 conditions
                                    LinkCriteria =
                                    {
                                        // Add 1 conditions to ofm_application
                                        Conditions =
                                        {
                                            new ConditionExpression("ofm_applicationid", ConditionOperator.Equal, application)
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var topups = service.RetrieveMultiple(query);

                tracingService.Trace("{0}{1}", "Topup:", topups.Entities.Count);
               
                if (topups != null && topups.Entities.Count > 0)
                {

                    var sum_of_topup_programming = topups.Entities.Sum( e => e.GetAttributeValue<Microsoft.Xrm.Sdk.Money>(ECC.Core.DataContext.ofm_top_up_fund.Fields.ofm_programming_amount).Value);
                    var sum_of_topup_total = sum_of_topup_programming; //Now only have programming envelop, will add other envelop in the future

                    tracingService.Trace("{0}{1}", "Topup Programming: ", sum_of_topup_programming);
                    tracingService.Trace("{0}{1}", "Topup Total: ", sum_of_topup_total);

                    //Update the funding
                    var entityToUpdate = new ofm_funding
                    {
                        Id = funding.Id,
                        ofm_envelope_programming_topup = new Money(sum_of_topup_programming),
                        ofm_envelope_grand_total_topup = new Money(sum_of_topup_total)
                    };
                    service.Update(entityToUpdate);

                    tracingService.Trace("Update the funding");

                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}