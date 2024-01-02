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
    public class GenerateAgreementNumber : PluginBase
    {
        public GenerateAgreementNumber(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GenerateAgreementNumber))
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

            localPluginContext.Trace("Start GenerateAgreementNumber Plug-in");

            if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity)
            {

                Entity entity = (Entity)localPluginContext.PluginExecutionContext.InputParameters["Target"];

                //check if the application is submitted - statuscode (3)

                //foreach (var attribute in localPluginContext.Current.Attributes)
                //{
                //    //access the attribute values
                //    localPluginContext.Trace("***input params:***" + attribute.Key + "Value: " + attribute.Value);
                //}

                var currentImage = localPluginContext.Current;

                if (currentImage != null)
                {
                    var applicationStatus = entity.GetAttributeValue<OptionSetValue>(ofm_application.Fields.statuscode).Value;
                    localPluginContext.Trace("***Debug applicationStatus:*** " + applicationStatus);
                    if (applicationStatus == 3)
                    {

                        string newApplicationFundingAgreementNum = string.Empty;

                        //check if the application is first submission - ofm_funding_agreement_number
                        var ofmFundingAgreementNumber = currentImage.GetAttributeValue<string>(ofm_application.Fields.ofm_funding_agreement_number);
                        localPluginContext.Trace("***Debug ofmFundingAgreementNumber :*** " + ofmFundingAgreementNumber);
                        if (string.IsNullOrEmpty(ofmFundingAgreementNumber))
                        {

                            //First submission, use seed to generate the funding agreement number
                            //get the fiscal year table row
                            var fetchData = new
                            {
                                statuscode = "1"
                            };
                            var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
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

                            EntityCollection fiscalYears = localPluginContext.PluginUserService.RetrieveMultiple(new FetchExpression(fetchXml));
                            Entity fiscalYear = fiscalYears[0]; //the first return result
                            var agreementNumSeed = fiscalYear.GetAttributeValue<int>(ofm_fiscal_year.Fields.ofm_agreement_number_seed);

                            localPluginContext.Trace("***Debug first submission :*** " + agreementNumSeed);

                            //update the seed number first
                            Entity fiscalYearTable = new Entity("ofm_fiscal_year");
                            fiscalYearTable.Attributes["ofm_agreement_number_seed"] = agreementNumSeed + 1;
                            fiscalYearTable["ofm_fiscal_yearid"] = fiscalYear["ofm_fiscal_yearid"];
                            localPluginContext.PluginUserService.Update(fiscalYearTable);

                            //generate the Funding Agreement Num
                            //caption is 2024/25
                            var yearNum = fiscalYear.GetAttributeValue<string>(ofm_fiscal_year.Fields.ofm_caption).Substring(2,2);
                            var versionNum = "00"; //Start with 00
                            newApplicationFundingAgreementNum = "OFM" + "-" + yearNum + agreementNumSeed.ToString("D6") + "-" + versionNum;

                        }
                        else 
                        {

                            //Resubmission, only update the version number
                            //"OFM-23000001-VV"
                            string[] applicationFundingAgreementNumArr = ofmFundingAgreementNumber.Split('-');
                            var versionNum = Convert.ToInt32(applicationFundingAgreementNumArr[2]);

                            var newVersionNum = (versionNum + 1).ToString("D2"); //pad leading 0
                            string[] newApplicationFundingAgreementNumArr = { applicationFundingAgreementNumArr[0], applicationFundingAgreementNumArr[1], newVersionNum };
                            newApplicationFundingAgreementNum = string.Join("-", newApplicationFundingAgreementNumArr);
                        }


                        if (!string.IsNullOrEmpty(newApplicationFundingAgreementNum))
                        {
                            //Update the application funding agreement number

                            Entity newudpate = new Entity();
                            newudpate.LogicalName = entity.LogicalName;
                            newudpate["ofm_applicationid"] = entity["ofm_applicationid"];
                            newudpate["ofm_funding_agreement_number"] = newApplicationFundingAgreementNum;
                            localPluginContext.PluginUserService.Update(newudpate);
                        }
                    }
                }

            }

        }
    }
}