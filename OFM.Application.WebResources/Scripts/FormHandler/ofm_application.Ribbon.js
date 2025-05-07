"use strict";

var OFM = OFM || {};
OFM.Application = OFM.Application || {};
OFM.Application.Ribbon = OFM.Application.Ribbon || {};

OFM.Application.Ribbon = {
    FundingMODRequest: function (recordId, primaryControl) {
        var formContext = primaryControl;

        var window_width = 1300;
        var window_height = 715;

        var pageInput = {
            pageType: "custom",
            name: "ofm_createapplicationmodpagelatestchanges_6d7e8",
            entityName: "ofm_application",
            recordId: recordId,
        };
        var navigationOptions = {
            target: 2,
            width: window_width,
            height: window_height
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    formContext.data.refresh();
                    console.log("Success");
                }
            ).catch(
                function () {
                    console.log(Error);
                }
            );
    },
    openCertStatusValidationWindow: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        var window_width = 400;
        var window_height = 300;
        var pageInput = {
            pageType: "custom",
            name: "ofm_ececertificatevalidationonapplication_e2c4e",
            entityName: "ofm_application",
            recordId: recordId,
        };
        var navigationOptions = {
            target: 2,
            width: window_width,
            height: window_height
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    formContext.data.refresh();
                    console.log("Success");
                }
            ).catch(
                function () {
                    console.log(Error);
                }
            );
    },
    openRecalculateScoreWindow: function (primaryControl) {
        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        var window_width = 400;
        var window_height = 300;
        var pageInput = {
            pageType: "custom",
            name: "ofm_recalculateapplicationscore_17dc4",
            entityName: "ofm_application",
            recordId: recordId,
        };
        var navigationOptions = {
            target: 2,
            width: window_width,
            height: window_height
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    formContext.data.refresh();
                    console.log("Success");
                }
            ).catch(
                function () {
                    console.log(Error);
                }
            );
    }
};