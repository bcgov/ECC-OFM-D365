"use strict";

var OFM = OFM || {};
OFM.Funding = OFM.Funding || {};
OFM.Funding.Form = OFM.Funding.Form || {};

//Formload logic starts here
OFM.Funding.Form = {
    onLoad: function (executionContext) {
        debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;
            case 1: //Create/QuickCreate

            case 2: // update      
                this.ShowHideBasedOnRoomSplit(executionContext);
                this.lockfieldsPCM(executionContext);
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
        this.ShowHideBasedOnRoomSplit(executionContext);
    },

    ShowHideBasedOnRoomSplit: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var roomSplit = formContext.getAttribute("ofm_apply_room_split_condition").getValue();
        formContext.ui.tabs.get("tab_1").sections.get("manual_spaces_allocation").setVisible(roomSplit);
    },

    RecalculateFundings: function (executionContext) {
        debugger;
        //alert("Recalculation initiated!");
        var formContext = executionContext;
        var recordId = formContext.data.entity.getId().replace("{", "").replace("}", "");
        var pageInput = {
            pageType: "custom",
            name: "ofm_ofmfundingrecalculation_08432",
            entityName: "ofm_application",
            recordId: recordId
        };
        var navigationOptions = {
            target: 2,
            position: 1,
            height: 540,
            width: 400,
            title: "Recalculate the Fundings Amount"
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    formContext.data.refresh();
                    console.log("Success");
                }
            ).catch(
                function (error) {
                    console.log(Error);
                }
            );
    },

    onGridRowSelected: function (executionContext) {
        executionContext.getFormContext().getData().getEntity().attributes.forEach(function (attr) {
            if (attr.getName() != "ofm_adjusted_allocation") {
                attr.controls.forEach(function (myField) {
                    myField.setDisabled(true);
                });
            }
        });
    },
    lockfieldsPCM: function (executionContext) {
        debugger;
        var roleId = "f3ce434d-ba1b-4538-b8b3-b7a8cc4547ec"; //PCM Access Role
        let formContext = executionContext.getFormContext();
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        if (userRoles.getLength() > 1) { }

        else if (userRoles.get()[0].id == roleId) {
            formContext.data.entity.attributes.forEach(function (attribute, index) {
                let control = formContext.getControl(attribute.getName());
                if (control) {
                    control.setDisabled(true);
                }
                executionContext.getFormContext().getControl("ofm_start_date").setDisabled(false);
                executionContext.getFormContext().getControl("ofm_end_date").setDisabled(false);
                executionContext.getFormContext().getControl("statuscode").setDisabled(false);

            });

        }

    }
}