﻿using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IdentityModel.Metadata;
using System.IdentityModel.Protocols.WSTrust;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Services;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OFM.Infrastructure.Plugins.Agreement
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class FundingRecordCreation : PluginBase
    {
        public FundingRecordCreation(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(FundingRecordCreation))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new InvalidPluginExecutionException(nameof(localPluginContext), new Dictionary<string, string>() { ["failedRecordId"] = localPluginContext.Target.Id.ToString() });
            }

            localPluginContext.Trace("Start FundingRecordCreation Plug-in");

            if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity)
            {

                Entity entity = (Entity)localPluginContext.PluginExecutionContext.InputParameters["Target"];

                var currentImage = localPluginContext.Current;

                if (currentImage != null)
                {
                    var applicationStatus = entity.GetAttributeValue<OptionSetValue>(ofm_application.Fields.statuscode).Value;
                    localPluginContext.Trace("***Debug applicationStatus:*** " + applicationStatus);
                    var applicationId = entity.GetAttributeValue<Guid>(ofm_application.Fields.Id);
                    Entity application = localPluginContext.PluginUserService.Retrieve(entity.LogicalName, entity.GetAttributeValue<Guid>("ofm_applicationid"), new ColumnSet("ofm_funding_number_base"));
                    string ofmFundingNumberBase = application.GetAttributeValue<string>("ofm_funding_number_base");
                    if (applicationStatus == 3 && !string.IsNullOrEmpty(ofmFundingNumberBase))
                    {
                        //check if the application is first submission - fetch the funding record based on application id
                        var fetchData = new
                        {
                            ofm_application = applicationId.ToString(),
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

                        EntityCollection fundingRecords = localPluginContext.PluginUserService.RetrieveMultiple(new FetchExpression(fetchXml));

                        //localPluginContext.Trace("***Debug ofm_applicationid:*** " + applicationId);
                        //localPluginContext.Trace("***Debug fundingRecords Count:*** " + fundingRecords.Entities.Count);

                        //if there is a funding record associated and funding agreement number is not empty -> Resubmission
                        if (fundingRecords.Entities.Count > 0 && fundingRecords[0] != null )
                        {
                            var id = fundingRecords[0].Id;
                            localPluginContext.Trace("***Debug resubmission:*** " + id);
                            //deactive the current funding record
                            Entity fundingRecordTable = new Entity("ofm_funding");
                            fundingRecordTable.Id = id;
                            fundingRecordTable["statecode"] = new OptionSetValue(1); //Inactive
                            fundingRecordTable["statuscode"] = new OptionSetValue(2);
                            localPluginContext.PluginUserService.Update(fundingRecordTable);
                            //create a new funding record
                            Entity newFundingRecord = new Entity("ofm_funding");
                            newFundingRecord["ofm_version_number"] = fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1;
                            newFundingRecord["ofm_funding_number"] = ofmFundingNumberBase + "-"+ (fundingRecords[0].GetAttributeValue<int>("ofm_version_number") + 1).ToString("00"); // Primary coloumn
                            newFundingRecord["ofm_application"] = new EntityReference("ofm_application", applicationId);
                            localPluginContext.PluginUserService.Create(newFundingRecord);
                        }
                    }

                }
            }
            localPluginContext.Trace("***Plugin end");
        }
    }
}