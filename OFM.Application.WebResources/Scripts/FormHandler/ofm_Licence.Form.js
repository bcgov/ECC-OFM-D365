"use strict";

//Create Namespace Object 
var organizationid;
var pageContext;
var OFM = OFM || {};
OFM.Licence = OFM.Licence || {};
OFM.Licence.Form = OFM.Licence.Form || {};
var isValidationNeeded = true;

OFM.Licence.Form = {
    onLoad: function (executionContext) {
        //debugger;
        let formContext = executionContext.getFormContext();
        pageContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.licenceDetailCheck(executionContext);
                this.showBanner(executionContext);
                break;

            case 2: // update 
                this.licenceDetailCheck(executionContext);
                this.showBanner(executionContext);
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
        this.stopSaveWhenLicenceDetailFailed(executionContext);
        this.showBanner(executionContext);
    },

    //function to check if the licence has at least 1 licence detail record
    licenceDetailCheck: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var licenceId = formContext.data.entity.getId();
        licenceId = licenceId.replace("{", "").replace("}", "");

        if (licenceId == null || licenceId === "") {
            isValidationNeeded = false;
            return;
        }

        Xrm.WebApi.retrieveMultipleRecords("ofm_licence_detail", "?$filter=(statecode eq 0 and _ofm_licence_value eq " + licenceId + ")").then(
            function success(results) {
                if (results.entities.length <= 0) {
                    formContext.ui.setFormNotification("Licence needs to have at least 1 licence detail.", "ERROR", "licence detail");
                }
                else {
                    formContext.ui.clearFormNotification("licence detail");
                }
            },
            function (error) {
                console.log(error.message);
            }
        );

    },

    stopSaveWhenLicenceDetailFailed: function (executionContext) {
        if (!isValidationNeeded) {
            isValidationNeeded = true;
            return;
        }

        debugger;

        var formContext = executionContext.getFormContext();
        var licenceId = formContext.data.entity.getId();
        licenceId = licenceId.replace("{", "").replace("}", "");

        if (licenceId == null || licenceId === "") {
            isValidationNeeded = false;
            return;
        }

        //getting save mode from event
        var saveMode = executionContext.getEventArgs().getSaveMode();

        var SaveMode = {
            Save: 1,
            SaveAndClose: 2,
            SaveAndNew: 59,
            Autosave: 70
        };

        //if savemode is not one of listed - just quit the execution and let the record to be saved
        if (saveMode !== SaveMode.Save &&
            saveMode !== SaveMode.SaveAndClose &&
            saveMode !== SaveMode.SaveAndNew &&
            saveMode !== SaveMode.Autosave) {
            return;
        }
        //stop saving
        executionContext.getEventArgs().preventDefault();

        //getting save mode from event
        Xrm.WebApi.retrieveMultipleRecords("ofm_licence_detail", "?$filter=(statecode eq 0 and _ofm_licence_value eq " + licenceId + ")").then(
            function success(results) {
                if (results.entities.length <= 0) {
                    formContext.ui.setFormNotification("Licence needs to have at least 1 licence detail.", "ERROR", "licence detail");
                    return;
                }
                else {
                    isValidationNeeded = false;
                    formContext.ui.clearFormNotification("licence detail");
                    if (saveMode === SaveMode.Save ||
                        saveMode === SaveMode.Autosave) {
                        formContext.data.entity.save(saveMode).then(
                            function success() {
                                formContext.data.refresh();
                            },
                            function (error) {
                                console.log(error.message);
                            }
                        );
                    } else if (saveMode === SaveMode.SaveAndClose) {
                        formContext.data.entity.save("saveandclose");
                    } else {
                        formContext.data.entity.save("saveandnew");
                    }
                }
            },
            function (error) {
                console.log(error.message);
            }
        );

    },

    showBanner: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
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
                    formContext.ui.tabs.get("tab_1").sections.get("tab_1_section_4").setVisible(review_flag);
                    formContext.getControl("ofm_review_underway_banner").setVisible(review_flag);
                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
        else {
            formContext.ui.tabs.get("tab_1").sections.get("tab_1_section_4").setVisible(review_flag);
            formContext.getControl("ofm_review_underway_banner").setVisible(review_flag);
        }
    }
}