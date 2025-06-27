using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFM.Infrastructure.Plugins.Account
{
    public class StorePreviousAccountValuesToApplication : PluginBase
    {
        public StorePreviousAccountValuesToApplication(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(StorePreviousAccountValuesToApplication))
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

            localPluginContext.Trace("Start StorePreviousAccountValues Plug-in");

            if (localPluginContext.PluginExecutionContext.InputParameters.Contains("Target") && localPluginContext.PluginExecutionContext.InputParameters["Target"] is Entity)
            {
                Entity entity = (Entity)localPluginContext.PluginExecutionContext.InputParameters["Target"];
                Entity preImageAccount = localPluginContext.PluginExecutionContext.PreEntityImages["PreImage"];
                Entity postImageAccount = localPluginContext.PluginExecutionContext.PostEntityImages["PostImage"];

                if (preImageAccount != null && postImageAccount != null)
                {

                    string accountId = preImageAccount.GetAttributeValue<Guid>("accountid").ToString().Replace("{", "").Replace("}", "");
                    localPluginContext.Trace("***Debug application:*** " + accountId);

                    if (accountId != null)
                    {
                        var applicationFetchXML = $@"<fetch>
                                                      <entity name='ofm_application'>
                                                        <attribute name='ofm_applicationid' />
                                                        <attribute name='ofm_application' />
                                                        <filter>
                                                          <condition attribute='statecode' operator='eq' value='0' />
                                                          <filter type='or'>
                                                            <condition attribute='ofm_organization' operator='eq' value='{accountId}' />
                                                            <condition attribute='ofm_facility' operator='eq' value='{accountId}' />
                                                          </filter>
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                        var application = localPluginContext.PluginUserService.RetrieveMultiple(new FetchExpression(applicationFetchXML)).Entities.Distinct().ToList();

                        if (application.Count > 0)
                        {
                            localPluginContext.Trace(application.Count.ToString() + " related Active Applicaitons found");

                            application.ForEach(record =>
                            {
                                var updateRequired = false;
                                Entity applicationUpdate = new Entity("ofm_application");
                                applicationUpdate.Id = record.Id;
                                
                                //Organization Name and Address
                                if (preImageAccount.GetAttributeValue<OptionSetValue>("ccof_accounttype").Value == (int)ccof_AccountType.Organization
                                && preImageAccount.GetAttributeValue<string>("name") != postImageAccount.GetAttributeValue<string>("name"))
                                {
                                    applicationUpdate["ofm_previous_organization_name"] = preImageAccount.GetAttributeValue<string>("name");
                                    updateRequired = true;
                                }
                                if (preImageAccount.GetAttributeValue<OptionSetValue>("ccof_accounttype").Value == (int)ccof_AccountType.Organization &&
                                preImageAccount.GetAttributeValue<string>("address1_composite") != postImageAccount.GetAttributeValue<string>("address1_composite"))
                                {
                                    var addressString = string.Empty;
                                    addressString += preImageAccount.GetAttributeValue<string>("address1_line1");
                                    if (preImageAccount.GetAttributeValue<string>("address1_line2") != null)
                                    {
                                        addressString += ", " + preImageAccount.GetAttributeValue<string>("address1_line2") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_city") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_stateorprovince") + " "
                                                        + preImageAccount.GetAttributeValue<string>("address1_postalcode");
                                    }
                                    else
                                    {
                                        addressString += ", " + preImageAccount.GetAttributeValue<string>("address1_city") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_stateorprovince") + " "
                                                        + preImageAccount.GetAttributeValue<string>("address1_postalcode");
                                    }

                                    applicationUpdate["ofm_previous_organization_address"] = addressString;
                                    updateRequired = true;
                                }


                                //Facility Name and Address
                                if (preImageAccount.GetAttributeValue<OptionSetValue>("ccof_accounttype").Value == (int)ccof_AccountType.Facility
                                && preImageAccount.GetAttributeValue<string>("name") != postImageAccount.GetAttributeValue<string>("name"))
                                {
                                    applicationUpdate["ofm_previous_facility_name"] = preImageAccount.GetAttributeValue<string>("name");
                                    updateRequired = true;
                                }
                                //Facility can have multiple addresses
                                if (preImageAccount.GetAttributeValue<OptionSetValue>("ccof_accounttype").Value == (int)ccof_AccountType.Facility &&
                                (preImageAccount.GetAttributeValue<string>("address1_composite") != postImageAccount.GetAttributeValue<string>("address1_composite") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address3_line1") != postImageAccount.GetAttributeValue<string>("ofm_address3_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address3_line2") != postImageAccount.GetAttributeValue<string>("ofm_address3_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address3_city") != postImageAccount.GetAttributeValue<string>("ofm_address3_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address3_province") != postImageAccount.GetAttributeValue<string>("ofm_address3_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address3_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address3_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address4_line1") != postImageAccount.GetAttributeValue<string>("ofm_address4_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address4_line2") != postImageAccount.GetAttributeValue<string>("ofm_address4_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address4_city") != postImageAccount.GetAttributeValue<string>("ofm_address4_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address4_province") != postImageAccount.GetAttributeValue<string>("ofm_address4_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address4_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address4_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address5_line1") != postImageAccount.GetAttributeValue<string>("ofm_address5_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address5_line2") != postImageAccount.GetAttributeValue<string>("ofm_address5_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address5_city") != postImageAccount.GetAttributeValue<string>("ofm_address5_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address5_province") != postImageAccount.GetAttributeValue<string>("ofm_address5_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address5_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address5_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address6_line1") != postImageAccount.GetAttributeValue<string>("ofm_address6_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address6_line2") != postImageAccount.GetAttributeValue<string>("ofm_address6_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address6_city") != postImageAccount.GetAttributeValue<string>("ofm_address6_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address6_province") != postImageAccount.GetAttributeValue<string>("ofm_address6_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address6_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address6_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address7_line1") != postImageAccount.GetAttributeValue<string>("ofm_address7_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address7_line2") != postImageAccount.GetAttributeValue<string>("ofm_address7_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address7_city") != postImageAccount.GetAttributeValue<string>("ofm_address7_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address7_province") != postImageAccount.GetAttributeValue<string>("ofm_address7_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address7_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address7_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address8_line1") != postImageAccount.GetAttributeValue<string>("ofm_address8_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address8_line2") != postImageAccount.GetAttributeValue<string>("ofm_address8_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address8_city") != postImageAccount.GetAttributeValue<string>("ofm_address8_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address8_province") != postImageAccount.GetAttributeValue<string>("ofm_address8_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address8_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address8_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address9_line1") != postImageAccount.GetAttributeValue<string>("ofm_address9_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address9_line2") != postImageAccount.GetAttributeValue<string>("ofm_address9_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address9_city") != postImageAccount.GetAttributeValue<string>("ofm_address9_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address9_province") != postImageAccount.GetAttributeValue<string>("ofm_address9_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address9_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address9_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address10_line1") != postImageAccount.GetAttributeValue<string>("ofm_address10_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address10_line2") != postImageAccount.GetAttributeValue<string>("ofm_address10_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address10_city") != postImageAccount.GetAttributeValue<string>("ofm_address10_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address10_province") != postImageAccount.GetAttributeValue<string>("ofm_address10_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address10_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address10_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address11_line1") != postImageAccount.GetAttributeValue<string>("ofm_address11_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address11_line2") != postImageAccount.GetAttributeValue<string>("ofm_address11_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address11_city") != postImageAccount.GetAttributeValue<string>("ofm_address11_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address11_province") != postImageAccount.GetAttributeValue<string>("ofm_address11_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address11_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address11_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address12_line1") != postImageAccount.GetAttributeValue<string>("ofm_address12_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address12_line2") != postImageAccount.GetAttributeValue<string>("ofm_address12_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address12_city") != postImageAccount.GetAttributeValue<string>("ofm_address12_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address12_province") != postImageAccount.GetAttributeValue<string>("ofm_address12_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address12_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address12_postal_code") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address13_line1") != postImageAccount.GetAttributeValue<string>("ofm_address13_line1") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address13_line2") != postImageAccount.GetAttributeValue<string>("ofm_address13_line2") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address13_city") != postImageAccount.GetAttributeValue<string>("ofm_address13_city") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address13_province") != postImageAccount.GetAttributeValue<string>("ofm_address13_province") ||
                                preImageAccount.GetAttributeValue<string>("ofm_address13_postal_code") != postImageAccount.GetAttributeValue<string>("ofm_address13_postal_code")
                                ))
                                {
                                    var facilityAddresslist = string.Empty;

                                    var addressString = string.Empty;
                                    addressString += preImageAccount.GetAttributeValue<string>("address1_line1");
                                    if (preImageAccount.GetAttributeValue<string>("address1_line2") != null)
                                    {
                                        addressString += ", " + preImageAccount.GetAttributeValue<string>("address1_line2") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_city") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_stateorprovince") + " "
                                                        + preImageAccount.GetAttributeValue<string>("address1_postalcode");
                                    }
                                    else
                                    {
                                        addressString += ", " + preImageAccount.GetAttributeValue<string>("address1_city") + ", "
                                                        + preImageAccount.GetAttributeValue<string>("address1_stateorprovince") + " "
                                                        + preImageAccount.GetAttributeValue<string>("address1_postalcode");
                                    }
                                    facilityAddresslist += addressString + "<br/>";


                                    if (preImageAccount.GetAttributeValue<string>("ofm_address3_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address3_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address3_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address3_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address3_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address3_province") + " " 
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address3_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address3_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address3_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address3_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address4_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address4_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address4_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address4_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address4_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address4_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address4_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address4_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address4_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address4_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address5_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address5_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address5_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address5_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address5_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address5_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address5_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address5_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address5_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address5_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address6_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address6_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address6_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address6_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address6_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address6_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address6_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address6_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address6_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address6_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address7_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address7_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address7_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address7_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address7_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address7_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address7_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address7_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address7_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address7_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address8_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address8_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address8_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address8_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address8_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address8_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address8_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address8_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address8_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address8_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address9_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address9_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address9_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address9_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address9_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address9_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address9_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address9_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address9_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address9_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address10_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address10_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address10_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address10_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address10_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address10_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address10_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address10_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address10_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address10_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address11_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address11_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address11_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address11_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address11_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address11_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address11_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address11_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address11_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address11_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address12_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address12_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address12_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address12_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address12_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address12_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address12_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address12_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address12_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address12_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }
                                    if (preImageAccount.GetAttributeValue<string>("ofm_address13_postal_code") != null)
                                    {
                                        addressString = string.Empty;
                                        addressString += preImageAccount.GetAttributeValue<string>("ofm_address13_line1");
                                        if (preImageAccount.GetAttributeValue<string>("ofm_address13_line2") != null)
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address13_line2") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address13_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address13_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address13_postal_code");
                                        }
                                        else
                                        {
                                            addressString += ", " + preImageAccount.GetAttributeValue<string>("ofm_address13_city") + ", "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address13_province") + " "
                                                            + preImageAccount.GetAttributeValue<string>("ofm_address13_postal_code");
                                        }
                                        facilityAddresslist += "<br/>" + addressString + "<br/>";

                                    }

                                    applicationUpdate["ofm_previous_facility_addresses"] = facilityAddresslist;
                                    updateRequired = true;
                                }
                                if (updateRequired)
                                {
                                    localPluginContext.Trace("Saving changes to ApplicationID: " + applicationUpdate.Id);
                                    localPluginContext.PluginUserService.Update(applicationUpdate);
                                }
                                else
                                {
                                    localPluginContext.Trace("No changes for ApplicationID: " + applicationUpdate.Id);
                                }
                            });
                        }
                        else
                        {
                            localPluginContext.Trace("No related Active Applicaitons found");
                        }
                    }
                }
            }
        }
    }
}
