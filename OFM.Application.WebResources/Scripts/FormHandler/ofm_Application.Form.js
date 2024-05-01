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
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.UpdateOrganizationdetails(executionContext);
                this.showBanner(executionContext);
                this.filterCreatedBySPLookup(executionContext);
                break;

            case 2: // update
                this.licenceDetailsFromFacility(executionContext);
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.licenceCheck(executionContext);
                this.showBanner(executionContext);
                this.lockStatusReason(executionContext);
                this.filterCreatedBySPLookup(executionContext);
                break;

            case 3: //readonly
                this.showBanner(executionContext);
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
        this.licenceDetailsFromFacility(executionContext);
    },

    licenceDetailsFromFacility: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var facilityId = formContext.getAttribute("ofm_facility").getValue() ? formContext.getAttribute("ofm_facility").getValue()[0].id : null;
        var submittedOnDate = formContext.getAttribute("ofm_summary_submittedon").getValue();
        if (submittedOnDate != null) {
            var date = submittedOnDate.toISOString();
        }
        else
            var date = formContext.getAttribute("createdon").getValue() != null ?
                formContext.getAttribute("createdon").getValue().toISOString() : new Date();
        if (facilityId != null) {
            var conditionFetchXML = "";
            Xrm.WebApi.retrieveMultipleRecords("ofm_licence", "?$select=ofm_licence&$filter=(_ofm_facility_value eq " + facilityId + " and statecode eq 0 and ((ofm_end_date eq null and Microsoft.Dynamics.CRM.OnOrBefore(PropertyName='ofm_start_date',PropertyValue='" + date + "')) or (Microsoft.Dynamics.CRM.OnOrAfter(PropertyName='ofm_end_date',PropertyValue='" + date + "') and Microsoft.Dynamics.CRM.OnOrBefore(PropertyName='ofm_start_date',PropertyValue='" + date + "'))))").then(
                function success(results) {
                    console.log(results);
                    if (results.entities.length > 0) {
                        for (var i = 0; i < results.entities.length; i++) {
                            var result = results.entities[i];
                            conditionFetchXML += "<value>{" + result["ofm_licenceid"] + "}</value>";
                        }
                    }
                    else {
                        conditionFetchXML += "<value>{00000000-0000-0000-0000-000000000000}</value>"
                    }
                    var fetchLicenceXML = "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>" +
                        "<entity name = 'ofm_licence' >" +
                        "<attribute name='ofm_licence' />" +
                        "<attribute name='ofm_health_authority' />" +
                        "<attribute name='ofm_ccof_facilityid' />" +
                        "<attribute name='ofm_tdad_funding_agreement_number' />" +
                        "<attribute name='ofm_ccof_organizationid' />" +
                        "<attribute name='ofm_accb_providerid' />" +
                        "<attribute name='ofm_start_date' />" +
                        "<attribute name='ofm_end_date' />" +
                        "<filter type='and' >" +
                        "<condition attribute = 'ofm_facility' operator = 'eq' uitype = 'account' value = '" + facilityId + "' />" +
                        "<filter type='or' >" +
                        "<filter type='and' >" +
                        "<condition attribute='ofm_end_date' operator='null' />" +
                        "<condition attribute='ofm_start_date' operator='on-or-before' value='" + date + "' />" +
                        "</filter>" +
                        "<filter type='and' >" +
                        "<condition attribute='ofm_end_date' operator='on-or-after' value='" + date + "' />" +
                        "<condition attribute='ofm_start_date' operator='on-or-before' value='" + date + "' />" +
                        "</filter>" +
                        "</filter>" +
                        "</filter >" +
                        "</entity >" +
                        "</fetch > ";
                    console.log(fetchLicenceXML);
                    OFM.Application.Form.addSubgridEventListener(formContext, fetchLicenceXML, "Subgrid_new_5");
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
        filterCreatedBySPLookup: function (executionContext) {
            debugger;
            var formContext = executionContext.getFormContext();
            var facility = formContext.getAttribute("ofm_facility").getValue();
            var facilityid;
            if (facility != null) {
                facilityid = facility[0].id;
    
                var viewId = "{00000000-0000-0000-0000-000000000091}";
                var entity = "contact";
                var ViewDisplayName = "Facility Created By Contacts";
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
    
                formContext.getControl("ofm_createdby").addCustomView(viewId, entity, ViewDisplayName, fetchXML, layout, true);
    
            }
            else {
                formContext.getAttribute("ofm_createdby").setValue(null);
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
            setTimeout(function () { this.addSubgridEventListener(formContext, filterFetchXML, subgridName); }, 500);
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

    //Last Updated on and Last Updated by on verification checklist
    updateVerificationInfo: function (executionContext, lastUpdatedOnField, LastUpdatedByField) {
        debugger;
        var formContext = executionContext.getFormContext();
        var currentDate = new Date();
        var userSettings = Xrm.Utility.getGlobalContext().userSettings;
        var username = userSettings.userName;
        formContext.getAttribute(lastUpdatedOnField) != null ? formContext.getAttribute(lastUpdatedOnField).setValue(currentDate) : null;
        formContext.getAttribute(LastUpdatedByField) != null ? formContext.getAttribute(LastUpdatedByField).setValue(username) : null;
    },

    showBanner: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var roomSplitIndicator = formContext.getAttribute("ofm_room_split_indicator").getValue();
        var pcmIndicator = formContext.getAttribute("ofm_pcm_indicator").getValue();
        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_5").setVisible(roomSplitIndicator || pcmIndicator);
        formContext.ui.tabs.get("tab_9").sections.get("tab_9_banner").setVisible(pcmIndicator);
        formContext.getControl("ofm_room_split_banner").setVisible(roomSplitIndicator);
        formContext.getControl("ofm_pcm_banner").setVisible(pcmIndicator);
        formContext.getControl("ofm_pcm_banner1").setVisible(pcmIndicator);
    },

    lockStatusReason: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        if (formContext.getAttribute("statuscode").getValue() != 1) {
            var roles = Xrm.Utility.getGlobalContext().userSettings.roles.getAll();
            var disable = true;
            for (var i = 0; i < roles.length; i++) {
                var name = roles[i].name;
                if (name == "System Administrator" || name == "OFM - System Administrator") {
                    disable = false;
                    break;
                }
            }
            formContext.getControl("header_statuscode").setDisabled(disable);
        }
        else
            formContext.getControl("header_statuscode").setDisabled(false);
    }
}