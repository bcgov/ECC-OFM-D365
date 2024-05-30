"use strict";

var OFM = OFM || {};
OFM.AssistanceRequest = OFM.AssistanceRequest || {};
OFM.AssistanceRequest.Ribbon = OFM.AssistanceRequest.Ribbon || {};

OFM.AssistanceRequest.Ribbon = {
    DeactivateRequest: function (recordId, primaryControl) {
        var formContext = primaryControl;
        var flag = OFM.AssistanceRequest.Form.restrictCloseRequest(formContext, 0);
        if (flag) {
            var window_width = 1300;
            var window_height = 715;

            var pageInput = {
                pageType: "custom",
                name: "ofm_ofmdeactivaterequestpage_db19a",
                entityName: "ofm_assistance_request",
                recordId: recordId,
            };
            var navigationOptions = {
                target: 2,
                width: window_width,
                height: window_height
            };
            Xrm.Navigation.navigateTo(pageInput, navigationOptions)
                .then(function () {
                    formContext.data.refresh();
                })
        }
    },

    showHideDeactivateRequest: function (primaryControl) {
        debugger;

        var isAdmin = false;
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        userRoles.forEach(function hasRole(item, index) {
            if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - PCM" || item.name === "OFM - Program Support" || item.name === "OFM - CRC") {
                isAdmin = true;
            }
        });

        return isAdmin;
    }
};