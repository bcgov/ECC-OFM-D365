
"use strict";
var OFM = OFM || {};
OFM.Intake = OFM.Intake || {};
OFM.Intake.Form = OFM.Intake.Form || {};

OFM.Intake.Form = {
    //Hide or show dynamic query section based on Intake type
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
                // alert("Failed while saving!");
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
    // Trigger on click of Associate facilities button on Intake form.
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
    },
    //validate start date and end date on Intake form.
    ValidateStartDateAndEndDate: function (executionContext) {

        var formContext = executionContext.getFormContext();

        var startDate = formContext.getAttribute("ofm_start_date").getValue();
        var endDate = formContext.getAttribute("ofm_end_date").getValue();

        // Check if both start date and end date are provided
        if (startDate != null && endDate != null) {
            // Compare start date and end date
            if (startDate >= endDate) {
                // End date is before start date, show an error message
                formContext.ui.setFormNotification("End date cannot be before start date.", "ERROR", "enddate");
                // Clear end date field
                formContext.getAttribute("ofm_end_date").setValue(null);
            } else {
                // Remove any existing error message
                formContext.ui.clearFormNotification("enddate");
            }
        }
    },
    // Trigger on click of Add Existing Facility button on Intake Facilities subgrid.
    RunOnSelectedIntaketoAddExistingfacility: function (primaryControl) {

        var formContext = primaryControl;
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        // Adding existing facilities to the list.
        var pageInput = {
            pageType: "custom",
            name: "ofm_addexistingfacilityforintakefacilitiesgrid_71be6",
            entityName: "ofm_intake",
            recordId: recordId,
        };

        var navigationOptions = {
            target: 2,
            position: 1,
            width: 800,
            height: 850,
            title: "Adding Existing Facility"
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
    },

    showHideAssociateFailities: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var visable = false;
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        userRoles.forEach(function hasRole(item, index) {
            if (item.name === "OFM - System Administrator") {
                visable = true;
            }
        });

        var intakeType = formContext.getAttribute('ofm_intake_type').getValue();

        return (intakeType == 2) && visable;
    },

    showHideAddExistingFacility: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var visable = false;
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        userRoles.forEach(function hasRole(item, index) {
            if (item.name === "OFM - System Administrator") {
                visable = true;
            }
        });

        return visable;
    },
    validateCohortNumber: function(executionContext){
        debugger;
        var formContext = executionContext.getFormContext();
        var cohort = formContext.getAttribute("ofm_cohort").getValue();

        //validation required must be numeric and 6 digits
        if(!/^\d+$/.test(cohort) || cohort.length != 6){
            formContext.getControl("ofm_cohort").setNotification("Cohort should be 6 digits in 'MMYYYY' format.", "validation_rule");
        }else{
            formContext.getControl("ofm_cohort").clearNotification("validation_rule");
        }
    }
};
