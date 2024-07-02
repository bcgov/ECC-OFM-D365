using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace OFM.Infrastructure.CustomWorkflowActivities.Application
{
    public sealed class CreateFundingRecord : CodeActivity
    {
        [RequiredArgument]
        [Input("FundingNumberBase")]
        public InArgument<string> fundingnumberbase { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            string ofmFundingNumberBase = fundingnumberbase.Get(executionContext);
            var recordId = context.PrimaryEntityId;
            tracingService.Trace("Begin creating funding record, recordId: {0}, ofmFundingNumberBase:{1} ", recordId, ofmFundingNumberBase);
            try
            {
                Entity entity = service.Retrieve("ofm_application", recordId, new ColumnSet("ofm_funding_number_base", "statuscode"));
                OptionSetValue statusReason = entity.GetAttributeValue<OptionSetValue>("statuscode");
                int statusReasonValue = statusReason.Value;
                tracingService.Trace("Checking condtions StatusReason value:{0}, ofmFundingNumberBase:{1} ", statusReasonValue, ofmFundingNumberBase);
                if (entity != null && entity.Attributes.Count > 0)
                {
                    DateTime currentDate = DateTime.UtcNow;

                    // get Rate Schedule
                    var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                <fetch>
                                  <entity name=""ofm_rate_schedule"">
                                    <attribute name=""ofm_caption"" />
                                    <attribute name=""ofm_end_date"" />
                                    <attribute name=""ofm_rate_scheduleid"" />
                                    <attribute name=""ofm_start_date"" />
                                    <attribute name=""statecode"" />
                                    <filter type=""and"">
                                      <condition attribute=""ofm_start_date"" operator=""le"" value=""{currentDate}"" />
                                      <condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                      <filter type=""or"">
                                        <condition attribute=""ofm_end_date"" operator=""null"" />
                                        <condition attribute=""ofm_end_date"" operator=""ge"" value=""{currentDate}"" />
                                      </filter>
                                    </filter>
                                  </entity>
                                </fetch>";

                    EntityCollection rateSchedual = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    Guid rateSchedualId = Guid.Empty;
                    if (rateSchedual.Entities.Count > 0) { rateSchedualId = rateSchedual[0].Id; }

                    if (string.IsNullOrEmpty(ofmFundingNumberBase))
                    {
                        //First submission, use seed to generate the funding agreement number
                        //get the fiscal year table row
                        var fetchData = new
                        {
                            statuscode = "1"
                        };
                        fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                        <fetch>
                                          <entity name=""ofm_fiscal_year"">
                                            <attribute name=""ofm_agreement_number_seed"" />
                                            <attribute name=""ofm_caption"" />
                                            <attribute name=""ofm_fiscal_year_number"" />
                                            <attribute name=""ofm_fiscal_yearid"" />
                                            <filter>
                                              <condition attribute=""statuscode"" operator=""eq"" value=""{fetchData.statuscode/*1*/}"" />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        EntityCollection fiscalYears = service.RetrieveMultiple(new FetchExpression(fetchXml));
                        Entity fiscalYear = fiscalYears[0]; //the first return result
                        var agreementNumSeed = fiscalYear.GetAttributeValue<int>(ofm_fiscal_year.Fields.ofm_agreement_number_seed);

                        tracingService.Trace("This first submission and AgreementNumSeed is: " + agreementNumSeed);

                        //update the seed number first
                        Entity fiscalYearTable = new Entity("ofm_fiscal_year");
                        fiscalYearTable.Attributes["ofm_agreement_number_seed"] = agreementNumSeed + 1;
                        fiscalYearTable["ofm_fiscal_yearid"] = fiscalYear["ofm_fiscal_yearid"];
                        service.Update(fiscalYearTable);

                        //generate the Funding Agreement Num
                        var yearNum = fiscalYear.GetAttributeValue<string>(ofm_fiscal_year.Fields.ofm_caption).Substring(2, 2);
                        ofmFundingNumberBase = "OFM" + "-" + yearNum + agreementNumSeed.ToString("D6");

                        //generate the application funding number Base
                        tracingService.Trace("***Update Funding Number Base" + ofmFundingNumberBase);
                        Entity newudpate = new Entity();
                        newudpate.LogicalName = entity.LogicalName;
                        newudpate["ofm_applicationid"] = entity["ofm_applicationid"];
                        newudpate["ofm_funding_number_base"] = ofmFundingNumberBase;
                        service.Update(newudpate);

                        //  Create first Funding record
                        Entity newFundingRecord = new Entity("ofm_funding");
                        newFundingRecord["ofm_version_number"] = 0;
                        newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-00"; // Primary coloumn
                        newFundingRecord["ofm_application"] = new EntityReference("ofm_application", recordId);
                        newFundingRecord["ofm_rate_schedule"] = (rateSchedualId == null || rateSchedualId == Guid.Empty) ? null : new EntityReference("ofm_rate_schedule", rateSchedualId);

                        service.Create(newFundingRecord);

                        tracingService.Trace("\nUpdate Agreement Number Base and create first Funding record successfully.");
                    }
                    else  // resubmit
                    {
                        tracingService.Trace("\nStart resubmit logic implement, logical name: {0}, id:{1}", entity.LogicalName, entity.Id);
                        var fetchData = new
                        {
                            ofm_application = recordId.ToString(),
                            statecode = "0"
                        };
                        fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                        <fetch>
                                          <entity name=""ofm_funding"">
                                            <attribute name=""ofm_application"" />
                                            <attribute name=""statecode"" />
                                            <attribute name=""statuscode"" />
                                            <attribute name=""ofm_version_number"" />
                                            <filter>
                                              <condition attribute=""ofm_application"" operator=""eq"" value=""{fetchData.ofm_application}"" />
                                              <condition attribute=""statecode"" operator=""eq"" value=""{fetchData.statecode}"" />
                                            </filter>
                                            <order attribute=""ofm_version_number"" descending=""true"" />
                                          </entity>
                                        </fetch>";

                        EntityCollection fundingRecords = service.RetrieveMultiple(new FetchExpression(fetchXml));
                        if (fundingRecords.Entities.Count > 0 && fundingRecords[0] != null)
                        {
                            var id = fundingRecords[0].Id;
                            tracingService.Trace("\nResubmission, create new funding records:" + id);
                            Entity newFundingRecord = new Entity("ofm_funding");
                            newFundingRecord["ofm_version_number"] = fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1;
                            newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-" + (fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1).ToString("00"); // Primary coloumn
                            newFundingRecord["ofm_application"] = new EntityReference("ofm_application", recordId);
                            newFundingRecord["ofm_rate_schedule"] = (rateSchedualId == null || rateSchedualId == Guid.Empty) ? null : new EntityReference("ofm_rate_schedule", rateSchedualId);
                            newFundingRecord["statuscode"] = new OptionSetValue((int) ofm_funding_StatusCode.FAReview);
                            service.Create(newFundingRecord);
                            
                            tracingService.Trace("\nThis is a resubmisstion.Create Funding records successfully.");
                        }
                    }
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
