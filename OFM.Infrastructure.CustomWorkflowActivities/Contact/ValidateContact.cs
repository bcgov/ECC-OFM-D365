using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;

namespace OFM.Infrastructure.CustomWorkflowActivities.Contact
{
    public sealed class ValidateContact : CodeActivity
    {     
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();

            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);

            tracingService.Trace("{0}{1}", "Start Custom Workflow Activity: ValidateRequest", DateTime.Now.ToLongTimeString());
            var recordId = context.PrimaryEntityId;
            tracingService.Trace("{0}", "Loading data");

            //Create the request
            RetrieveRequest request = new RetrieveRequest();
            request.ColumnSet = new ColumnSet(new string[] { "lastname" });
            request.Target = new EntityReference(ECC.Core.DataContext.Contact.EntityLogicalName, recordId);

            //Retrieve the entity to determine what the birthdate is set at
            Entity entity = (Entity)((RetrieveResponse)service.Execute(request)).Entity;

            //VALIDATION STARTS HERE
            bool isValid = true;

            this.valid.Set(executionContext, isValid);
            this.message.Set(executionContext, "a message.");
        }

        [Output("Valid")]
        public OutArgument<bool> valid { get; set; }

        [Output("Validate Message")]
        public OutArgument<string> message { get; set; }
    }
}