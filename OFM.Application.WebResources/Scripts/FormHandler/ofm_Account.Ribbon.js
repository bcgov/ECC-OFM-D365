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
        var formLabel = formContext.ui.formSelector.getCurrentItem().getLabel();
        var pageName;
        if (formLabel == "Organization Information") {
            pageName = "ccof_ccofgoodstandingcheckbcregistry_ee79c";
        }
        else if (formLabel == "Organization Information - OFM") {
            pageName = "ofm_goodstandingverificationpage_52f22";
        }

        var pageInput = {
            pageType: "custom",
            name: pageName,
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
            if (item.name === "OFM - System Administrator" || item.name === "OFM - Leadership" || item.name === "OFM - Program Support" || item.name === "OFM - CRC" || item.name === "OFM - Program Policy Analyst" || item.name === "CCOF - Leadership" || item.name === "CCOF - Sr. Adjudicator") {
                visable = true;
            }
        });

        var accountFlag = false;
        var bypassFlag = true;
        var accountType = primaryControl.getAttribute('ccof_accounttype').getValue(); //100000000 - Organization
        if (accountType === 100000000) {
            accountFlag = true;
            if (primaryControl.getAttribute("ofm_bypass_bc_registry_good_standing") != null) {
                bypassFlag = !(Xrm.Page.getAttribute('ofm_bypass_bc_registry_good_standing').getValue());
            }
            else if (primaryControl.getAttribute('ccof_bypass_goodstanding_check') != null) {
                bypassFlag = !(Xrm.Page.getAttribute('ccof_bypass_goodstanding_check').getValue());
            }
        }
        if (primaryControl.getAttribute("ccof_typeoforganization") != null && visable) {
            var businessType = primaryControl.getAttribute("ccof_typeoforganization").getValue();
            visable = businessType == 100000002 ? true : false;
        }
        return visable && accountFlag && bypassFlag;
    }

}