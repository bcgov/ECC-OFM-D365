
"use strict";
var OFM = OFM || {};
OFM.FundingReportsResponse = OFM.FundingReportsResponse || {};
OFM.FundingReportsResponse.Form = OFM.FundingReportsResponse.Form || {};

OFM.FundingReportsResponse.Form = {
    onLoad: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;
            case 1: //Create/QuickCreate

            case 2: // update      
                this.showHideVersions(executionContext);
                break;
            case 3: //readonly
                this.showHideVersions(executionContext);
                break;
            case 4: //disable
                this.showHideVersions(executionContext);
                break;
            case 6: //bulkedit
                break;
        }
    },

    //A function called on save
    onSave: function (executionContext) {
    },

    showHideVersions: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var current_version = formContext.getAttribute("ofm_current_version").getValue();

        //if current version does not contain data -> it is latest version so show the old version
        if (current_version == null) {
            formContext.ui.tabs.get("tab_old_versions").setVisible(true);
            formContext.getControl("ofm_current_version").setVisible(false);
        } else {
            formContext.ui.tabs.get("tab_old_versions").setVisible(false);
            formContext.getControl("ofm_current_version").setVisible(true);
        }

    },

    openUnlockWindow: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");

        var window_width = 800;
        var window_height = 715;

        Xrm.Navigation.navigateTo({
            pageType: "custom",
            name: "ofm_fundingreportresponseunlockpage_dbcf7",
            entityName: "ofm_survey_response",
            recordId: recordId,

        }, {
            target: 2,
            width: window_width,
            height: window_height,
            title: "Unlock Provider Report"
        })
            .then(function () {
                formContext.data.refresh();
            })
            .catch(console.error);

    },
    openCertStatusValidationWindow: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");

        var window_width = 400;
        var window_height = 300;

        Xrm.Navigation.navigateTo({
            pageType: "custom",
            name: "ofm_ececertificatevalidationonreport_297dd",
            entityName: "ofm_survey_response",
            recordId: recordId,

        }, {
            target: 2,
            width: window_width,
            height: window_height,
            title: "Certificate Status Validation"
        })
            .then(function () {
                formContext.data.refresh();
            })
            .catch(console.error);

    }
}