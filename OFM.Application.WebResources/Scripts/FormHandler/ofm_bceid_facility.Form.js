"use strict";

var OFM = OFM || {};
OFM.BCeIDFacility = OFM.BCeIDFacility || {};
OFM.BCeIDFacility.Form = OFM.BCeIDFacility.Form || {};

//Formload logic starts here
OFM.BCeIDFacility.Form = {
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
    },

    //A function called on save
    onSave: function (executionContext) {

    },

    getTypeOfForm: function () {

    },

    //Filter Facility lookup to show only active facilities associated to organizations (on Business BCeID form)
    filterFacilityLookup: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var contactID = formContext.getAttribute("ofm_bceid").getValue() != null ? formContext.getAttribute("ofm_bceid").getValue()[0].id : null;
        if (contactID != null) {
            Xrm.WebApi.retrieveMultipleRecords("contact", "?$select=_parentcustomerid_value&$filter=contactid eq " + contactID).then(
                function success(data) {
                    debugger;
                    if (data.entities.length == 1) {
                        var lookupIDValue = data.entities[0]["_parentcustomerid_value"];
                        var fetchXmlFilter = "<filter type='and'><condition attribute = 'parentaccountid' operator = 'eq' value= '{" + lookupIDValue + "}' /><condition attribute = 'statecode' operator = 'eq' value = '0' /></filter>";
                        OFM.Common.FilterLookup(formContext, "ofm_facility", fetchXmlFilter);
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    }
};