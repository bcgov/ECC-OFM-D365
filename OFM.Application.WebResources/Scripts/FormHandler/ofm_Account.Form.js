﻿"use strict";

//Create Namespace Object 
var OFM = OFM || {};
OFM.Account = OFM.Account || {};
OFM.Account.OrgFacility = OFM.Account.OrgFacility || {};
OFM.Account.OrgFacility.Form = OFM.Account.OrgFacility.Form || {};

//Formload logic starts here
OFM.Account.OrgFacility.Form = {
    onLoad: function (executionContext) {
        //debugger;
        let formContext = executionContext.getFormContext();
        switch (formContext.ui.getFormType()) {
            case 0: //undefined
                break;

            case 1: //Create/QuickCreate
                this.setAccountType(executionContext);
                formContext.getAttribute("address1_stateorprovince").setValue("BC");
                this.setRequiredFieldsOrgFacility(executionContext);
                this.setVisibilityMailingAddress(executionContext);
                formContext.getAttribute("address1_stateorprovince").addOnChange(this.setRequiredFieldsOrgFacility);
                break;

            case 2: // update  
                this.getTypeOfForm(executionContext);
                formContext.getControl("ccof_accounttype").setDisabled(true);    // readonly
                this.setRequiredFieldsOrgFacility(executionContext);
                this.setVisibilityMailingAddress(executionContext);
                this.filterPrimaryContactLookup(executionContext);
                formContext.getAttribute("address1_stateorprovince").addOnChange(this.setRequiredFieldsOrgFacility);
                var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	    // get current form's label
                if (formLabel == "Organization Information - OFM") {
                    formContext.data.entity.addOnSave(this.onChangePrimaryContact);
                }
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

    //A function called onLoad to navigate the related main form (based on Account Type)
    getTypeOfForm: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var typeOfInfo = formContext.getAttribute("ccof_accounttype").getValue();  // 100000000 = Organization, 100000001 = Facility
        console.log("typeOfInfo" + typeOfInfo);

        var lblForm;
        if (typeOfInfo == 100000000) {
            lblForm = "Organization Information - OFM";
        }
        else {
            lblForm = "Facility Information - OFM";
        }

        // Current form's label
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();
        console.log("lblForm " + lblForm);
        console.log("CFL " + formLabel);

        //check if the current form is form need to be displayed based on the value
        if (formContext.ui.formSelector.getCurrentItem().getLabel() != lblForm) {
            var items = formContext.ui.formSelector.items.get();
            for (var i in items) {
                var item = items[i];
                var itemId = item.getId();
                var itemLabel = item.getLabel()

                if (itemLabel == lblForm) {               //Check the current form is the same form to be redirected.
                    if (itemLabel != formLabel) {
                        item.navigate();                  //navigate to the form
                    }
                }
            }
        }
    },

    //A function called onLoad to set Account Type and Required fields
    setAccountType: function (executionContext) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	    // get current form's label

        if (formContext.getAttribute("parentaccountid").getValue() != null && formContext.getAttribute("parentaccountid").getValue()[0].id != null) {
            this.navigateToAppropriateForm(formContext, "Facility Information - OFM");
        }
        else {
            this.navigateToAppropriateForm(formContext, "Organization Information - OFM");
        }

        if (formLabel == "Organization Information - OFM") {
            formContext.getAttribute("ccof_accounttype").setValue(100000000);           // AccountType = Organization (100000000) 
            formContext.getControl("ccof_accounttype").setDisabled(true);               // readonly
            formContext.getAttribute("parentaccountid").setValue(null);                 // set NULL when AccounType = Organization	
        }

        if (formLabel == "Facility Information - OFM") {
            formContext.getAttribute("ccof_accounttype").setValue(100000001);           // AccountType = Facility (100000001) 
            formContext.getControl("ccof_accounttype").setDisabled(true);               // readonly
            formContext.getAttribute("parentaccountid").setRequiredLevel("required");   // required field			
        }
    },

    //A function called onChange to validate Postal Code format
    //Valid Canadian postal code: 
    //      (1) in the format A1A 1A1, where A is a letter and 1 is a digit.
    //		(2) either a space separates the third and fourth characters, or no space in between.
    //		(3) does not include the letters D, F, I, O, Q or U.
    //		(4) the first position does not make use of the letters W or Z.
    onChange_PostalCodeValidation: function (executionContext, postalCodeLogicalName) {
        //debugger;
        var formContext = executionContext.getFormContext();
        var postalCode = formContext.getAttribute(postalCodeLogicalName).getValue();

        if (typeof (postalCode) != "undefined" && postalCode != null) {
            postalCode = postalCode.toString().trim().toUpperCase();

            var regexp_ca = new RegExp("^(?!.*[DFIOQU])[A-VXY][0-9][A-Z] ?[0-9][A-Z][0-9]$");
            var regexp_us = new RegExp("^[0-9]{5}(?:-[0-9]{4})?$");

            if (regexp_ca.test(postalCode))                // check for Canadian postal code
            {
                formContext.getControl(postalCodeLogicalName).clearNotification("999");
                //formContext.getAttribute(postalCodeLogicalName).setValue(postalCode.substr(0,3) + " " + postalCode.substr(postalCode.length-3,3));                
                formContext.getAttribute(postalCodeLogicalName).setValue(postalCode.substr(0, 3) + postalCode.substr(postalCode.length - 3, 3));
            }
            else if (regexp_us.test(postalCode))            // check for US ZIP code
            {
                formContext.getControl(postalCodeLogicalName).clearNotification("999");
                formContext.getAttribute(postalCodeLogicalName).setValue(postalCode.replace(" ", "-"));
            }
            else {
                formContext.getControl(postalCodeLogicalName).setNotification("Postal Code Format Validation fails. Please enter correct postal code", "999");
            }
        }
        else {
            formContext.getControl(postalCodeLogicalName).setNotification("Postal Code is empty. Please enter postal code", "999");
        }
    },


    //A function called on change of "Is Mailing Address Different" two option to toggle the visibility of "Mailing Address" section
    setVisibilityMailingAddress: function (executionContext) {
        //debugger;	
        var formContext = executionContext.getFormContext();

        if (formContext.getAttribute("ofm_is_mailing_address_different") != null) {
            var isDifferent_MailingAddress = formContext.getAttribute("ofm_is_mailing_address_different").getValue();

            if (typeof (isDifferent_MailingAddress) != "undefined" && isDifferent_MailingAddress != null) {
                if (isDifferent_MailingAddress) {
                    formContext.ui.tabs.get("tab_Overview").sections.get("section_MailingAddress").setVisible(true);
                }
                else {
                    formContext.ui.tabs.get("tab_Overview").sections.get("section_MailingAddress").setVisible(false);

                    // set the fields to null
                    formContext.getAttribute("address2_line1").setValue(null);
                    formContext.getAttribute("address2_line2").setValue(null);
                    formContext.getAttribute("address2_city").setValue(null);
                    formContext.getAttribute("address2_stateorprovince").setValue(null);
                    formContext.getAttribute("address2_postalcode").setValue(null);

                }
            }
        }
    },


    //A function called onLoad to set the required fields for OrgFacility forms
    setRequiredFieldsOrgFacility: function (executionContext) {
        //debugger;	
        var formContext = executionContext.getFormContext();

        var Province1 = formContext.getAttribute("address1_stateorprovince").getValue();  // check if it is BC only
        if (typeof (Province1) != "undefined" && Province1 != null) {
            Province1 = Province1.toString().trim().toUpperCase();

            if (Province1 == "BC") {
                formContext.getControl("address1_stateorprovince").clearNotification("999");
                formContext.getAttribute("address1_stateorprovince").setValue(Province1);
            }
            else {
                formContext.getControl("address1_stateorprovince").setNotification("Only BC facilities are eligible for OFM", "999");
            }
        }

        formContext.getAttribute("address1_line1").setRequiredLevel("required");          // set the fields to be required	
        //formContext.getAttribute("address1_line2").setRequiredLevel("required");
        formContext.getAttribute("address1_city").setRequiredLevel("required");
        formContext.getAttribute("address1_stateorprovince").setRequiredLevel("required");
        formContext.getAttribute("address1_postalcode").setRequiredLevel("required");

        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	      // get current form's label
        if (formLabel == "Organization Information - OFM") {
            if (formContext.ui.getFormType() != 1)                                        // 1 = Create/QuickCreate
            {
                formContext.getAttribute("primarycontactid").setRequiredLevel("required");
            }
        }
    },


    //A function called to filter active contacts associated to organization (lookup on Organization form)
    filterPrimaryContactLookup: function (executionContext) {
        //debugger;	
        var formContext = executionContext.getFormContext();
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();	    // get current form's label

        if (formLabel == "Organization Information - OFM") {
            var currentRecordId = formContext.data.entity.getId().replace("{", "").replace("}", "");

            var fetchXmlFilter = [
                "	<filter type='and' >",
                "	  <condition attribute='statecode' operator='eq' value='0' />",
                "	  <condition attribute='parentcustomerid' operator='eq' value='{" + currentRecordId + "}' uitype='account' />",
                "	</filter>"
            ].join("");

            formContext.getControl("primarycontactid").addPreSearch(function () {
                formContext.getControl("primarycontactid").addCustomFilter(fetchXmlFilter, "contact");
            });
        }
    },


    //A function called onSave to update 'IsPrimaryContact' fields in the subgrid_Contacts 
    onChangePrimaryContact: function (executionContext) {
        debugger;
        var formContext = executionContext.getFormContext();
        var primayContactField = formContext.getAttribute("primarycontactid").getValue();

        if (primayContactField != null) {
            var currentRecordId = formContext.data.entity.getId();
            currentRecordId = currentRecordId.replace("{", "").replace("}", "");

            var primaryContactId = primayContactField[0].id.replace("{", "").replace("}", "");

            // update 'isPrimaryContact' field for primary contact record
            var data = { "ofm_is_primary_contact": true };
            Xrm.WebApi.retrieveRecord("contact", primaryContactId, "?$select=ofm_is_primary_contact").then(
                function success(result) {
                    console.log(result);
                    var ofm_is_primary_contact = result["ofm_is_primary_contact"]; // Boolean
                    if (!ofm_is_primary_contact) {
                        Xrm.WebApi.updateRecord("contact", primaryContactId, data).then(
                            function success(result) {
                                console.log("Conact updated");
                            },
                            function (error) {
                                console.log(error.message);
                            }
                        );
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );

            //query subgrid contact records, and update 'isPrimaryContact' field for non-primary-contact records (true => false)          
            var contactFetchXml = "?$filter=(statecode eq 0 and ofm_is_primary_contact eq true and contactid ne " + primaryContactId + " and _parentcustomerid_value eq " + currentRecordId + ")";

            var payload = {};
            payload.ofm_is_primary_contact = false;
            Xrm.WebApi.retrieveMultipleRecords("contact", contactFetchXml).then(
                function success(results) {
                    console.log(results);
                    for (var i = 0; i < results.entities.length; i++) {
                        var result = results.entities[i];
                        var contactid = result["contactid"]; // Guid
                        console.log(contactid);
                        Xrm.WebApi.updateRecord("contact", contactid, payload).then(
                            function success(result) {
                                var updatedId = result.id;
                                console.log(updatedId);
                            },
                            function (error) {
                                console.log(error.message);
                            }
                        );
                    }
                },
                function (error) {
                    console.log(error.message);
                }
            );

            formContext.getControl("Subgrid_Contacts").refresh();   // refresh contact subgrid 
        }
    },

    setSubmitModeForAttributes: function (formContext) {
        var attributes = formContext.data.entity.attributes.get();
        for (var i in attributes) {
            attributes[i].setSubmitMode("never");
        }
    },

    navigateToAppropriateForm: function (formContext, formLabel) {
        if (formContext.ui.formSelector.getCurrentItem().getLabel() != formLabel) {
            var items = formContext.ui.formSelector.items.get();
            for (var i in items) {
                var item = items[i];
                var itemLabel = item.getLabel()

                if (itemLabel == formLabel) {               //Check the current form is the same form to be redirected.
                    if (itemLabel != formContext.ui.formSelector.getCurrentItem().getLabel()) {
                        this.setSubmitModeForAttributes(formContext);
                        item.navigate();                  //navigate to the form
                    }
                }
            }
        }
    }

}