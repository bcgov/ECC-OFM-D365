using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace OFM.Infrastructure.CustomWorkflowActivities.PCMReview
{
    public sealed class GetIncompletePCMCount : CodeActivity
    {
        [Output("Incomplete PCM Count")]
        public OutArgument<int> incompletepcmcount { get; set; }
        protected override void Execute(CodeActivityContext executionContext)
        {
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();
            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            //Create an Organization Service
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.InitiatingUserId);
            var recordId = context.PrimaryEntityId;
            tracingService.Trace("Begin get not complete PCM Review records, recordId: {0}", recordId);
            try
            {
                // StatusReason 4 Pass, 5 Fail
                var fetchXml = $@"<?xml version=""1.0"" encoding=""utf-16""?>
                        <fetch>
                          <entity name=""ofm_pcm_review"">
                            <attribute name=""createdon"" />
                            <attribute name=""ofm_application"" />
                            <attribute name=""ofm_name"" />
                            <attribute name=""ofm_pcm_reviewid"" />
                            <attribute name=""ofm_reason"" />
                            <attribute name=""statuscode"" />
                            <filter>
                              <condition attribute=""ofm_application"" operator=""eq"" value=""{recordId}"" />
                              <condition attribute=""statuscode"" operator=""ne"" value=""4"" />
                              <condition attribute=""statuscode"" operator=""ne"" value=""5"" />
                            </filter>
                          </entity>
                        </fetch>";
                EntityCollection pcmReviews = service.RetrieveMultiple(new FetchExpression(fetchXml));
                tracingService.Trace("PCM Records number which is not complete:" + pcmReviews.Entities.Count);
                this.incompletepcmcount.Set(executionContext, pcmReviews.Entities.Count);
                tracingService.Trace("Workflow activity end.");
            }
            catch (Exception ex)
            {
                throw new InvalidWorkflowException("Exeception in Custom Workflow -" + ex.Message + ex.InnerException);
            }
        }
    }
}