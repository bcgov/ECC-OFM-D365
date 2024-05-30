"use strict";

var OFM = OFM || {};
OFM.ContactList = OFM.ContactList || {};
OFM.ContactList.Ribbon = OFM.ContactList.Ribbon || {};

OFM.ContactList.Ribbon = {
    //Function called by "Send Notifications" ribbon button to open custom page
    OpenContactListCustomPage: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var Id = formContext.data.entity.getId().replace("{", "").replace("}", "");

        var pageInput = {
            pageType: "custom",
            name: "ofm_createbulkemailspage_add1c",
            recordId: Id
        };
        var navigationOptions = {
            target: 2,                                   // 1 = open inline page, 2 = open dialog
            position: 1,                                 // 1 = centered dialog,  2 = side dialog 
            width: { value: 75, unit: "%" },
            height: { value: 90, unit: "%" }
        };

        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    console.log("Success");
                }
            ).catch(
                function (error) {
                    console.log(Error);
                }
            );
    },

    showHideSendNotification: function (primaryControl) {
        var visable = false;

        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        userRoles.forEach(function hasRole(item, index) {
            if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership") {
                visable = true;
            }
        });

        return visable;
    }

}