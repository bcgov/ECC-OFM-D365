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

	//ValidatePaymentStartdate: function (executionContext) {
	//	debugger;
	//	var formContext = executionContext.getFormContext();
	//	var selectedPaymentDate = formContext.getAttribute("ofm_start_date").getValue();

	//	var futureDate = new Date();
	//	var addedDays = 0;
	//	var businessDaysToAdd = 5;

	//	while (addedDays < businessDaysToAdd) {
	//		futureDate = futureDate.setDate(futureDate.getDate() + 1);
	//		if (futureDate.	 != DayOfWeek.Saturday &&
	//			futureDate.getDay != DayOfWeek.Sunday) {
	//			addedDays++;
	//		}
	//	}
	//	if (selectedPaymentDate <= futureDate) {
	//		formContext.getControl("ofm_start_date").setNotification("Select a date 5 days in the future from today", "paymentdate");
	//	}
	//	else {
	//		formContext.getControl("ofm_start_date").clearNotification();
	//	}
		
	//},

}