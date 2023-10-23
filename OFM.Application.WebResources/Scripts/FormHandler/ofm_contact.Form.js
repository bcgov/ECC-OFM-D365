"use strict";

var OFM = OFM || {};
OFM.Contact = OFM.Contact || {};
OFM.Contact.Form = OFM.Contact.Form || {};

//Formload logic starts here
OFM.Contact.Form = {
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

    //Filter Organization lookup to show only active organizations (Account type = Organization)
    filterOrganizationLookup: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        formContext.getControl("parentcustomerid").setEntityTypes(["account"]);
        var fetchXmlFilter = "<filter type='and'><condition attribute='ccof_accounttype' operator='eq' value='100000000' /></filter>";
        OFM.Common.FilterLookup(formContext, "parentcustomerid", fetchXmlFilter);
    }
};