"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.licence_detail = OFM.licence_detail || {};
OFM.licence_detail.Form = OFM.licence_detail.Form || {};

//Formload logic starts here
OFM.licence_detail.Form = {
    onLoad: function (executionContext) {
        //debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.identifyLicenceType(formContext);
                break;

            case 2: // update 
                this.checkPreSchoolSelected(executionContext);
                this.identifyLicenceType(formContext);
                break;

            case 3: //readonly
                break;

            case 4: //disable
                break;

            case 6: //bulkedit
                break;
        }
    },

    identifyLicenceType: function (formContext) {
        debugger;
        //formContext.getControl("ofm_licence_type").clearOptions();
        var removeValues;
        var licenceId = formContext.getAttribute("ofm_licence").getValue() != null ? formContext.getAttribute("ofm_licence").getValue()[0].id : null;
        if (licenceId != null) {
            Xrm.WebApi.retrieveRecord("ofm_licence", licenceId, "?$select=_ofm_application_value").then(
                function success(result) {
                    console.log(result);
                    // Columns
                    var applicationId = result["_ofm_application_value"]; // Lookup
                    Xrm.WebApi.retrieveRecord("ofm_application", applicationId, "?$select=ofm_provider_type").then(
                        function success(result) {
                            console.log(result);
                            // Columns
                            var providerType = result["ofm_provider_type"]; // Choice
                            if (providerType == 1) {
                                removeValues = [10, 11];
                            }
                            else if (providerType == 2) { removeValues = [1, 2, 3, 4, 5, 6, 7, 8, 9] }
                            else {
                                formContext.getControl("ofm_licence_type").setNotification("Select provider type on Application", "type");
                            }
                            for (var i = 0; i < removeValues.length; i++) {
                                formContext.getControl("ofm_licence_type").removeOption(removeValues[i]);
                            }
                        },
                        function (error) {
                            console.log(error.message);
                        }
                    );
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    },

    checkPreSchoolSelected: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var hoursOfOperationFrom = formContext.getAttribute("ofm_operation_hours_from").getValue();
        var hoursOfOperationTo = formContext.getAttribute("ofm_operation_hours_to").getValue();
        if (formContext.getAttribute("ofm_licence_type").getValue() == 4 || formContext.getAttribute("ofm_licence_type").getValue() == 5 || formContext.getAttribute("ofm_licence_type").getValue() == 6) {
            var hourDiff = (hoursOfOperationTo - hoursOfOperationFrom) / (1000 * 60 * 60); //convert millisconds to hours
            if (hourDiff > 4) {
                formContext.getControl("ofm_operation_hours_to").setNotification("Hours of operations cannot be greater than 4 hours", "operation");
            }
            else {
                formContext.getControl("ofm_operation_hours_to").clearNotification();
            }
        }
        else {
            formContext.getControl("ofm_operation_hours_to").clearNotification();
        }
    },

    //A function called on save
    onSave: function (executionContext) {

    }
}