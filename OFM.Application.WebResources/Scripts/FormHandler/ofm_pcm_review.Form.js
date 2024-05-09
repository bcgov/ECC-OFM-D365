"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.PCMReview = OFM.PCMReview || {};
OFM.PCMReview.Form = OFM.PCMReview.Form || {};

//Formload logic starts here
OFM.PCMReview.Form = {
    onLoad: function (executionContext) {
         debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                break;

            case 2: // update  
                this.lockAllFieldsAppApproval(executionContext);
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
    lockAllFieldsAppApproval: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        let appid = formContext.getAttribute("ofm_application").getValue()[0].id;
        Xrm.WebApi.retrieveRecord("ofm_application", getCleanedGuid(appid)).then(
            function success(result) {
                console.log("Retrieved values: Name: " + result.ofm_application+ ", statuscode: " + result.statuscode);
                if (result.statuscode === 6) {
                    var controls = formContext.ui.controls.get();
                    // formContext.getAttribute("ofm_reason").setValue(1);
                    controls.forEach(function (control) {
                        control.setDisabled(true);
                }
                    );
                }
            },
            function (error) { 
                console.log(error.message);
                // handle error conditions
            }
        );

    }
}
function getCleanedGuid(id) {
    return id.replace("{", "").replace("}", "");
}