using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Linq;

namespace OFM.Infrastructure.CustomWorkflowActivities.Contact
{
    public sealed class GenerateFacilityPermissions : CodeActivity
    {
        [ReferenceTarget("account")]
        [RequiredArgument]
        [Input("ParentCustomerID")]
        public InArgument<EntityReference> parentCustomerID { get; set; }

        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: GenerateFacilityPermissions", DateTime.Now.ToLongTimeString());
            var organizationID = this.parentCustomerID.Get(executionContext);
            using (var crmContext = new DataverseContext(service))
            {
                var oldPermissions = crmContext.ofm_bceid_facilitySet.Where(permission => permission.ofm_bceid.Id == context.PrimaryEntityId).ToList();
                tracingService.Trace($"Total Old permissions count {oldPermissions.Count}");

                oldPermissions.ForEach(record =>
                {
                    if (record.Attributes.Contains(ofm_bceid_facility.Fields.statuscode))
                    {
                        var entity = new ofm_bceid_facility
                        {
                            Id = record.Id,
                            statecode = ofm_bceid_facility_statecode.Inactive,
                            statuscode = ofm_bceid_facility_StatusCode.Inactive
                        };

                        UpdateRequest updateRequest = new UpdateRequest { Target = entity };
                        crmContext.Execute(updateRequest);

                    }
                });

                var newFacilityPermissions = crmContext.AccountSet.Where(facility => facility.parentaccountid.Id == organizationID.Id).ToList();
                tracingService.Trace($"Total New child facilities count {newFacilityPermissions.Count}");

                newFacilityPermissions.ForEach(record =>
                {
                    if (record.Attributes.Contains(Account.Fields.statuscode) &&
                        record.GetAttributeValue<OptionSetValue>(Account.Fields.statuscode).Value == Convert.ToInt32(Account_StatusCode.Active))
                    {
                        var entity = new ofm_bceid_facility
                        {
                            ofm_bceid = new EntityReference(ECC.Core.DataContext.Contact.EntityLogicalName, context.PrimaryEntityId),
                            ofm_facility = new EntityReference(Account.EntityLogicalName, record.Id)
                        };

                        CreateRequest createRequest = new CreateRequest { Target = entity };
                        crmContext.Execute(createRequest);
                    }
                });

                tracingService.Trace("Completed with no errors.");
            }
        }
    }
}