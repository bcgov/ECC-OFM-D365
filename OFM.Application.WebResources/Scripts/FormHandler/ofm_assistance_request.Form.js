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
                break;
            case 3: //readonly
                break;
            case 4: //disable
                break;
            case 6: //bulkedit
                break;
        }
        verifyRequestFacilityGrid(executionContext);
    },

    //A function called on save
    onSave: function (executionContext) {

    },

    getTypeOfForm: function () {

    },

    verifyRequestFacilityGrid: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var grid = formContext.getControl("Subgrid_new_1");

        var requestId = formContext.data.entity.getId();
        grid.addOnLoad(function () {
            Xrm.WebApi.retrieveMultipleRecords("ofm_facility_request", "?$filter=(_ofm_request_value eq " + requestId.replace("{", "").replace("}", "") + " and statecode eq 0)").then(
                function success(results) {
                    console.log(results);
                    if (results.entities.length < 0) {
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
    }
};