"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.PaymentLines = OFM.PaymentLines || {};
OFM.PaymentLines.Form = OFM.PaymentLines.Form || {};

//Formload logic starts here
OFM.PaymentLines.Form = {
    onLoad: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                break;

            case 2: // update  
                this.unlockRevisedInvoiceDate(executionContext);
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

    //System Admin have ability to edit Revised Invoice Date and status in case of error
    unlockRevisedInvoiceDate: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var roles = Xrm.Utility.getGlobalContext().userSettings.roles;

        if (roles === null) return;
        roles.forEach(function (item) {
            if (item.name === "OFM - System Administrator" || item.name === "System Administrator") {
                formContext.getControl("ofm_revised_invoice_date").setDisabled(false);
                formContext.getControl("header_statuscode").setDisabled(false);
            }
            else if (item.name === "OFM - Qualified Receiver") {
                formContext.getControl("header_statuscode").setDisabled(false);
            }
            else if (item.name === "OFM - Leadership") {
                formContext.getControl("ofm_revised_invoice_date").setDisabled(false);
            }
        });
    }
}