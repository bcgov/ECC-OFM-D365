"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.Supplementary = OFM.Supplementary || {};
OFM.Supplementary.Form = OFM.Supplementary.Form || {};

//Formload logic starts here
OFM.Supplementary.Form = {
    onLoad: function (executionContext) {
        // debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.setAllowanceDetailVisibility(executionContext);
                this.validateStartDateEndDate(executionContext);
                formContext.getAttribute("ofm_start_date").addOnChange(this.validateStartDateEndDate);
                formContext.getAttribute("ofm_end_date").addOnChange(this.validateStartDateEndDate);
                break;

            case 2: // update  
                this.setAllowanceDetailVisibility(executionContext);
                this.lockStatusReason(executionContext);
                this.validateStartDateEndDate(executionContext);
                formContext.getAttribute("ofm_start_date").addOnChange(this.validateStartDateEndDate);
                formContext.getAttribute("ofm_end_date").addOnChange(this.validateStartDateEndDate);
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

    //A function called on change of "allowance type" option to toggle the visibility of Indigenous Programming, Support Needs Programming and Transportation 
    setAllowanceDetailVisibility: function (executionContext) {
        //debugger;	
        var formContext = executionContext.getFormContext();

        if (formContext.getAttribute("ofm_allowance_type") != null) {
            var allowanceType = formContext.getAttribute("ofm_allowance_type").getValue();

            if (typeof (allowanceType) != "undefined" && allowanceType != null) {
                switch (allowanceType) {
                    /*
                    Support Needs Programming = 1
                    Indigenous Programming = 2
                    Transportation = 3
                    */
                    case 1:
                        formContext.ui.tabs.get("tab_general").sections.get("section_support_needs").setVisible(true);
                        formContext.ui.tabs.get("tab_general").sections.get("section_indigenous").setVisible(false);
                        formContext.ui.tabs.get("tab_general").sections.get("section_transportation").setVisible(false);

                        //set attributes to required
                        formContext.getAttribute("ofm_needs_expenses").setRequiredLevel("required");
                        this.showSNDiscription(executionContext);
                        formContext.getAttribute("ofm_needs_expenses").addOnChange(this.showSNDiscription);

                        //set other attribute to none
                        formContext.getAttribute("ofm_indigenous_expenses").setRequiredLevel("none");
                        formContext.getAttribute("ofm_indigenous_description").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_estimated_yearly_km").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_odometer").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_vehicle_vin").setRequiredLevel("none");
                        break;
                    case 2:
                        formContext.ui.tabs.get("tab_general").sections.get("section_support_needs").setVisible(false);
                        formContext.ui.tabs.get("tab_general").sections.get("section_indigenous").setVisible(true);
                        formContext.ui.tabs.get("tab_general").sections.get("section_transportation").setVisible(false);

                        //set attributes to required
                        formContext.getAttribute("ofm_indigenous_expenses").setRequiredLevel("required");
                        this.showIPDiscription(executionContext);
                        formContext.getAttribute("ofm_indigenous_expenses").addOnChange(this.showIPDiscription);

                        //set other attribute to none
                        formContext.getAttribute("ofm_needs_expenses").setRequiredLevel("none");
                        formContext.getAttribute("ofm_needs_description").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_estimated_yearly_km").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_odometer").setRequiredLevel("none");
                        formContext.getAttribute("ofm_transport_vehicle_vin").setRequiredLevel("none");
                        break;
                    case 3:
                        formContext.ui.tabs.get("tab_general").sections.get("section_support_needs").setVisible(false);
                        formContext.ui.tabs.get("tab_general").sections.get("section_indigenous").setVisible(false);
                        formContext.ui.tabs.get("tab_general").sections.get("section_transportation").setVisible(true);

                        //set attributes to required
                        formContext.getAttribute("ofm_transport_estimated_yearly_km").setRequiredLevel("required");
                        formContext.getAttribute("ofm_transport_vehicle_vin").setRequiredLevel("required");
                        formContext.getAttribute("ofm_transport_odometer").setRequiredLevel("required");

                        //set other attribute to none
                        formContext.getAttribute("ofm_indigenous_expenses").setRequiredLevel("none");
                        formContext.getAttribute("ofm_indigenous_description").setRequiredLevel("none");
                        formContext.getAttribute("ofm_needs_expenses").setRequiredLevel("none");
                        formContext.getAttribute("ofm_needs_description").setRequiredLevel("none");
                        break;
                    default:
                }
            }
        }
    },

    //A function called on change to validate start date and end date
    /*
        The start date cannot be before the FA start date
        The end date cannot be greater than 1 year from the start date
        The end date cannot be after the FA end date
    */
    validateStartDateEndDate: function (executionContext) {
        // debugger;
        var formContext = executionContext.getFormContext();
        var startDateStr = formContext.getAttribute("ofm_start_date").getValue();
        var endDateStr = formContext.getAttribute("ofm_end_date").getValue();

        if (startDateStr != null && endDateStr != null) {
            var startDate = new Date(startDateStr);
            var endDate = new Date(endDateStr);
            var expectedEndDate = new Date(startDateStr);
            expectedEndDate.setFullYear(expectedEndDate.getFullYear() + 1);

            //The end date cannot be greater than the start date 
            if (startDate.getTime() > endDate.getTime()) {
                //show error message
                formContext.getControl("ofm_end_date").setNotification("The end date cannot be earlier than the start date", "rule_2.1");
                return;
                //2. The end date cannot be greater than 1 year from the start date    
            } else if (endDate.getTime() > expectedEndDate.getTime()) {
                //show error message
                formContext.getControl("ofm_end_date").setNotification("The end date cannot be greater than 1 year from the start date", "rule_2");
                return;
            } else {
                formContext.getControl("ofm_end_date").clearNotification("rule_2.1");
                formContext.getControl("ofm_end_date").clearNotification("rule_2");
            }

            var currentRecordId = formContext.data.entity.getId();
            currentRecordId = currentRecordId.replace("{", "").replace("}", "");

            //Supplemental -> Application -> Funding

            var fundingFetchXml = `?fetchXml=<fetch>
            <entity name="ofm_funding">
              <attribute name="ofm_end_date" />
              <attribute name="ofm_start_date" />
              <filter>
                <condition attribute="statecode" operator="eq" value="0" />
              </filter>
              <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner">
                <link-entity name="ofm_allowance" from="ofm_application" to="ofm_applicationid">
                  <filter>
                    <condition attribute="ofm_allowanceid" operator="eq" value="${currentRecordId}"  uitype="ofm_allowance" />
                  </filter>
                </link-entity>
              </link-entity>
            </entity>
          </fetch>`

            //Get Funding record start date and end date
            Xrm.WebApi.retrieveMultipleRecords("ofm_funding", fundingFetchXml).then(
                function success(result) {
                    console.log(result);
                    var fundingStartDateStr = result.entities[0]["ofm_start_date"];
                    var fundingEndDateStr = result.entities[0]["ofm_end_date"];

                    var fundingStartDate = new Date(fundingStartDateStr);
                    var fundingEndDate = new Date(fundingEndDateStr);

                    //1. The start date cannot be before the FA start date
                    if (startDate.getTime() < fundingStartDate.getTime()) {
                        formContext.getControl("ofm_start_date").setNotification("The start date cannot be before the FA start date", "rule_1");
                    } else {
                        formContext.getControl("ofm_start_date").clearNotification("rule_1");
                    }

                    //3. The end date cannot be after the FA end date
                    if (endDate.getTime() > fundingEndDate.getTime()) {
                        formContext.getControl("ofm_end_date").setNotification("The end date cannot be after the FA end date", "rule_3");
                    } else {
                        formContext.getControl("ofm_end_date").clearNotification("rule_3");
                    }

                },
                function (error) {
                    console.log(error.message);
                }
            );
        }
    },

    showSNDiscription: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var SNSelectedOptions = formContext.getAttribute("ofm_needs_expenses").getValue();

        //Make the description required if the selected options include "Other" (4)
        if (typeof (SNSelectedOptions) != "undefined" && SNSelectedOptions != null && SNSelectedOptions.includes(4)) {
            formContext.getControl("ofm_needs_description").setVisible(true);
            formContext.getAttribute("ofm_needs_description").setRequiredLevel("required");
        } else {
            formContext.getControl("ofm_needs_description").setVisible(false);
            formContext.getAttribute("ofm_needs_description").setRequiredLevel("none");
        }
    },

    showIPDiscription: function (executionContext) {
        var formContext = executionContext.getFormContext();
        var IPSelectedOptions = formContext.getAttribute("ofm_indigenous_expenses").getValue();

        //Make the description required if the selected options include "Other" (9)
        if (typeof (IPSelectedOptions) != "undefined" && IPSelectedOptions != null && IPSelectedOptions.includes(9)) {
            formContext.getControl("ofm_indigenous_description").setVisible(true);
            formContext.getAttribute("ofm_indigenous_description").setRequiredLevel("required");
        } else {
            formContext.getControl("ofm_indigenous_description").setVisible(false);
            formContext.getAttribute("ofm_indigenous_description").setRequiredLevel("none");
        }
    },

    lockStatusReason: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        if (formContext.getAttribute("statuscode").getValue() != 1) {
            var roles = Xrm.Utility.getGlobalContext().userSettings.roles.getAll();
            var disable = true;
            for (var i = 0; i < roles.length; i++) {
                var name = roles[i].name;
                if (name == "System Administrator" || name == "OFM - System Administrator") {
                    disable = false;
                    break;
                }
            }
            formContext.getControl("header_statuscode").setDisabled(disable);
        }
        else
            formContext.getControl("header_statuscode").setDisabled(false);
    }

}