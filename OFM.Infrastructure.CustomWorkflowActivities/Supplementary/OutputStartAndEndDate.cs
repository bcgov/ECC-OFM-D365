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

            /* One month rule: 
             * If submission date is within the last month of the current term, start date = submission date
             * If submission date is before the last month of the current term, start date = the first date of next month.
             * UNLESS the SP has an active supplementary of the same type, THEN the start date is the 1st day after the Active supplementary expires.
             * 
             * Example: 
             * First year anniversary date: 2024 Oct 31
             * 1. Submission date: 2024 Oct 6 -> Start date: 2024 Oct 6
             * 2. Submission date: 2024 Sept 30 -> Start date: 2024 Oct 1
             */

            /*
             * Supplementary should have ofm_renewal_term : 
             * StartDate Follow the rules:
             * 1. Have approved supplementary of the same type and applied for next term: apply either the 15 days rule after submission date or one day after previous end date, which ever is later.  The end date will be the anniversary date one year later.
             * 2. Have approved supplementary of the same type for current term and applied for current term: this will stop from portal
             * 3. No approved supplementary and applied for term 1: start date follows the one month rule
             * 4. No approved supplementary and applied for term 2 and 3: apply either one month rule after submission date or one day after previous term anniversary date, which ever is later.
             * 
             * EndDate Follow the rules:
             * Get the terms number from portal, the end date will be the term anniversary date
             */


            //Fetch application & related supplementary & funding 
            try
            {
                RetrieveRequest request = new RetrieveRequest();
                request.ColumnSet = new ColumnSet(new string[] { "ofm_submittedon", "ofm_allowance_type", "ofm_application", "ofm_renewal_term", "ofm_transport_vehicle_vin" });
                request.Target = new EntityReference(recordId.LogicalName, recordId.Id);

                Entity entity = ((RetrieveResponse)service.Execute(request)).Entity;


                if (entity != null && entity.Attributes.Count > 0 && entity.Attributes.Contains("ofm_submittedon") && entity.Attributes.Contains("ofm_renewal_term"))
                {
                    var dateSubmittedOn = entity.GetAttributeValue<DateTime>("ofm_submittedon");

                    //Get the submitted on time
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

                    tracingService.Trace("{0}{1}", "SubmittedOn Date: ", convertedSubmittedOn); //Get the submitted on time

                    //Get renewal term number
                    var renewalTerm = entity.GetAttributeValue<int>("ofm_renewal_term");
                    tracingService.Trace("{0}{1}", "FA year number: ", renewalTerm);


                    var finalStartDate = new DateTime();
                    var finalEndDate = new DateTime();

                    //Get related Application id and FA date 
                    var applicationId = entity.GetAttributeValue<EntityReference>("ofm_application").Id;
                    tracingService.Trace("{0}{1}", "applicationId: ", applicationId);

                    var fundingStateCode = (int)ofm_funding_statecode.Active;
                    //fetch related funding agreement with application id
                    var fundingQuery = new QueryExpression("ofm_funding")
                    {

                        ColumnSet = new ColumnSet(true),
                        Criteria =
                        {
                            // Add 2 conditions to ofm_funding
                            Conditions =
                            {
                                new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                new ConditionExpression("statecode", ConditionOperator.Equal, fundingStateCode)
                            }
                        }
                    };

                    var fundingResult = service.RetrieveMultiple(fundingQuery).Entities.OrderByDescending(t => t.GetAttributeValue<int>("ofm_version_number")).FirstOrDefault();
                    var fundingStartDate = fundingResult.GetAttributeValue<DateTime>("ofm_start_date");
                    var fundingEndDate = fundingResult.GetAttributeValue<DateTime>("ofm_end_date");
                    /*                    var convertedStartDate = TimeZoneInfo.ConvertTimeFromUtc(fundingStartDate, TimeZoneInfo
                                            .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                                            .standardname)).FirstOrDefault().ToString()));
                                        var convertedEndDate = TimeZoneInfo.ConvertTimeFromUtc(fundingEndDate, TimeZoneInfo
                                            .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                                            .standardname)).FirstOrDefault().ToString()));*/



                    tracingService.Trace("{0}{1}", "Funding Start Date: ", fundingStartDate);
                    tracingService.Trace("{0}{1}", "Funding End Date: ", fundingEndDate);

                    var firstAnniversary = new DateTime();
                    var secondAnniversary = new DateTime();
                    var intermediateDate = new DateTime();

                    //Two year contract or three year contract
                    if (fundingEndDate.Year - fundingStartDate.Year  == 2)
                    {
                        intermediateDate = fundingEndDate.AddYears(-1);
                        firstAnniversary = intermediateDate;
                        secondAnniversary = fundingEndDate;
                    }
                    else
                    {
                        intermediateDate = fundingEndDate.AddYears(-2);
                        firstAnniversary = intermediateDate;
                        intermediateDate = fundingEndDate.AddYears(-1);
                        secondAnniversary = intermediateDate;
                    }

                    tracingService.Trace("{0}{1}", "Funding firstAnniversary Date: ", firstAnniversary);
                    tracingService.Trace("{0}{1}", "Funding secondAnniversary Date: ", secondAnniversary);
                    tracingService.Trace("{0}{1}", "Funding year term: ", fundingEndDate.Year - fundingStartDate.Year);

                    //Applied One month rule: 
                    var oneMonthbeforeAnniversary = new DateTime();
                    if (renewalTerm == 1)
                    {
                        oneMonthbeforeAnniversary = firstAnniversary.AddMonths(-1);
                    }
                    else if (renewalTerm == 2)
                    {
                        oneMonthbeforeAnniversary = secondAnniversary.AddMonths(-1);
                    }
                    else if (renewalTerm == 3)
                    {
                        oneMonthbeforeAnniversary = fundingEndDate.AddMonths(-1);
                    }

                    tracingService.Trace("{0}{1}", "One Month before Anniversary: ", oneMonthbeforeAnniversary);

                    var potentialStartDate = new DateTime();

                    //compare submission time and oneMonthbeforeAnniversary
                    //if submission time is greater than one month before Anniversary
                    if (convertedSubmittedOn > oneMonthbeforeAnniversary)
                    {
                        //potentialStartDate = submission date
                        potentialStartDate = new DateTime(convertedSubmittedOn.Year, convertedSubmittedOn.Month, convertedSubmittedOn.Day, 0, 0, 0);
                    }
                    else
                    {
                        //potentialStartDate= 1st day of next month
                        potentialStartDate = convertedSubmittedOn.AddMonths(1);
                        potentialStartDate = new DateTime(potentialStartDate.Year, potentialStartDate.Month, 1, 0, 0, 0);
                    }

                    //Start Date Rules:
                    //if renewal term (FA year) is 1: dont need to check previous supplication application, follow one month rule
                    if (renewalTerm == 1)
                    {
                        finalStartDate = potentialStartDate;
                    }
                    else
                    {
                        //Check if have the previous approved supplementary application for term 2 and term 3

                        //IP and SN (1 and 2): only one per year
                        //Transportation (3): one per vin per year

                        var allowanceType = (int)entity.GetAttributeValue<OptionSetValue>("ofm_allowance_type").Value;
                        var supplmentaryStatecode = (int)ofm_allowance_statecode.Active;
                        var supplmentaryStatuscode = (int)ofm_allowance_StatusCode.Approved;
                        tracingService.Trace("{0}{1}", "allowanceType: ", allowanceType);

                        var previousTerm = renewalTerm - 1;
                        tracingService.Trace("{0}{1}", "previousTerm: ", previousTerm);

                        var supplementaryQuery = new QueryExpression();

                        if (allowanceType == 3)
                        {
                            var vin = entity.GetAttributeValue<string>("ofm_transport_vehicle_vin");
                            tracingService.Trace("{0}{1}", "vin: ", vin);

                            //fetch related previous approved supplementary with application id 
                            supplementaryQuery = new QueryExpression("ofm_allowance")
                            {

                                ColumnSet = new ColumnSet(true),
                                Criteria =
                                {
                                    // Add 2 conditions to ofm_allowance
                                    Conditions =
                                    {
                                        new ConditionExpression("ofm_allowance_type", ConditionOperator.Equal, allowanceType),
                                        new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                        new ConditionExpression("statecode", ConditionOperator.Equal, supplmentaryStatecode),
                                        new ConditionExpression("statuscode", ConditionOperator.Equal, supplmentaryStatuscode),
                                        new ConditionExpression("ofm_renewal_term", ConditionOperator.Equal, previousTerm),
                                        new ConditionExpression("ofm_transport_vehicle_vin", ConditionOperator.Equal, vin),
                                    }
                                }
                            };

                        }
                        else
                        {
                            //fetch related previous approved supplementary with application id 
                            supplementaryQuery = new QueryExpression("ofm_allowance")
                            {

                                ColumnSet = new ColumnSet(true),
                                Criteria =
                                {
                                    // Add 2 conditions to ofm_allowance
                                    Conditions =
                                    {
                                        new ConditionExpression("ofm_allowance_type", ConditionOperator.Equal, allowanceType),
                                        new ConditionExpression("ofm_application", ConditionOperator.Equal, applicationId),
                                        new ConditionExpression("statecode", ConditionOperator.Equal, supplmentaryStatecode),
                                        new ConditionExpression("statuscode", ConditionOperator.Equal, supplmentaryStatuscode),
                                        new ConditionExpression("ofm_renewal_term", ConditionOperator.Equal, previousTerm)
                                    }
                                }
                            };
                        }
                        var supplementaryResult = service.RetrieveMultiple(supplementaryQuery);

                        //there is approved supplementary funding of the same type for the previous year
                        if (supplementaryResult.Entities.Count > 0 && supplementaryResult[0] != null)
                        {
                            var previousSupplementary = supplementaryResult.Entities.OrderByDescending(t => t.GetAttributeValue<DateTime>("ofm_end_date")).FirstOrDefault();
                            var previouseEndDate = previousSupplementary.GetAttributeValue<DateTime>("ofm_end_date");

                            /*                            var convertPreviouseEndDate = TimeZoneInfo.ConvertTimeFromUtc(previouseEndDate, TimeZoneInfo
                                                        .FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>(TimeZoneDefinition.Fields
                                                        .standardname)).FirstOrDefault().ToString()));*/

                            tracingService.Trace("{0}{1}", "Previous Funding End Date: ", previouseEndDate);

                            //apply either one month rule or one day after previous end date, which ever is later. - Applied for both portal and CRM submission
                            var oneDayAfterPreviousEndDate = previouseEndDate.AddDays(1);
                            oneDayAfterPreviousEndDate = new DateTime(oneDayAfterPreviousEndDate.Year, oneDayAfterPreviousEndDate.Month, oneDayAfterPreviousEndDate.Day, 0, 0, 0);

                            tracingService.Trace("{0}{1}", "potentialStartDate: ", potentialStartDate);
                            tracingService.Trace("{0}{1}", "oneDayAfterPreviousEndDate: ", oneDayAfterPreviousEndDate);

                            if (potentialStartDate > oneDayAfterPreviousEndDate)
                            {
                                finalStartDate = potentialStartDate;
                            }
                            else
                            {
                                finalStartDate = oneDayAfterPreviousEndDate;
                            }

                        }
                        else
                        {
                            //2. If there is no approved supplementary funding,

                            // Submit from portal with renewal term: 
                            // term 1: start date follows one month rule
                            // term 2 and 3: apply either one month rule or one day after previous term anniversary date, which ever is later.

                            if (renewalTerm == 1)
                            {
                                finalStartDate = potentialStartDate;
                            }
                            else if (renewalTerm == 2)
                            {
                                var secondAnniversaryStartDate = firstAnniversary.AddDays(1);
                                secondAnniversaryStartDate = new DateTime(secondAnniversaryStartDate.Year, secondAnniversaryStartDate.Month, secondAnniversaryStartDate.Day, 0, 0, 0);

                                finalStartDate = potentialStartDate > secondAnniversaryStartDate ? potentialStartDate : secondAnniversaryStartDate;
                            }
                            else if (renewalTerm == 3)
                            {
                                var thirdAnniversaryStartDate = secondAnniversary.AddDays(1);
                                thirdAnniversaryStartDate = new DateTime(thirdAnniversaryStartDate.Year, thirdAnniversaryStartDate.Month, thirdAnniversaryStartDate.Day, 0, 0, 0);

                                finalStartDate = potentialStartDate > thirdAnniversaryStartDate ? potentialStartDate : thirdAnniversaryStartDate;
                            }
                        }

                    }

                    //EndDate rule: 
                    //the end date will be the term anniversary date
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
                    intermediateDate = fundingEndDate.AddYears(-1);
                    if (finalStartDate <= fundingEndDate && finalStartDate >= fundingEndDate.AddYears(-1) && renewalTerm == 3)
                    {
                        finalEndDate = fundingEndDate;
                    }

                    tracingService.Trace("{0}{1}", "Final Start Date: ", finalStartDate);
                    tracingService.Trace("{0}{1}", "Final End Date: ", finalEndDate);

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