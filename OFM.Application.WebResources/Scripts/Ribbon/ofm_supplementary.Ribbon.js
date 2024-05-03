"use strict";

var OFM = OFM || {};
OFM.Supplementary = OFM.Supplementary || {};
OFM.Supplementary.Ribbon = OFM.Supplementary.Ribbon || {};

OFM.Supplementary.Ribbon = {
    DeactivateRequest: function (recordId, primaryControl) {
        var formContext = primaryControl;

        var window_width = 550;
        var window_height = 575;

        var pageInput = {
            pageType: "custom",
            name: "ofm_denysupplementalapplication_5c2f6",
            entityName: "ofm_allowance",
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