﻿"use strict";

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
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.UpdateOrganizationdetails(executionContext);
                break;

            case 2: // update 
                this.licenceDetailsFromFacility(formContext);
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.licenceCheck(executionContext);
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
        //debugger;
        let formContext = executionContext.getFormContext();
        this.licenceDetailsFromFacility(formContext);
    },

    licenceDetailsFromFacility: function (formContext) {
        var facilityId = formContext.getAttribute("ofm_facility").getValue() ? formContext.getAttribute("ofm_facility").getValue()[0].id : null;
        if (facilityId != null) {
            var conditionFetchXML = "";
            Xrm.WebApi.retrieveMultipleRecords("ofm_licence", "?$select=ofm_licence&$filter=(_ofm_facility_value eq " + facilityId + ")").then(
                function success(results) {
                    console.log(results);
                    for (var i = 0; i < results.entities.length; i++) {
                        var result = results.entities[i];
                        conditionFetchXML += "<value>{" + result["ofm_licenceid"] + "}</value>";
                    }
                    var fetchLicenceDetail = "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>" +
                        "<entity name = 'ofm_licence_detail' >" +
                        "<attribute name='ofm_licence_type' />" +
                        "<attribute name='ofm_overnight_care' />" +
                        "<attribute name='ofm_licence_spaces' />" +
                        "<attribute name='ofm_operational_spaces' />" +
                        "<attribute name='ofm_enrolled_spaces' />" +
                        "<attribute name='ofm_weeks_in_operation' />" +
                        "<attribute name='ofm_week_days' />" +
                        "<attribute name='ofm_operation_from_time' />" +
                        "<attribute name='ofm_operations_to_time' />" +
                        "<attribute name='ofm_care_type' />" +
                        "<filter type='and'>" +
                        "<condition attribute='ofm_licence' operator='in'>" +
                        conditionFetchXML +
                        "</condition>" +
                        "</filter>" +
                        "</entity >" +
                        "</fetch > ";
                    console.log(fetchLicenceDetail);
                    OFM.Application.Form.addSubgridEventListener(formContext, fetchLicenceDetail, "Subgrid_new_2");

                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    },

    //A function called to filter active contacts associated to organization (lookup on Organization form)
    filterPrimaryContactLookup: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	    // get current form's label
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;

            var viewId = "{00000000-0000-0000-0000-000000000090}";
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
                "<condition attribute='ofm_portal_access' operator='eq' value='1' />" +
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

    // function to validate seconday and primary contact
    validateSecondaryContact: function (executionContext) {
        //debugger;
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
    addSubgridEventListener: function (formContext, filterFetchXML, subgridName) {
        var gridContext = formContext.getControl(subgridName);
        //ensure that the subgrid is ready, if not wait and call this function again
        if (gridContext == null) {
            setTimeout(function () { this.addSubgridEventListener(recordid, formContext); }, 500);
            return;
        }

        gridContext.setFilterXml(filterFetchXML);
        gridContext.refresh();
    },

    //function to check if the facility has atleast 1 licence and licence details
    licenceCheck: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var facilityId = formContext.getAttribute("ofm_facility").getValue() ? formContext.getAttribute("ofm_facility").getValue()[0].id : null;
        var message = "Facility needs to have atleast 1 Licence Type for Licence : ";
        Xrm.WebApi.retrieveMultipleRecords("ofm_licence", "?$select=ofm_licence&$filter=(_ofm_facility_value eq " + facilityId + " and statecode eq 0)").then(
            function success(results) {
                if (results.entities.length <= 0) {
                    formContext.ui.setFormNotification("Facility needs to have atleast 1 Licence. Please go to the facility and add atleast 1 licence.", "ERROR", "licence");
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

    CreateFundingRecord: function (executionContext) {
        //debugger;
        var formContext = executionContext;
        var recordId = formContext.data.entity.getId();
        Xrm.WebApi.retrieveMultipleRecords("workflow", "?$select=workflowid&$filter=(name eq 'OFM - Application: Creating Funding Record' and statecode eq 1 and type eq 1)").then(
            function success(results) {
                console.log(results);
                    // Columns
                var workflowid = results.entities[0]["workflowid"]; // Guid
                //}
                var executeWorkflowRequest = {
                    entity: { entityType: "workflow", id: workflowid },
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
            },
            function (error) {
                console.log(error.message);
            });
    },

    filterExpenseAuthorityLookup: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;

            var viewId = "{00000000-0000-0000-0000-000000000089}";
            var entity = "contact";
            var ViewDisplayName = "Facility Expense Authority Contacts";
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
                "<condition attribute='ofm_is_expense_authority' operator='eq' value='1' />" +
                "</filter></link-entity></entity></fetch>";

            var layout = "<grid name='resultset' jump='fullname' select='1' icon='1' preview='1'>" +
                "<row name = 'result' id = 'contactid' >" +
                "<cell name='fullname' width='300' />" +
                "<cell name='ccof_username' width='125' />" +
                "<cell name='emailaddress1' width='150' />" +
                "<cell name='parentcustomerid' width='150' />" +
                "</row></grid>";

            formContext.getControl("ofm_expense_authority").addCustomView(viewId, entity, ViewDisplayName, fetchXML, layout, true);

        }
        else {
            formContext.getAttribute("ofm_expense_authority").setValue(null);
        }
        // perform operations on record retrieval
    },
}