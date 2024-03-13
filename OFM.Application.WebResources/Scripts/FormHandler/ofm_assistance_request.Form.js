"use strict";

var OFM = OFM || {};
OFM.AssistanceRequest = OFM.AssistanceRequest || {};
OFM.AssistanceRequest.Form = OFM.AssistanceRequest.Form || {};

//Formload logic starts here
OFM.AssistanceRequest.Form = {
    onLoad: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;
            case 1: //Create/QuickCreate

            case 2: // update                           
                this.getTypeOfForm();
                this.verifyRequestFacilityGrid(executionContext);
                this.showHideSubType(executionContext);
                this.showHideChecklistSection(executionContext);
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
        this.unableToCloseRequest(executionContext);
    },

    getTypeOfForm: function () {

    },

    verifyRequestFacilityGrid: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var grid = formContext.getControl("Subgrid_new_1");

        var requestId = formContext.data.entity.getId();
        grid.addOnLoad(function () {
            Xrm.WebApi.retrieveMultipleRecords("ofm_facility_request", "?$filter=(_ofm_request_value eq " + requestId.replace("{", "").replace("}", "") + " and statecode eq 0)").then(
                function success(results) {
                    console.log(results);
                    if (results.entities.length <= 0) {
                        formContext.ui.setFormNotification("A minimum of 1 facility is required - enter the facility in the Request Facility", "WARNING", "facilityRequired");
                    }
                    else {
                        formContext.ui.clearFormNotification("facilityRequired");
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );
        });
    },

    showHideSubType: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var requestCategory = formContext.getAttribute("ofm_request_category").getValue();
        if (requestCategory != null) {
            var requestCategoryName = requestCategory[0].name;
            if (requestCategoryName == "Account Maintenance") {
                formContext.getControl("ofm_subcategory").setVisible(true);
            }
            else {
                formContext.getControl("ofm_subcategory").setVisible(false);
                formContext.getAttribute("ofm_subcategory").setValue(null);
                this.showHideChecklistSection(executionContext);
            }
        }
    },

    showHideChecklistSection: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var subTypes = formContext.getAttribute("ofm_subcategory").getValue();
        var updatedArray = this.returnChecklistSectionArray(subTypes);
        for (var element in updatedArray) {
            var section = formContext.ui.tabs.get("tab_overview").sections.get(element);
            section.setVisible(updatedArray[element]);
            if (!updatedArray[element]) {
                var controls = section.controls.get();
                var controlsLength = controls.length;
                for (var i = 0; i < controlsLength; i++) {
                    var controlName = controls[i].getName();
                    controls[i].getAttribute().setValue(1);
                    formContext.getControl(controlName).clearNotification(controlName);
                }
            }
        }
    },

    returnChecklistSectionArray: function (subTypes) {
        //debugger;
        const sectionArray = { section_6: false, section_7: false, section_8: false };
        var subType = JSON.parse(subTypes);
        if (subType != null) {
            for (var i = 0; i < subType.length; i++) {
                if (subType[i]._name == "Organization Details" || subType[i].name == "Organization Details") {
                    sectionArray.section_6 = true;
                }
                else if (subType[i]._name == "Facility Details" || subType[i].name == "Facility Details") {
                    sectionArray.section_7 = true;
                }
                else if (subType[i]._name == "Add/change a licence" || subType[i].name == "Add/change a licence") {
                    sectionArray.section_8 = true;
                }
            }
        }
        return sectionArray;
    },

    unableToCloseRequest: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var statusReason = formContext.getAttribute("statuscode").getValue();
        this.restrictCloseRequest(formContext, statusReason);
    },

    restrictCloseRequest: function (formContext, statusReason) {
        //debugger;
        var flag = true;
        if (statusReason == 4 || statusReason == 0) {
            var subTypes = formContext.getAttribute("ofm_subcategory").getValue();
            var updatedArray = this.returnChecklistSectionArray(subTypes);
            for (var element in updatedArray) {
                if (updatedArray[element]) {
                    var section = formContext.ui.tabs.get("tab_overview").sections.get(element);
                    var controls = section.controls.get();
                    var controlsLength = controls.length;

                    for (var i = 0; i < controlsLength; i++) {
                        var controlName = controls[i].getName();
                        if (controls[i].getAttribute().getValue() === 1 || controls[i].getAttribute().getValue() === 3) {
                            formContext.getControl(controlName).setNotification("Mark this as either Complete or N/A", controlName);
                            if (flag)
                                flag = false;
                        }
                        else {
                            formContext.getControl(controlName).clearNotification(controlName);
                        }
                    }
                }
            }
        }
        return flag;
    },

    clearFieldNotification: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var controlName = executionContext.getEventSource().getName();
        formContext.getControl(controlName).clearNotification(controlName);
    }
}