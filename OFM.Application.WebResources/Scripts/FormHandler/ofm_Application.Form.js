"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.Application = OFM.Application || {};
OFM.Application.Form = OFM.Application.Form || {};

//Formload logic starts here
OFM.Application.Form = {
    onLoad: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.UpdateOrganizationdetails(executionContext);
                this.showBanner(executionContext);
                this.filterCreatedBySPLookup(executionContext);
                this.filterSubmittedByLookup(executionContext);
                this.showCCFRIParticipation(executionContext);
                break;

            case 2: // update
                this.licenceDetailsFromFacility(executionContext);
                this.filterPrimaryContactLookup(executionContext);
                this.filterExpenseAuthorityLookup(executionContext);
                this.licenceCheck(executionContext);
                this.showBanner(executionContext);
                //this.lockStatusReason(executionContext);
                this.filterCreatedBySPLookup(executionContext);
                this.hideVerificationTab(executionContext);
                this.filterSubmittedByLookup(executionContext);
                this.showUnionList(executionContext);
                this.showOtherDescription(executionContext);
                this.notForProfitSection(executionContext);
                this.showCCFRIParticipation(executionContext);
                this.hideEligibilityTab(executionContext);
                break;

            case 3: //readonly
                this.licenceDetailsFromFacility(executionContext);
                this.showBanner(executionContext);
                this.hideVerificationTab(executionContext);
                this.showUnionList(executionContext);
                this.showOtherDescription(executionContext);
                this.notForProfitSection(executionContext);
                this.showCCFRIParticipation(executionContext);
                this.hideEligibilityTab(executionContext);
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
        this.licenceDetailsFromFacility(executionContext);
        this.showBanner(executionContext);
    },

    licenceDetailsFromFacility: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var facilityId = formContext.getAttribute("ofm_facility").getValue() ? formContext.getAttribute("ofm_facility").getValue()[0].id : null;
        var submittedOnDate = formContext.getAttribute("ofm_summary_submittedon").getValue();
        if (submittedOnDate != null) {
            submittedOnDate.setMinutes(submittedOnDate.getMinutes() - submittedOnDate.getTimezoneOffset());
            var date = submittedOnDate.toISOString();
        }
        else {
            var createdOn = formContext.getAttribute("createdon").getValue() != null ?
                formContext.getAttribute("createdon").getValue() : new Date();
            createdOn.setMinutes(createdOn.getMinutes() - createdOn.getTimezoneOffset());
            var date = createdOn.toISOString();
        }
        if (facilityId != null) {
            var conditionFetchXML = "";
            Xrm.WebApi.retrieveMultipleRecords("ofm_licence", "?$select=ofm_licence&$filter=((_ofm_facility_value eq " + facilityId + " and statecode eq 0 and ofm_start_date le " + date + ") or (ofm_end_date eq null and ofm_end_date ge " + date + "))").then(
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

    filterSubmittedByLookup: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;

            var viewId = "{00000000-0000-0000-0000-000000000091}";
            var entity = "contact";
            var ViewDisplayName = "Facility Submitted By Contacts";
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

            formContext.getControl("ofm_summary_submittedby").addCustomView(viewId, entity, ViewDisplayName, fetchXML, layout, true);

        }
        else {
            formContext.getAttribute("ofm_summary_submittedby").setValue(null);
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
                        Xrm.WebApi.retrieveRecord("account", results["_parentaccountid_value"], "?$select=ofm_business_type").then(

                            function success(result) {
                                console.log(result);
                                if (result["ofm_business_type"] != null) {
                                    var businessType = result["ofm_business_type"]; // Choice
                                    if (businessType == 2)
                                        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(true);
                                    else
                                        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
                                }
                                else {
                                    formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
                                }
                            },
                            function (error) {
                                console.log(error.message);
                            }
                        );
                    }
                    else {
                        Xrm.Page.getAttribute("ofm_organization").setValue(null);
                        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
        else {
            Xrm.Page.getAttribute("ofm_organization").setValue(null);
            formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
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
        var providerType = formContext.getAttribute("ofm_provider_type").getValue();
        var unionizedFlag = formContext.getAttribute("ofm_unionized").getValue();

        var status = formContext.getAttribute("statecode").getValue();
        var statusReason = formContext.getAttribute("statuscode").getValue();
        var supplementaryIndicator = formContext.getAttribute("ofm_supplementary_indicator").getValue();
        var review_flag = false;
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;

        if (facility != null) {
            facilityid = facility[0].id;
            Xrm.WebApi.retrieveRecord("account", facilityid, "?$select=ofm_flag_vau_review_underway").then(

                function success(results) {
                    console.log(results);
                    if (results["ofm_flag_vau_review_underway"] != null) {
                        review_flag = results["ofm_flag_vau_review_underway"];
                    }
                    formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_5").setVisible(roomSplitIndicator || pcmIndicator || supplementaryIndicator || providerType === 2 || unionizedFlag === 1 || review_flag);
                    formContext.getControl("ofm_review_underway_banner").setVisible(review_flag);
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
        else {
            formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_5").setVisible(roomSplitIndicator || pcmIndicator || supplementaryIndicator || providerType === 2 || unionizedFlag === 1 || review_flag);
            formContext.getControl("ofm_review_underway_banner").setVisible(review_flag);
        }

        formContext.ui.tabs.get("tab_9").sections.get("tab_9_banner").setVisible(pcmIndicator);
        formContext.getControl("ofm_room_split_banner").setVisible(roomSplitIndicator);
        formContext.getControl("ofm_pcm_banner").setVisible(pcmIndicator);
        formContext.getControl("ofm_pcm_banner1").setVisible(pcmIndicator);
        formContext.getControl("ofm_familyprovider_banner").setVisible(providerType === 2);
        formContext.getControl("ofm_unionizedsite_banner").setVisible(unionizedFlag === 1);
        if (status == 0 && statusReason != 6)
            formContext.getControl("ofm_supplementary_banner").setVisible(supplementaryIndicator);
    },

    lockStatusReason: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        if (formContext.getAttribute("statuscode").getValue() == 6) {  //Approved
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
    },
    hideVerificationTab: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        if (userRoles.getLength() > 1) { }

        else if (userRoles.get()[0].name == "OFM - Read Only") {
            formContext.ui.tabs.get("tab_9").setVisible(false);
        }
    },

    showUnionList: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var unionized = formContext.getAttribute("ofm_unionized").getValue();
        if (unionized == 1) {
            formContext.getControl("ofm_union_list").setVisible(true);
            formContext.getAttribute("ofm_union_list").setRequiredLevel("required");
        }
        else {
            formContext.getControl("ofm_union_list").setVisible(false);
            formContext.getAttribute("ofm_union_list").setRequiredLevel("none");
            formContext.getAttribute("ofm_union_list").setValue(null);
        }
    },
    //if union list contains Other, then show description field
    showOtherDescription: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var unionsList = formContext.getAttribute("ofm_union_list");
        if (unionsList != null) {
            var selectedOption = unionsList.getSelectedOption();
            if (selectedOption != null) {
                if (selectedOption.filter(i => i.value === 6).length > 0) {
                    formContext.getControl("ofm_union_description").setVisible(true);
                    formContext.getAttribute("ofm_union_description").setRequiredLevel("required");
                }
                else {
                    formContext.getControl("ofm_union_description").setVisible(false);
                    formContext.getAttribute("ofm_union_description").setRequiredLevel("none");
                    formContext.getAttribute("ofm_union_description").setValue(null);
                }
            }
        }
    },

    notForProfitSection: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var organization = formContext.getAttribute("ofm_organization").getValue();
        Xrm.WebApi.retrieveRecord("account", organization[0].id, "?$select=ofm_business_type").then(
            function success(result) {
                console.log(result);
                if (result["ofm_business_type"] != null) {
                    var businessType = result["ofm_business_type"]; // Choice
                    if (businessType == 2)
                        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(true);
                    else
                        formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
                }
                else {
                    formContext.ui.tabs.get("tab_6").sections.get("tab_6_section_7").setVisible(false);
                }
            },
            function (error) {
                console.log(error.message);
            }
        );
    },
    showCCFRIParticipation: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var facility = formContext.getAttribute("ofm_facility").getValue();
        var facilityid;
        if (facility != null) {
            facilityid = facility[0].id;
            Xrm.WebApi.retrieveRecord("account", facilityid, "?$select=ofm_program").then(
                function success(results) {
                    console.log(results);
                    if (results["ofm_program"] === 2 || results["ofm_program"] === 4) {
                        formContext.getControl("ofm_ccfri_participation").setVisible(true);

                    }
                    else {
                        formContext.getControl("ofm_ccfri_participation").setVisible(false);
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    },

    hideEligibilityTab: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var applicationType = formContext.getAttribute("ofm_application_type").getValue();
        if (applicationType == 1) { //NEW
            formContext.ui.tabs.get("tab_11").setVisible(true);
        }
        else {
            formContext.ui.tabs.get("tab_11").setVisible(false);
        }
    }
}