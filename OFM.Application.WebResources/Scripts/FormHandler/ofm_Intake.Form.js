
"use strict";
var OFM = OFM || {};
OFM.Intake = OFM.Intake || {};
OFM.Intake.Form = OFM.Intake.Form || {};

OFM.Intake.Form = {
    HideShowCategoryandFacilitiesSection: function (executionContext) {
       
        var formContext = executionContext.getFormContext();
        var intakeType = formContext.getAttribute("ofm_intake_type").getValue();
        var intakeQueryType = formContext.getAttribute("ofm_intake_query_type").getValue();

        //Hide category and facilities section
        //get tab
        let General = formContext.ui.tabs.get("General");
        //get section
        let querySection = General.sections.get("query_Section");
        let facilities_Section = General.sections.get("facilities_Section");
        formContext.data.save(1).then(
            function () {
                //  alert("Successfully Saved!");
            },
            function () {
                alert("Failed while saving!");
            });

        // if intake type is close ended
        if (intakeType == 2 && intakeType != null) {
            querySection.setVisible(true);
            if (intakeQueryType == 2) {
                //show the criteria attribute

                formContext.getControl("ofm_criteria").setVisible(true);

                //setting criteria field required.
                formContext.getAttribute("ofm_criteria").setRequiredLevel("required");
            }
            else {
                //Hide the criteria attribute
                formContext.getControl("ofm_criteria").setVisible(false);

                //setting criteria field optional.
                formContext.getAttribute("ofm_criteria").setRequiredLevel("none");

            }
            facilities_Section.setVisible(true);


        }
        else {
            //hide the sections and not required
            querySection.setVisible(false);
            facilities_Section.setVisible(false);
            //setting criteria field required.
            formContext.getAttribute("ofm_criteria").setRequiredLevel("none");

        }
    },
    RunOnSelectedIntake: function (primaryControl) {

        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        //Facilities association to Intake form
        var pageInput = {
            pageType: "custom",
            name: "ofm_facilityassociationforintakepage_cd364",
            entityName: "ofm_intake",
            recordId: recordId,
        };

        var navigationOptions = {
            target: 2,
            position: 1,
            width: 540,
            height: 400,
            title: "Associating Facilities"
        };
        Xrm.Navigation.navigateTo(pageInput, navigationOptions).then(
            function () {
                // Called when the dialog closes
                formContext.data.refresh();

                console.log("Success");
            }
        ).catch(
            function (error) {
                console.log(Error);
            }
        );
    }
};