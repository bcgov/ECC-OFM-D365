"use strict";

var OFM = OFM || {};
OFM.application_score_calculator = OFM.application_score_calculator || {};
OFM.application_score_calculator.Ribbon = OFM.application_score_calculator.Ribbon || {};

OFM.application_score_calculator.Ribbon = {
    
    openCloneWindow: function (primaryControl) {
        var formContext = primaryControl;
        var recordId = "";
        if (primaryControl.getGrid == null) {
            recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        }
        if (primaryControl.getGrid != null) {
            var records = new Array();
            primaryControl.getGrid().getSelectedRows().forEach(function (selectedRow, i) {
                records.push(selectedRow.getData().getEntity().getId().replace(/[{}]/g, ""));
            });
            recordId = records.join(",");
        }


        var window_width = 400;
        var window_height = 300;
        var pageInput = {
            pageType: "custom",
            name: "ofm_clonescorecalculator_df505",
            entityName: "ofm_application_score_calculator",
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
                    if (primaryControl.getGrid == null) {
                        formContext.data.refresh();
                    }
                    if (primaryControl.getGrid != null) {
                        primaryControl.refresh();
                    }
                    console.log("Success");
                }
            ).catch(
                function (error,data) {
                    console.log(error);
                    console.log(data);
                }
            );
    }
};