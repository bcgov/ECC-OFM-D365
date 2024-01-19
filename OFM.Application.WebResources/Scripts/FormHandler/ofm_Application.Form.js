"use strict";

//Create Namespace Object 
var organizationid;
var pageContext;
var OFM = OFM || {};
OFM.Application = OFM.Application || {};
OFM.Application.Form = OFM.Application.Form || {};

//Formload logic starts here
OFM.Application.Form = {
    onLoad: function (executionContext) {
        //debugger;
        let formContext = executionContext.getFormContext();
        pageContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.addSubgridEventListener("00000000-0000-0000-0000-000000000000", formContext);
                this.filterPrimaryContactLookup(executionContext);
                this.UpdateOrganizationdetails(executionContext);
                break;

            case 2: // update  
                this.addSubgridEventListener("00000000-0000-0000-0000-000000000000", formContext);
                this.filterPrimaryContactLookup(executionContext);
                this.licenceCheck(formContext);
                break;

            case 3: //readonly
                break;

            case 4: //disable
                break;

            case 6: //bulkedit
                break;
        }
    },


    //A function called on save
    onSave: function (executionContext) {

    },



    //A function called to filter active contacts associated to organization (lookup on Organization form)
    filterPrimaryContactLookup: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	    // get current form's label
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;

            var viewId = "{00000000-0000-0000-0000-000000000089}";
            var entity = "contact";
            var ViewDisplayName = "Facility Contacts";
            var fetchXML = "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>" +
                "<entity name='contact'>" +
                "<attribute name='fullname' />" +
                "<attribute name='ccof_username' />" +
                "<attribute name='parentcustomerid' />" +
                "<attribute name='emailaddress1' />" +
                "<attribute name='contactid' />" +
                "<order attribute='fullname' descending='false' />" +
                "<link-entity name='ofm_bceid_facility' from='ofm_bceid' to='contactid' link-type='inner' alias='an'>" +
                "<filter type='and'>" +
                "<condition attribute='ofm_facility' operator='eq'  uitype='account' value='" + facilityid + "'/>" +
                "</filter></link-entity></entity></fetch>";


            var layout = "<grid name='resultset' jump='fullname' select='1' icon='1' preview='1'>" +
                "<row name = 'result' id = 'contactid' >" +
                "<cell name='fullname' width='300' />" +
                "<cell name='ccof_username' width='125' />" +
                "<cell name='emailaddress1' width='150' />" +
                "<cell name='parentcustomerid' width='150' />" +
                "</row></grid>";

            formContext.getControl("ofm_contact").addCustomView(viewId, entity, ViewDisplayName, fetchXML, layout, true);
            formContext.getControl("ofm_secondary_contact").addCustomView(viewId, entity, ViewDisplayName, fetchXML, layout, true);

        }
        else {
            formContext.getAttribute("ofm_contact").setValue(null);
            formContext.getAttribute("ofm_secondary_contact").setValue(null);

        }
        // perform operations on record retrieval
    },

    //function called on Licence Editable grid event:OnRecordSelect, to filter the associated Licence Details 
    subgridOnSelect: function (executionContext) {
        var selected = executionContext.getFormContext().data.entity;
        var Id = selected.getId();
        var formContext = window.pageContext;
        this.addSubgridEventListener(Id, formContext);
    },
    // function to validate seconday and primary contact
    validateSecondaryContact: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var primaryContact = formContext.getAttribute("ofm_contact").getValue();
        var secondaryContact = formContext.getAttribute("ofm_secondary_contact").getValue();
        if (primaryContact != null && secondaryContact != null) {
            if (primaryContact[0].id == secondaryContact[0].id) {
                formContext.ui.setFormNotification("Primary and secondary contact can not be same.", "ERROR", "ContactValidation");
                formContext.getAttribute("ofm_secondary_contact").setValue(null);
            }
            else {
                formContext.ui.clearFormNotification("ContactValidation");
            }
        }

    },
    // function to filter the licence details grid based on record selected in licence grid
    addSubgridEventListener: function (recordid, formContext) {
        var gridContext = formContext.getControl("Subgrid_new_2");
        //ensure that the subgrid is readyâ€¦if not wait and call this function again
        if (gridContext == null) {
            setTimeout(function () { this.addSubgridEventListener(recordid, formContext); }, 500);
            return;
        }
        //bind the event listener when the subgrid is ready
        var fetchxml = "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>" +
            "  <entity name='ofm_licence_detail'>" +
            "   <attribute name='ofm_care_type' />" +
            "   <attribute name = 'ofm_enrolled_spaces' />" +
            "   <attribute name='ofm_licence' />" +
            "   <attribute name='ofm_licence_detailid' />" +
            "   <attribute name='ofm_licence_spaces' />" +
            "   <attribute name='ofm_licence_type' />" +
            "   <attribute name='ofm_operational_spaces' />" +
            "   <attribute name='ofm_overnight_care' />" +
            "   <attribute name='ofm_week_days' />" +
            "   <attribute name='ofm_weeks_in_operation' />" +
            "    <filter type='and'>" +
            "      <condition attribute='ofm_licence' operator='eq' value='" + recordid.replace("{", "").replace("}", "") + "' />" +
            "    </filter>" +
            "  </entity>" +
            "</fetch>";

        gridContext.setFilterXml(fetchxml);
        gridContext.refresh();
    },

    //function to check if the application has atleast 1 licence and licence details
    licenceCheck: function (formContext) {
        debugger;
        var applicationId = formContext.data.entity.getId().replace("{", "").replace("}", "");
        var message = "Application needs to have atleast 1 Licence Type for Licence : ";
        Xrm.WebApi.retrieveMultipleRecords("ofm_licence", "?$select=ofm_licence&$filter=(_ofm_application_value eq " + applicationId + " and statecode eq 0)").then(
            function success(results) {
                if (results.entities.length <= 0) {
                    formContext.ui.setFormNotification("Application needs to have atleast 1 Licence. Please go to Licence tab to add 1 licence.", "ERROR", "licence");
                }
                else {
                    formContext.ui.clearFormNotification("licence");
                    for (var i = 0; i < results.entities.length; i++) {
                        var result = results.entities[i];
                        // Columns
                        var licenceId = result["ofm_licenceid"]; // Guid
                        var licenceName = result["ofm_licence"];
                        Xrm.WebApi.retrieveMultipleRecords("ofm_licence_detail", "?$filter=(statecode eq 0 and _ofm_licence_value eq " + licenceId + ")").then(
                            function success(results) {
                                if (results.entities.length <= 0) {
                                    message += licenceName + ",";
                                    formContext.ui.setFormNotification(message, "ERROR", "licence");
                                }
                            },
                            function (error) {
                                console.log(error.message);
                            }
                        );
                    }
                }
            },
            function (error) {
                console.log(error.message);
            }
        );
    },
    UpdateOrganizationdetails: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;
            Xrm.WebApi.retrieveRecord("account", facilityid, "?$select=_parentaccountid_value").then(
                function success(results) {
                    console.log(results);
                    if (results["_parentaccountid_value"] != null) {
                        var lookup = new Array();
                        lookup[0] = new Object;
                        lookup[0].id = results["_parentaccountid_value"];
                        lookup[0].name = results["_parentaccountid_value@OData.Community.Display.V1.FormattedValue"];
                        lookup[0].entityType = results["_parentaccountid_value@Microsoft.Dynamics.CRM.lookuplogicalname"];
                        Xrm.Page.getAttribute("ofm_organization").setValue(lookup);
                    }
                    else {
                        Xrm.Page.getAttribute("ofm_organization").setValue(null);
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
        else {
            Xrm.Page.getAttribute("ofm_organization").setValue(null);
        }
    },

    RecalculateFundings: function (executionContext) {
        debugger;
        //alert("Recalculation initiated!");
        var formContext = executionContext;
        var recordId = formContext.data.entity.getId();
        var pageInput = {
            pageType: "custom",
            name: "ofm_ofmfundingrecalculation_08432",
            entityName: "ofm_application",
            recordId: recordId
        };
        var navigationOptions = {
            target: 2,
            position: 1,
            height: 720,
            width: 600,
            title: "Recalculate the fundings"
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions);
    },
    CreateFundingRecord: function (executionContext) {
        debugger;
        var formContext = executionContext;
        var recordId = formContext.data.entity.getId();
        var statusReason = formContext.getAttribute("statuscode").getValue();
        var FundingBaseNum = formContext.getAttribute("ofm_funding_number_base").getValue();
        var parameters = { EntityId: recordId };

        var executeWorkflowRequest = {
            entity: { entityType: "workflow", id: "2752f03f-8db5-ee11-a569-000d3af4865d" },
            EntityId: { guid: recordId },
            getMetadata: function () {
                return {
                    boundParameter: "entity",
                    parameterTypes: {
                        entity: { typeName: "mscrm.workflow", structuralProperty: 5 },
                        EntityId: { typeName: "Edm.Guid", structuralProperty: 1 }
                    },
                    operationType: 0, operationName: "ExecuteWorkflow"
                };
            }
        };
        Xrm.WebApi.execute(executeWorkflowRequest).then(
            function success(response) {
                if (response.ok) {
                    return response.json();
                    alert("create funding sucessfully!");
                }
            }
        ).then(function (responseBody) {
            var result = responseBody;
            console.log(result);
        }).catch(function (error) {
            console.log(error.message);
        });
    }
}