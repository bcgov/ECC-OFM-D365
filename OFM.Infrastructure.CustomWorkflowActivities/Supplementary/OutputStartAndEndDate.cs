using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Activities.Statements;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

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

            /*
             * Req 1 If there is no supplementary funding of the same type associated with the main application, start date follows the one-month rule, the end date will be the anniversary date one year later.

               Req 2 If there is an active supplementary funding of the same type for the previous year, apply either the one month after submission date or one day after previous end date, which ever is later.  The end date will be the anniversary date one year later.
            */

            //Fetch application & related supplementary & funding 
            try
            {
                RetrieveRequest request = new RetrieveRequest();
                request.ColumnSet = new ColumnSet(new string[] { "ofm_submittedon", "ofm_allowance_type", "ofm_application", "ofm_renewal_term" });
                request.Target = new EntityReference(recordId.LogicalName, recordId.Id);

                Entity entity = ((RetrieveResponse)service.Execute(request)).Entity;

                if (entity != null && entity.Attributes.Count > 0 && entity.Attributes.Contains("ofm_submittedon") && entity.Attributes.Contains("ofm_renewal_term"))
                {
                    var dateSubmittedOn = entity.GetAttributeValue<DateTime>("ofm_submittedon");
                    var renewalTerm = entity.GetAttributeValue<int>("ofm_renewal_term");

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

                    var convertedSubmittedOn = TimeZoneInfo.ConvertTimeFromUtc(dateSubmittedOn, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));

                    tracingService.Trace("{0}{1}", "SubmittedOn Date: ", convertedSubmittedOn);

                    var finalStartDate = new DateTime();
                    var finalEndDate = new DateTime();

                    var allowanceType = (int)entity.GetAttributeValue<OptionSetValue>("ofm_allowance_type").Value;
                    var applicationId = entity.GetAttributeValue<EntityReference>("ofm_application").Id;
                    var statecode = 0; //ECC.Core.DataContext.ofm_allowance_statecode.Active
                    var statuscode = 6; //ECC.Core.DataContext.ofm_allowance_StatusCode.Approved

                    tracingService.Trace("{0}{1}", "allowanceType: ", allowanceType);
                    tracingService.Trace("{0}{1}", "applicationId: ", applicationId);

                    //fetch related previous approved supplementary with application id
                    var supplementaryQuery = new QueryExpression("ofm_allowance")
                    {

                        ColumnSet = new ColumnSet(true),
                        Criteria =
                        {
                            // Add 2 conditions to ofm_allowance
                            Conditions =
                            {
                                new ConditionExpression("ofm_allowance_type", ConditionOperator.Equal, allowanceType),
                                new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, statecode),
                                new ConditionExpression("statuscode", ConditionOperator.Equal, statuscode)
                            }
                        }
                    };

                    //fetch related funding agreement with application id
                    // Instantiate QueryExpression query
                    var fundingQuery = new QueryExpression("ofm_funding")
                    {

                        ColumnSet = new ColumnSet(true),
                        Criteria =
                        {
                            // Add 2 conditions to ofm_funding
                            Conditions =
                            {
                                new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, statecode)
                            }
                        }
                    };

                    var supplementaryResult = service.RetrieveMultiple(supplementaryQuery);

                    var fundingResult = service.RetrieveMultiple(fundingQuery).Entities.FirstOrDefault();

                    var fundingEndDate = fundingResult.GetAttributeValue<DateTime>("ofm_end_date");
                    var convertedEndDate = TimeZoneInfo.ConvertTimeFromUtc(fundingEndDate, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));

                    tracingService.Trace("{0}{1}", "Funding End Date: ", convertedEndDate);

                    var intermediateDate = convertedEndDate.AddYears(-2);
                    var firstAnniversary = intermediateDate;
                    intermediateDate = convertedEndDate.AddYears(-1);
                    var secondAnniversary = intermediateDate;
                    tracingService.Trace("{0}{1}", "Funding firstAnniversary Date: ", firstAnniversary);
                    tracingService.Trace("{0}{1}", "Funding secondAnniversary Date: ", secondAnniversary);

                    var oneMonthAfterSubmission = convertedSubmittedOn.AddMonths(2);
                    oneMonthAfterSubmission = new DateTime(oneMonthAfterSubmission.Year, oneMonthAfterSubmission.Month, 1, 0, 0, 0);

                    //Start Date Rules:
                    //1. there is approved supplementary funding of the same type for the previous year
                    if (supplementaryResult.Entities.Count > 0 && supplementaryResult[0] != null)
                    {
                        var previousSupplementary = supplementaryResult.Entities.OrderByDescending(t => t.GetAttributeValue<DateTime>("ofm_end_date")).FirstOrDefault();
                        var previouseEndDate = previousSupplementary.GetAttributeValue<DateTime>("ofm_end_date");

                        var convertPreviouseEndDate = TimeZoneInfo.ConvertTimeFromUtc(previouseEndDate, TimeZoneInfo
                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                        .standardname)).FirstOrDefault().ToString()));

                        tracingService.Trace("{0}{1}", "Previous Funding End Date: ", convertPreviouseEndDate);

                        //apply either the one month after submission date or one day after previous end date, which ever is later.
                        var oneDayAfterPreviousEndDate = convertPreviouseEndDate.AddDays(1);
                        oneDayAfterPreviousEndDate = new DateTime(oneDayAfterPreviousEndDate.Year, oneDayAfterPreviousEndDate.Month, oneDayAfterPreviousEndDate.Day, 0, 0, 0);

                        tracingService.Trace("{0}{1}", "oneMonthAfterSubmission: ", oneMonthAfterSubmission);
                        tracingService.Trace("{0}{1}", "oneDayAfterPreviousEndDate: ", oneDayAfterPreviousEndDate);

                        if (oneMonthAfterSubmission > oneDayAfterPreviousEndDate)
                        {
                            finalStartDate = oneMonthAfterSubmission;
                        }
                        else
                        {
                            finalStartDate = oneDayAfterPreviousEndDate;
                        }
                    }
                    else
                    {
                        //2. If there is no approved supplementary funding,
                        // term 1: start date follows the one-month rule
                        // term 2 and 3: apply either the one month after submission date or one day after previous term anniversary date, which ever is later.
                        //(apply Feb 2 start is April 1)
                        //(apply Jan 31 start is Mar 1)

                        if (renewalTerm == 1)
                        {
                            finalStartDate = convertedSubmittedOn.AddMonths(2);
                            finalStartDate = new DateTime(finalStartDate.Year, finalStartDate.Month, 1, 0, 0, 0);
                        }else if(renewalTerm == 2)
                        {
                            finalStartDate = firstAnniversary.AddDays(1);
                            finalStartDate = new  DateTime(finalStartDate.Year, finalStartDate.Month, finalStartDate.Day, 0, 0, 0);

                            if (oneMonthAfterSubmission > finalStartDate)
                            {
                                finalStartDate = oneMonthAfterSubmission;
                            }
                        }
                        else if (renewalTerm == 3)
                        {
                            finalStartDate = secondAnniversary.AddDays(1);
                            finalStartDate = new DateTime(finalStartDate.Year, finalStartDate.Month, finalStartDate.Day, 0, 0, 0);

                            if (oneMonthAfterSubmission > finalStartDate)
                            {
                                finalStartDate = oneMonthAfterSubmission;
                            }
                        }
                    }

                    //End Date Rules:
                    //Get the terms number from portal, the end date will be the term anniversary date
                    intermediateDate = firstAnniversary.AddYears(-1);
                    if (finalStartDate <= firstAnniversary && finalStartDate >= intermediateDate && renewalTerm == 1)
                    {
                        finalEndDate = firstAnniversary;
                    }
                    intermediateDate = secondAnniversary.AddYears(-1);
                    if (finalStartDate <= secondAnniversary && finalStartDate >= intermediateDate && renewalTerm == 2)
                    {
                        finalEndDate = secondAnniversary;
                    }
                    intermediateDate = convertedEndDate.AddYears(-1);
                    if (finalStartDate <= convertedEndDate && finalStartDate >= convertedEndDate.AddYears(-1) && renewalTerm == 3)
                    {
                        finalEndDate = convertedEndDate;
                    }

                    startDate.Set(executionContext, finalStartDate);
                    endDate.Set(executionContext, finalEndDate);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}