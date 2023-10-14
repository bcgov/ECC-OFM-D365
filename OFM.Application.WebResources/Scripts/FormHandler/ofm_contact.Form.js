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

    }
};