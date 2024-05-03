using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OFM.Infrastructure.Plugins.Funding
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class GenerateManualSpaceAllocations : PluginBase
    {
        public GenerateManualSpaceAllocations(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GenerateManualSpaceAllocations))
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

            localPluginContext.Trace("Start GenerateManualSpaceAllocations Plug-in");

            if (localPluginContext.Target.Contains(ofm_funding.Fields.ofm_apply_room_split_condition) ||
                localPluginContext.Target.Contains(ofm_funding.Fields.ofm_facility))
            {
                var fundingDetails = localPluginContext.PluginUserService.Retrieve(ofm_funding.EntityLogicalName, localPluginContext.Target.Id, new ColumnSet(true));
                // Getting latest data to get the value
                var roomSplit = localPluginContext.Target.Contains(ofm_funding.Fields.ofm_apply_room_split_condition) ?
                    localPluginContext.Target.GetAttributeValue<bool>(ofm_funding.Fields.ofm_apply_room_split_condition) :
                    fundingDetails.GetAttributeValue<bool>(ofm_funding.Fields.ofm_apply_room_split_condition);

                var facilityId = localPluginContext.Target.Contains(ofm_funding.Fields.ofm_facility) ?
                    localPluginContext.Target.GetAttributeValue<EntityReference>(ofm_funding.Fields.ofm_facility) :
                    fundingDetails.GetAttributeValue<EntityReference>(ofm_funding.Fields.ofm_facility);

                HashSet<string> processedLicenceTypes = new HashSet<string>();
                int count = 0;
                using (var crmContext = new DataverseContext(localPluginContext.PluginUserService))
                {
                    if (roomSplit)
                    {
                        var licence = crmContext.ofm_licenceSet.Where(lic => lic.ofm_facility.Id == facilityId.Id && lic.statecode == ofm_licence_statecode.Active).ToList();
                        if (licence.Count > 0)
                        {
                            licence.ForEach(lic =>
                            {
                                var licenceDetailQuery = new QueryExpression()
                                {
                                    EntityName = ofm_licence_detail.EntityLogicalName,
                                    ColumnSet = new ColumnSet(true),
                                    Criteria = new FilterExpression(LogicalOperator.And)
                                    {
                                        Conditions =
                                        {
                                            new ConditionExpression(ofm_licence_detail.Fields.ofm_licence, ConditionOperator.Equal,lic.Id),
                                            new ConditionExpression(ofm_licence_detail.Fields.statecode, ConditionOperator.Equal,(int)ofm_licence_detail_statecode.Active)
                                        }
                                    }
                                };
                                var licenceDetails = localPluginContext.PluginUserService.RetrieveMultiple(licenceDetailQuery).Entities.Distinct().ToList();

                                licenceDetails.ForEach(record =>
                                {
                                    if (record.Attributes.Contains(ofm_licence_detail.Fields.ofm_licence_type))
                                    {
                                        var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                                        <fetch>
                                          <entity name=""ofm_cclr_ratio"">
                                            <attribute name=""ofm_cclr_ratioid"" />
                                            <attribute name=""ofm_caption"" />
                                            <filter>
                                              <condition attribute=""ofm_licence_mapping"" operator=""contain-values"" >
                                                <value>{record.GetAttributeValue<OptionSetValue>(ofm_licence_detail.Fields.ofm_licence_type).Value}</value>
                                              </condition>
                                            </filter>
                                            <order attribute=""ofm_group_size"" />
                                          </entity>
                                        </fetch>";
                                        EntityCollection cclrRecords = localPluginContext.PluginUserService.RetrieveMultiple(new FetchExpression(fetchXml));
                                        if (cclrRecords != null && cclrRecords.Entities.Count > 0)
                                        {
                                            foreach (var cclrDetail in cclrRecords.Entities)
                                            {
                                                if (!processedLicenceTypes.Contains(cclrDetail.GetAttributeValue<string>(ofm_cclr_ratio.Fields.ofm_caption)))
                                                {
                                                    localPluginContext.Trace("In CCLR Ratio loop" + cclrDetail.Id);
                                                    var spaceAllocation = crmContext.ofm_space_allocationSet.Where(space => space.ofm_cclr_ratio.Id == cclrDetail.Id
                                                       && space.ofm_funding.Id == localPluginContext.Target.Id && space.statecode == ofm_space_allocation_statecode.Inactive);
                                                    if (spaceAllocation.ToList().Count <= 0)
                                                    {
                                                        var entity = new ofm_space_allocation
                                                        {
                                                            ofm_cclr_ratio = new EntityReference(ofm_cclr_ratio.EntityLogicalName, cclrDetail.Id),
                                                            ofm_funding = new EntityReference(ofm_funding.EntityLogicalName, localPluginContext.Target.Id),
                                                            ofm_adjusted_allocation = 0,
                                                            ofm_default_allocation = 0,
                                                            ofm_order_number = ++count
                                                        };
                                                        CreateRequest createRequest = new CreateRequest { Target = entity };
                                                        crmContext.Execute(createRequest);
                                                    }
                                                    else
                                                    {
                                                        var entity = new ofm_space_allocation
                                                        {
                                                            Id = spaceAllocation.FirstOrDefault().Id,
                                                            statecode = ofm_space_allocation_statecode.Active,
                                                            statuscode = ofm_space_allocation_StatusCode.Active,
                                                            ofm_adjusted_allocation = 0,
                                                            ofm_default_allocation = 0
                                                        };
                                                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                                                        crmContext.Execute(updateRequest);
                                                    }
                                                    processedLicenceTypes.Add(cclrDetail.GetAttributeValue<string>(ofm_cclr_ratio.Fields.ofm_caption));
                                                }
                                            }
                                        }
                                    }
                                });
                            });
                            var timeZoneCode = localPluginContext.PluginUserService.Retrieve(UserSettings.EntityLogicalName, localPluginContext.PluginExecutionContext.InitiatingUserId, new ColumnSet(UserSettings.Fields.timezonecode));
                            localPluginContext.Trace("time zone code retrieved");
                            var timeZoneQuery = new QueryExpression()
                            {
                                EntityName = TimeZoneDefinition.EntityLogicalName,
                                ColumnSet = new ColumnSet(TimeZoneDefinition.Fields.standardname),
                                Criteria = new FilterExpression
                                {
                                    Conditions =
                                {
                                    new ConditionExpression(TimeZoneDefinition.Fields.timezonecode, ConditionOperator.Equal,timeZoneCode.GetAttributeValue<int>(UserSettings.Fields.timezonecode))
                                }
                                }
                            };
                            var result = localPluginContext.PluginUserService.RetrieveMultiple(timeZoneQuery);

                            var new_allocated_date = string.Format("{0:yyyy/MM/dd hh:mm tt}", TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                                TimeZoneInfo.FindSystemTimeZoneById(result.Entities.Select(t => t.GetAttributeValue<string>
                                (TimeZoneDefinition.Fields.standardname)).FirstOrDefault().ToString())));

                            localPluginContext.Trace("new date allocated " + new_allocated_date);
                            var entityFunding = new ofm_funding
                            {
                                Id = localPluginContext.Target.Id,
                                ofm_new_allocation_date = DateTime.Parse(new_allocated_date)
                            };
                            UpdateRequest updateFundingRequest = new UpdateRequest { Target = entityFunding };
                            crmContext.Execute(updateFundingRequest);
                        }
                    }
                    else
                    {
                        var spaceAllocation = crmContext.ofm_space_allocationSet.Where(space => space.ofm_funding.Id == localPluginContext.Target.Id)
                                                    .Where(space => space.statecode == ofm_space_allocation_statecode.Active).ToList();
                        spaceAllocation.ForEach(allocation =>
                        {
                            var entityToUpdate = new ofm_space_allocation
                            {
                                Id = allocation.Id,
                                statecode = ofm_space_allocation_statecode.Inactive,
                                statuscode = ofm_space_allocation_StatusCode.Inactive
                            };
                            UpdateRequest updateRequest = new UpdateRequest { Target = entityToUpdate };
                            crmContext.Execute(updateRequest);
                        });
                    }
                }
            }
        }
    }
}