"use strict";

var OFM = OFM || {};
OFM.Expense = OFM.Expense || {};
OFM.Expense.Form = OFM.Expense.Form || {};

//Formload logic starts here
OFM.Expense.Form = {
	onLoad: function (executionContext) {
		debugger;
		let formContext = executionContext.getFormContext();
		switch (formContext.ui.getFormType()) {
			case 0: //undefined
				break;
			case 1: //Create/QuickCreate

			case 2: // update      
				
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

	approveApplication: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var confirmStrings = {
			title: "Confirm Expense Application Approval",
			text: "Are you sure you want to approve this expense application? Please click Yes button to continue, or click No button to cancel.",
			confirmButtonLabel: "Yes",
			cancelButtonLabel: "No"
		};

		var confirmOptions = { height: 200, width: 550 };
		Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
			function (success) {
				if (success.confirmed) {
					formContext.getAttribute("statuscode").setValue(6);                                    // 6 = Approved
					formContext.data.entity.save();
				}
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ message: error });
			});
	},
	// show approve button for Leadership role.
	showHideApproveExpense: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var statusReason = formContext.getAttribute("statuscode").getValue();

		var visible = false;
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - Leadership") {
				visible = true;
			}
		});

		var showButton = false;
		// Recommended for approval, 
		if (statusReason == 4) {
			showButton = true;
		}

		return showButton && visible;
	},

	RemoveOptionFromStatusCode: function (executionContext) {
		var formContext = executionContext.getFormContext();
		var statusCodeOptions = formContext.getAttribute("statuscode").getOptions();
		var statuscodeControl = formContext.getControl("header_statuscode");
		var draft = { value: 1, text: "Draft" };
		var inReview = { value: 3, text: "In Review" };
		var notRecommended = { value: 5, text: "Not Recommended" };
		var recommendedForApproval = { value: 4, text: "Recommended for Approval" };

		// Determine if option 4 should be removed based on verification criteria
		var ofm_verification_funding_exhausted = formContext.getAttribute("ofm_verification_funding_exhausted").getValue() === 2;
		var ofm_verification_supported_by_policy = formContext.getAttribute("ofm_verification_supported_by_policy").getValue() === 2;
		var ofm_verification_quotations_arms_length = formContext.getAttribute("ofm_verification_quotations_arms_length").getValue() === 2;
		var ofm_verification_quotations_confirmed = formContext.getAttribute("ofm_verification_quotations_confirmed").getValue() === 2;
		var ofm_verification_documents_complete = formContext.getAttribute("ofm_verification_documents_complete").getValue() === 2;
		// Clear current items from status code options.
		for (var i = 0; i < statusCodeOptions.length; i++) {
			statuscodeControl.removeOption(statusCodeOptions[i].value);
		}
			if (ofm_verification_funding_exhausted && ofm_verification_supported_by_policy && ofm_verification_quotations_arms_length && ofm_verification_quotations_confirmed && ofm_verification_documents_complete) {
				// Add option  recommended approval 
				statuscodeControl.addOption(draft);
				statuscodeControl.addOption(notRecommended);
				statuscodeControl.addOption(inReview);
				statuscodeControl.addOption(recommendedForApproval);
			} else {
				// Remove option 4 if any verification fails
				statuscodeControl.addOption(draft);
				statuscodeControl.addOption(notRecommended);
				statuscodeControl.addOption(inReview);
				
			}
		}


	}
	//},

