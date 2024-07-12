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
				this.lockApprovedStatus(executionContext);
				this.enableAgreementPDF(executionContext);
				this.lockfieldsPCM(executionContext);
				formContext.getAttribute("ofm_ops_manager_approval").addOnChange(this.setOpsManagerApproval);
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

	RecalculateFundings: function (primaryControl) {
		//debugger;
		var formContext = primaryControl;
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

	CreateMOD: function (primaryControl, recordId) {
		debugger;
		var formContext = primaryControl;
		var applicationID = formContext.getAttribute("ofm_facility").getValue();
		var pageInput = {
			pageType: "custom",
			name: "ofm_createafundingmodpage_834ab",
			recordId: recordId.replace("{", "").replace("}", "")
		};
		var navigationOptions = {
			target: 2,
			position: 1,
			height: 1200,
			width: 1150,
			title: "Funding Modification"
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

	onGridRowSelected: function (context) {
		context.getFormContext().getData().getEntity().attributes.forEach(function (attr) {
			if (attr.getName() != "ofm_adjusted_allocation") {
				attr.controls.forEach(function (myField) {
					myField.setDisabled(true);
				});
			}
		});
	},

	regenerateFAPDF_Superseded: function (primaryControl) {
		debugger;
		var globalContext = Xrm.Utility.getGlobalContext();
		var envUrl = globalContext.getClientUrl();

		var formContext = primaryControl;
		var recordId = formContext.data.entity.getId().replace("{", "").replace("}", "");
		Xrm.Navigation.navigateTo({
			pageType: "custom",
			name: "ofm_generatefundingagreementpdf_17349",
			entityName: "ofm_funding",
			recordId: recordId,
			envUrl: envUrl

		}, {
			target: 2,
			position: 1,
			width: 420,
			height: 300,
			title: "Generate Funding Agreement Letter"
		})
			.then(function () {
				// formContext.data.refresh();
			})
			.catch(console.error);
	},

	regenerateFAPDF: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var entityName = formContext.data.entity.getEntityName();
		var recordId = formContext.data.entity.getId().replace("{", "").replace("}", "");

		var flowUrl;
		var result = this.getSyncMultipleRecord("environmentvariabledefinitions?$select=defaultvalue&$expand=environmentvariabledefinition_environmentvariablevalue($select=value)&$filter=(schemaname eq 'ofm_FundingGenerateFAPDFUrl') and (environmentvariabledefinition_environmentvariablevalue/any(o1:(o1/environmentvariablevalueid ne null)))&$top=50");
		flowUrl = result[0]["environmentvariabledefinition_environmentvariablevalue"][0].value;

		var confirmStrings = {
			title: "Confirm Manual Generation of Funding Agreement PDF",
			text: "Are you sure you want to manually generate Funding Agreement PDF? Please click Yes button to continue, or click No button to cancel.",
			confirmButtonLabel: "Yes",
			cancelButtonLabel: "No"
		};
		var confirmOptions = { height: 80, width: 500 };
		Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
			function (success) {
				if (success.confirmed) {
					let body = {
						"Entity": entityName,
						"RecordId": recordId
					};
					let req = new XMLHttpRequest();
					req.open("POST", flowUrl, true);
					req.setRequestHeader("Content-Type", "application/json");
					req.onreadystatechange = function () {
						if (this.readyState === 4) {
							req.onreadystatechange = null;
							if (this.status === 200) {
								let resultJson = JSON.parse(this.response);
							} else {
								console.log(this.statusText);
							}
						}
					};
					req.send(JSON.stringify(body));
				}
				else {
					console.log("Not OK");
				}
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ message: error });
			});
	},

	getSyncMultipleRecord: function (request) {
		var result = null;
		var req = new XMLHttpRequest();
		req.open("GET", Xrm.Page.context.getClientUrl() + "/api/data/v9.1/" + request, false);
		req.setRequestHeader("OData-MaxVersion", "4.0");
		req.setRequestHeader("OData-Version", "4.0");
		req.setRequestHeader("Accept", "application/json");
		req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
		req.setRequestHeader("Prefer", "odata.include-annotations=\"*\"");
		req.onreadystatechange = function () {
			if (this.readyState === 4) {
				req.onreadystatechange = null;
				if (this.status === 200) {
					var results = JSON.parse(this.response);
					result = results.value;
				} else {
					Xrm.Utility.alertDialog(this.statusText);
				}
			}
		};
		req.send();
		return result;
	},

	setOpsManagerApproval: function (executionContext) {
		debugger;
		var formContext = executionContext.getFormContext();
		var newStatus = formContext.getAttribute("ofm_ops_manager_approval").getValue();

		var userSettings = Xrm.Utility.getGlobalContext().userSettings;
		var currentUser = new Array();
		currentUser[0] = new Object();
		currentUser[0].entityType = "systemuser";
		currentUser[0].id = userSettings.userId;
		currentUser[0].name = userSettings.userName;

		var currentDateTime = new Date();

		if (newStatus == 2) {   // approved
			var confirmStrings = {
				title: "Confirm Status Change of Ops Supervisor Approval",
				//subtitle: "A ministry user will need to confirm that they have approved the record.",
				text: "Are you sure you want to approve this funding record? Please click Yes button to continue, or click No button to cancel.",
				confirmButtonLabel: "Yes",
				cancelButtonLabel: "No"
			};

			var confirmOptions = { height: 200, width: 550 };
			Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
				function (success) {
					if (success.confirmed) {
						formContext.getAttribute("ofm_ops_manager_approval").setValue(2);                       // approved
						formContext.getAttribute("statuscode").setValue(5);                                     // FA Signature Pending						
						formContext.getAttribute("ofm_ops_approver").setValue(currentUser);                     // set lookup to current user
						formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(currentDateTime); // set now() to approval date	
						formContext.getControl("ofm_ops_manager_approval").setDisabled(true);                   // lock the approval field
						formContext.data.entity.save();
					}
					else {
						formContext.getAttribute("ofm_ops_manager_approval").setValue(1);                       // pending
						formContext.getAttribute("ofm_ops_approver").setValue(null); 				            // clear lookup	
						formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(null); 			// clear approval date							
						formContext.data.entity.save();
					}
				},
				function (error) {
					Xrm.Navigation.openErrorDialog({ message: error });
				});
		}

		if (newStatus == 3) {   // not approved
			var displayText1 = "You are denying this funding record. Please click Yes button to continue, or click No button to cancel.";
			var displayText2 = "For the denied funding record, its status should be manually set to Cancelled and the application should be manually set to Ineligible.";
			var confirmStrings = {
				title: "Confirm Status Change of Ops Supervisor Approval",
				text: displayText1 + "\n\n" + "Note:" + "\n" + displayText2,
				confirmButtonLabel: "Yes",
				cancelButtonLabel: "No"
			};

			var confirmOptions = { height: 290, width: 550 };
			Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
				function (success) {
					if (success.confirmed) {
						formContext.getAttribute("ofm_ops_manager_approval").setValue(3);                  // not approved
						formContext.getAttribute("ofm_ops_approver").setValue(null); 				       // clear lookup	
						formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(null); 	   // clear approval date
						formContext.data.entity.save();
					}
					else {
						//formContext.data.refresh();	
						formContext.getAttribute("ofm_ops_manager_approval").setValue(1);                  // pending
						formContext.getAttribute("ofm_ops_approver").setValue(null); 				       // clear lookup	
						formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(null); 	   // clear approval date							
						formContext.data.entity.save();
					}
				},
				function (error) {
					Xrm.Navigation.openErrorDialog({ message: error });
				});
		}

		if (newStatus == 1) {   // pending
			formContext.getAttribute("ofm_ops_manager_approval").setValue(1);                  // pending
			formContext.getAttribute("ofm_ops_approver").setValue(null); 				       // clear lookup	
			formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(null); 	   // clear approval date	
			formContext.data.entity.save();
		}

	},

	lockApprovedStatus: function (executionContext) {
		//debugger;	
		var formContext = executionContext.getFormContext();
		var status = formContext.getAttribute("ofm_ops_manager_approval").getValue();

		var userSettings = Xrm.Utility.getGlobalContext().userSettings;
		var currentUser = new Array();
		currentUser[0] = new Object();
		currentUser[0].entityType = "systemuser";
		currentUser[0].id = userSettings.userId;
		currentUser[0].name = userSettings.userName;

		var currentDateTime = new Date();

		var userRoles = userSettings.roles;
		var isAdmin = false;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - System Administrator") {
				isAdmin = true;
			}
		});

		if (status == 2 && isAdmin != true) {
			formContext.getControl("ofm_ops_manager_approval").setDisabled(true);                       // lock the approval field
		}

		var approver = formContext.getAttribute("ofm_ops_approver").getValue();
		var approvalDate = formContext.getAttribute("ofm_ops_supervisor_approval_date").getValue();

		if (status != 2) {   // pending or not approved
			if (approver != null || approvalDate != null) {
				formContext.getAttribute("ofm_ops_approver").setValue(null); 				            // clear lookup	
				formContext.getAttribute("ofm_ops_supervisor_approval_date").setValue(null); 			// clear approval date
				formContext.data.entity.save();
				//formContext.data.refresh();					
			}
		}

		// Ministry EA Approval
		var statusReason = formContext.getAttribute("statuscode").getValue();
		if (statusReason == 8) {    // 8 = Active
			formContext.getControl("header_statuscode").setDisabled(true);                              // lock status Reason after Ministry EA Approval
		}

	},

	setMinistryEAApprovalYes: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var Id = formContext.data.entity.getId().replace("{", "").replace("}", "");

		var userSettings = Xrm.Utility.getGlobalContext().userSettings;
		var currentUser = new Array();
		currentUser[0] = new Object();
		currentUser[0].entityType = "systemuser";
		currentUser[0].id = userSettings.userId;
		currentUser[0].name = userSettings.userName;

		var currentDateTime = new Date();

		var confirmStrings = {
			title: "Confirm Ministry Expense Authority Approval",
			text: "Are you sure you want to approve this funding record? Please click Yes button to continue, or click No button to cancel.",
			confirmButtonLabel: "Yes",
			cancelButtonLabel: "No"
		};

		var confirmOptions = { height: 200, width: 550 };
		Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
			function (success) {
				if (success.confirmed) {
					formContext.getAttribute("statuscode").setValue(8);                                    // 8 = "Active"		
					formContext.getAttribute("ofm_ministry_approver").setValue(currentUser);               // set lookup to current user
					formContext.getAttribute("ofm_ministry_approval_date").setValue(currentDateTime); 	   // set now() to approval date
					formContext.getAttribute("ofm_ministry_approval").setValue(2); 	                       // 2 = Approved				
					formContext.data.entity.save();
				}
				else {
					formContext.getAttribute("statuscode").setValue(4);                                    // 4 = "In Review with Ministry EA"
					formContext.getAttribute("ofm_ministry_approver").setValue(null); 				       // clear lookup	
					formContext.getAttribute("ofm_ministry_approval_date").setValue(null); 			       // clear approval date	
					formContext.getAttribute("ofm_ministry_approval").setValue(1); 	                       // 1 = Pending					
					formContext.data.entity.save();
				}
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ message: error });
			});
	},

	setMinistryEAApprovalNo: function (primaryControl) {
		debugger;
		var formContext = primaryControl;

		var displayText1 = "You are denying this funding record. Please click Yes button to continue, or click No button to cancel.";
		var displayText2 = "For the denied funding record, its status should be manually set to Cancelled and the application should be manually set to Ineligible.";
		var confirmStrings = {
			title: "Confirm Ministry Expense Authority Deny",
			text: displayText1 + "\n\n" + "Note:" + "\n" + displayText2,
			confirmButtonLabel: "Yes",
			cancelButtonLabel: "No"
		};

		var confirmOptions = { height: 290, width: 550 };
		Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
			function (success) {
				if (success.confirmed) {
					formContext.getAttribute("statuscode").setValue(4);                                    // 4 = "In Review with Ministry EA"
					formContext.getAttribute("ofm_ministry_approver").setValue(null); 				       // clear lookup	
					formContext.getAttribute("ofm_ministry_approval_date").setValue(null); 			       // clear approval date	
					formContext.getAttribute("ofm_ministry_approval").setValue(3); 	                       // 0 - Not Approved					
					formContext.data.entity.save();
				}
				else {
					formContext.getAttribute("statuscode").setValue(4);                                    // 4 = "In Review with Ministry EA"
					formContext.getAttribute("ofm_ministry_approver").setValue(null); 				       // clear lookup	
					formContext.getAttribute("ofm_ministry_approval_date").setValue(null); 			       // clear approval date	
					formContext.getAttribute("ofm_ministry_approval").setValue(1); 	                       // 1 = Pending						
					formContext.data.entity.save();
				}
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ message: error });
			});
	},

	setMinistryEAUnapproval: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var Id = formContext.data.entity.getId().replace("{", "").replace("}", "");
		var ministryApproval = formContext.getAttribute("ofm_ministry_approval").getValue();

		var userSettings = Xrm.Utility.getGlobalContext().userSettings;
		var currentUser = new Array();
		currentUser[0] = new Object();
		currentUser[0].entityType = "systemuser";
		currentUser[0].id = userSettings.userId;
		currentUser[0].name = userSettings.userName;

		var currentDateTime = new Date();

		var displayText;
		if (ministryApproval == 2) {     // Approved
			displayText = "You are reversing the decision of an approved funding. Please click Yes button to continue, or click No button to cancel."
		}
		if (ministryApproval == 3) {     // Not Approved
			displayText = "You are reversing the decision of a denied funding. Please click Yes button to continue, or click No button to cancel."
		}

		var confirmStrings = {
			title: "Confirm Ministry Expense Authority Decision Reversal",
			text: displayText,
			confirmButtonLabel: "Yes",
			cancelButtonLabel: "No"
		};

		var confirmOptions = { height: 200, width: 600 };
		Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(
			function (success) {
				if (success.confirmed) {
					formContext.getAttribute("statuscode").setValue(4);                                    // 4 = "In Review with Ministry EA"
					formContext.getAttribute("ofm_ministry_approver").setValue(null); 				       // clear lookup	
					formContext.getAttribute("ofm_ministry_approval_date").setValue(null); 			       // clear approval date	
					formContext.getAttribute("ofm_ministry_approval").setValue(1); 	                       // 1 = Pending					
					formContext.data.entity.save();

				}
				else {
					// do nothing

				}
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ message: error });
			});
	},

	showHideApprovalFA: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var statusReason = formContext.getAttribute("statuscode").getValue()
		var opsApprover = formContext.getAttribute("ofm_ops_approver").getValue();
		var declarationFA = formContext.getAttribute("ofm_declaration").getValue();
		var providerApprover = formContext.getAttribute("ofm_provider_approver").getValue();
		var ministryApproval = formContext.getAttribute("ofm_ministry_approval").getValue();
		var currentUserId = Xrm.Utility.getGlobalContext().userSettings.userId.replace(/[{}]/g, "");

		return new Promise(function (resolve, reject) {
			Xrm.WebApi.retrieveRecord("systemuser", currentUserId, "?$select=ofm_is_expense_authority").then(
				function success(result) {
					resolve(statusReason == 4 && (result.ofm_is_expense_authority == true ? true : false) && opsApprover != null && providerApprover != null && declarationFA == true && ministryApproval == 1);                  // 4 = "In Review with Ministry EA" (statusReason), 1 = "pending" (ministry Approval)
				},
				function (error) {
					reject(error.message);
				}
			);
		});
	},

	showHideUnapproveFA: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var statusReason = formContext.getAttribute("statuscode").getValue();
		var opsApprover = formContext.getAttribute("ofm_ops_approver").getValue();
		var providerApprover = formContext.getAttribute("ofm_provider_approver").getValue();
		var ministryApprover = formContext.getAttribute("ofm_ministry_approver").getValue();
		var ministryApproval = formContext.getAttribute("ofm_ministry_approval").getValue();

		var isAdmin = false;
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - System Administrator") {
				isAdmin = true;
			}
		});

		var showButton = false;
		// reverse "Approved" decision
		if (statusReason == 8 && isAdmin == true && opsApprover != null && providerApprover != null && ministryApprover != null && ministryApproval == 2) {    // 8 = Active, 2 = "Approved" (ministry Approval)
			showButton = true;
		}

		// reverse "Not Approved" decision
		if (statusReason == 4 && isAdmin == true && opsApprover != null && providerApprover != null && ministryApproval == 3) {    // 4 = "In Review with Ministry EA", 3 = "Not Approved" (ministry Approval)
			showButton = true;
		}

		return showButton;
	},
	enableAgreementPDF: function (executionContext) {
		debugger;
		Xrm.WebApi.retrieveMultipleRecords('environmentvariablevalue', "?$select=value&$expand=EnvironmentVariableDefinitionId&$filter=(EnvironmentVariableDefinitionId/schemaname eq 'ofm_OFMFundingAgreementEnablePDFDelete')").then(
			function success(result) {
				debugger;
				var roles = result.entities[0].value;
				var rolesArray = roles.split(';');
				var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;

				for (var i = 0; i < userRoles.getLength(); i++) {
					for (var j = 0; j < rolesArray.length; j++)
						if (userRoles.get()[i].name == rolesArray[j]) {
							executionContext.getFormContext().getControl("ofm_agreement_file").setDisabled(false);
						}
				}
				//TODO: Add code here to process the Environment Variable value
			},
			function (error) {
				Xrm.Navigation.openErrorDialog({ details: error.message, message: 'A problem occurred while retrieving an Environment Variable value. Please contact support.' });
			}
		)
	},
	lockfieldsPCM: function (executionContext) {
		debugger; //PCM Access Role
		let formContext = executionContext.getFormContext();
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		if (userRoles.getLength() > 1) { }

		else if (userRoles.get()[0].name == "OFM - PCM") {
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

	},

	showHideGenerateFAPDF: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var statusReason = formContext.getAttribute("statuscode").getValue();

		var visable = false;
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - CRC") {
				visable = true;
			}
		});

		var showButton = false;
		//FA review, FA Signature Pending, FA Submitted, In Review with Ministry EA
		if (statusReason == 3 || statusReason == 5 || statusReason == 6 || statusReason == 4) {
			showButton = true;
		}

		return showButton && visable;
	},

	showHideRecalculate: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var visable = false;
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - PCM" || item.name === "OFM - CRC" || item.name === "OFM - Program Policy Analyst") {
				visable = true;
			}
		});

		return visable;
	},
	RemoveOptionFromPaymentFrequency: function (executionContext) {
		debugger;
		var formContext = executionContext.getFormContext();
		var retroActivePaymentDateField = formContext.getAttribute("ofm_retroactive_payment_date");
		var retroActivePaymentFrequencyField = formContext.getControl("ofm_retroactive_payment_frequency");
		var retroActivePaymentDate = retroActivePaymentDateField.getValue();
		if (retroActivePaymentDate != null) {
			formContext.getAttribute("ofm_retroactive_payment_frequency").setRequiredLevel("required");
		} else {
			formContext.getAttribute("ofm_retroactive_payment_frequency").setRequiredLevel("none");
			formContext.getAttribute("ofm_retroactive_payment_frequency").setValue(null);
		}
	},
	showHideCreateMOD: function (primaryControl) {
		debugger;
		var formContext = primaryControl;
		var statusReason = formContext.getAttribute("statuscode").getValue();

		var visable = false;
		var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
		userRoles.forEach(function hasRole(item, index) {
			if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - CRC" || item.name === "OFM - PCM" || item.name === "OFM - Program Policy Analyst") {
				visable = true;
			}
		});

		var showButton = false;
		//ACTIVE
		if (statusReason == 8) {
			showButton = true;
		}

		return showButton && visable;
	},
}