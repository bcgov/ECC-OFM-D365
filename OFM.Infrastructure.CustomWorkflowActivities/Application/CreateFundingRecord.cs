using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

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
                // ofmFundingNumberBase = entity.GetAttributeValue<string>("ofm_funding_number_base");
                OptionSetValue statusReason = entity.GetAttributeValue<OptionSetValue>("statuscode");
                int statusReasonValue = statusReason.Value;
                tracingService.Trace("Checking condtions, statusReason: {0}, value:{1}, ofmFundingNumberBase:{2} ", statusReason.ToString(), statusReasonValue, ofmFundingNumberBase);
                // should be resubmit application
                if (entity != null && entity.Attributes.Count > 0 && entity.Attributes.Contains("ofm_funding_number_base") &&
                    !string.IsNullOrEmpty(ofmFundingNumberBase) && statusReasonValue == 3)
                {
                    tracingService.Trace("Start logic implement, logical name: {0}, id:{1}", entity.LogicalName, entity.Id);
                    var fetchData = new
                    {
                        ofm_application = recordId.ToString(),
                        statecode = "0"
                    };
                    var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
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
                        tracingService.Trace("***Debug deactive records:*** " + id);
                        //deactive the current funding record
                        Entity fundingRecordTable = new Entity("ofm_funding");
                        fundingRecordTable.Id = id;
                        fundingRecordTable["statecode"] = new OptionSetValue(1); //Inactive
                        fundingRecordTable["statuscode"] = new OptionSetValue(2);
                        service.Update(fundingRecordTable);
                        //create a new funding record
                        tracingService.Trace("***Debug create funding records:*** " + id);
                        Entity newFundingRecord = new Entity("ofm_funding");
                        newFundingRecord["ofm_version_number"] = fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1;
                        newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-" + (fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1).ToString("00"); // Primary coloumn
                        newFundingRecord["ofm_application"] = new EntityReference("ofm_application", recordId);
                        service.Create(newFundingRecord);
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
