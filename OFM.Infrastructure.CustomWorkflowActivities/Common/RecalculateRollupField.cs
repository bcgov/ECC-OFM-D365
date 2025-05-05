using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFM.Infrastructure.CustomWorkflowActivities.Common
{
    /// <summary>
    /// Custom workflow activity to recalculate Rollup Field
    /// </summary>
     public class CalculateRollupField : CodeActivity
    {
        #region "Parameter Definition"
        [RequiredArgument]
        [Input("FieldName")]
        [Default("")]        
        public InArgument<String> FieldName { get; set; }

        [RequiredArgument]
        [Input("Parent Record URL")]
        [ReferenceTarget("")]
        public InArgument<String> ParentRecordURL { get; set; }
        #endregion

        protected override void Execute(CodeActivityContext executionContext)
        {

            #region "Load CRM Service from context"

            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService.Trace("Load CRM Service from context --- OK");
            #endregion

            #region "Read Parameters"
            String _FieldName = this.FieldName.Get(executionContext);
            tracingService.Trace("_FieldName=" + _FieldName);
            String _ParentRecordURL = this.ParentRecordURL.Get(executionContext);

            if (_ParentRecordURL == null || _ParentRecordURL == "")
            {
                return;
            }
            tracingService.Trace("_ParentRecordURL=" + _ParentRecordURL);
            string[] urlParts = _ParentRecordURL.Split("?".ToArray());
            string[] urlParams=urlParts[1].Split("&".ToCharArray());
            
            string ParentObjectTypeCode=urlParams[0].Replace("etc=","");
            string ParentId = urlParams[1].Replace("id=", "");
            tracingService.Trace("ParentObjectTypeCode=" + ParentObjectTypeCode + "--ParentId=" + ParentId);
            #endregion


            #region "CalculateRollupField Execution"
            string ParentEntityName = sGetEntityNameFromCode(ParentObjectTypeCode, service);
            CalculateRollupFieldRequest calculateRollup = new CalculateRollupFieldRequest();
            calculateRollup.FieldName = _FieldName;
            calculateRollup.Target = new EntityReference(ParentEntityName, new Guid(ParentId));
            CalculateRollupFieldResponse resp = (CalculateRollupFieldResponse)service.Execute(calculateRollup);
            #endregion
            
        }
        private string sGetEntityNameFromCode(string ObjectTypeCode, IOrganizationService service)
        {
            MetadataFilterExpression entityFilter = new MetadataFilterExpression(LogicalOperator.And);
            entityFilter.Conditions.Add(new MetadataConditionExpression("ObjectTypeCode", MetadataConditionOperator.Equals, Convert.ToInt32(ObjectTypeCode)));
            EntityQueryExpression entityQueryExpression = new EntityQueryExpression()
            {
                Criteria = entityFilter
            };
            RetrieveMetadataChangesRequest retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest()
            {
                Query = entityQueryExpression,
                ClientVersionStamp = null
            };
            RetrieveMetadataChangesResponse response = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);

            EntityMetadata entityMetadata = (EntityMetadata)response.EntityMetadata[0];
            return entityMetadata.SchemaName.ToLower();
        }

    }
}
