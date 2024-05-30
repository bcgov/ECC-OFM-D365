"use strict";

var OFM = OFM || {};
OFM.Account = OFM.Account || {};
OFM.Account.Ribbon = OFM.Account.Ribbon || {};

OFM.Account.Ribbon = {
    //Function called by "Verify Good Standing" ribbon button to open custom page
    OpenOrganizationCustomPage: function (primaryControl) {
        debugger;
        var formContext = primaryControl;
        var Id = formContext.data.entity.getId().replace("{", "").replace("}", "");

        var pageInput = {
            pageType: "custom",
            name: "ofm_goodstandingverificationpage_52f22",
            recordId: Id
        };
        var navigationOptions = {
            target: 2,                                   // 1 = open inline page, 2 = open dialog
            position: 1,                                 // 1 = centered dialog,  2 = side dialog 
            //  width: {value: 80, unit:"%"},
            //  height: { value: 80, unit: "%"}
            width: 540,
            height: 400,
            title: "Good Standing Verification Page"
        };

        Xrm.Navigation.navigateTo(pageInput, navigationOptions)
            .then(
                function () {
                    //	pageContext.data.refresh();
                    primaryControl.data.refresh();
                    console.log("Success");
                }
            ).catch(
                function (error) {
                    console.log(Error);
                }
            );
    },

    showHideVerifyGoodStanding: function (primaryControl) {
        debugger;

        var visable = false;
        var userRoles = Xrm.Utility.getGlobalContext().userSettings.roles;
        userRoles.forEach(function hasRole(item, index) {
            if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - Program Support" || item.name === "OFM - CRC" || item.name === "OFM - Program Policy Analyst") {
                visable = true;
            }
        });

        var accountFlag = false;
        var bypassFlag = false;
        var accountType = Xrm.Page.getAttribute('ccof_accounttype').getValue(); //100000000 - Organization
        if (accountType === 100000000) {
            accountFlag = true;
            var bypass = Xrm.Page.getAttribute('ofm_bypass_bc_registry_good_standing').getValue();
            if (bypass == null || bypass == false) {
                bypassFlag = true;
            }
        }
        return visable && accountFlag && bypassFlag;
    }

}