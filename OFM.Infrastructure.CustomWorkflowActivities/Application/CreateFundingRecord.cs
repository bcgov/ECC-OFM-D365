﻿using ECC.Core.DataContext;
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
                Entity entity = service.Retrieve("ofm_application", recordId, new ColumnSet("ofm_funding_number_base", "statuscode", "ofm_facility"));
                OptionSetValue statusReason = entity.GetAttributeValue<OptionSetValue>("statuscode");
                int statusReasonValue = statusReason.Value;
                tracingService.Trace("Checking condtions StatusReason value:{0}, ofmFundingNumberBase:{1}, facility:{2} ", statusReasonValue, ofmFundingNumberBase, ((EntityReference)entity["ofm_facility"]).Id);
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
                        Entity newupdate = new Entity();
                        newupdate.LogicalName = entity.LogicalName;
                        newupdate["ofm_applicationid"] = entity["ofm_applicationid"];
                        newupdate["ofm_funding_number_base"] = ofmFundingNumberBase;
                        newupdate["ofm_funding_version_number"] = 0;
                        service.Update(newupdate);

                        //  Create first Funding record
                        Entity newFundingRecord = new Entity("ofm_funding");
                        newFundingRecord["ofm_version_number"] = 0;
                        newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-00"; // Primary coloumn
                        newFundingRecord["ofm_application"] = new EntityReference("ofm_application", recordId);
                        newFundingRecord["ofm_rate_schedule"] = (rateSchedualId == null || rateSchedualId == Guid.Empty) ? null : new EntityReference("ofm_rate_schedule", rateSchedualId);
                        newFundingRecord["ofm_facility"] = new EntityReference(((EntityReference)entity["ofm_facility"]).LogicalName, ((EntityReference)entity["ofm_facility"]).Id);
                        service.Create(newFundingRecord);

                        tracingService.Trace("\nUpdate Agreement Number Base and create first Funding record successfully.");
                    }
                    else  // resubmit
                    {
                        tracingService.Trace("\nStart resubmit logic implement, logical name: {0}, id:{1}", entity.LogicalName, entity.Id);
                        var fetchData = new
                        {
                            ofm_application = recordId.ToString()
                        };
                        fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                        <fetch>
                                          <entity name=""ofm_application"">
                                            <attribute name=""ofm_funding_number_base"" />
                                            <attribute name=""ofm_funding_version_number"" />
                                            <filter>
                                              <condition attribute=""ofm_applicationid"" operator=""eq"" value=""{fetchData.ofm_application}"" />
                                            </filter>
                                          </entity>
                                        </fetch>";

                        EntityCollection applicationRecord = service.RetrieveMultiple(new FetchExpression(fetchXml));
                        tracingService.Trace("application count: " + applicationRecord.Entities.Count);
                        if (applicationRecord.Entities.Count > 0 && applicationRecord[0] != null)
                        {
                            tracingService.Trace("\nModification, create new funding records, current version: " + applicationRecord[0].GetAttributeValue<int>("ofm_funding_version_number"));
                            Entity newFundingRecord = new Entity("ofm_funding");
                            var newVersionNumber = applicationRecord[0].GetAttributeValue<int>("ofm_funding_version_number") + 1;
                            newFundingRecord["ofm_version_number"] = newVersionNumber;
                            newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-" + newVersionNumber.ToString("00"); // Primary coloumn
                            newFundingRecord["ofm_application"] = new EntityReference("ofm_application", recordId);
                            newFundingRecord["ofm_rate_schedule"] = (rateSchedualId == null || rateSchedualId == Guid.Empty) ? null : new EntityReference("ofm_rate_schedule", rateSchedualId);
                            newFundingRecord["ofm_facility"] = new EntityReference(((EntityReference)entity["ofm_facility"]).LogicalName, ((EntityReference)entity["ofm_facility"]).Id);
                            service.Create(newFundingRecord);


                            //Update application version number
                            Entity newupdate = new Entity();
                            newupdate.LogicalName = entity.LogicalName;
                            newupdate["ofm_applicationid"] = entity["ofm_applicationid"];
                            newupdate["ofm_funding_version_number"] = newVersionNumber;
                            service.Update(newupdate);


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