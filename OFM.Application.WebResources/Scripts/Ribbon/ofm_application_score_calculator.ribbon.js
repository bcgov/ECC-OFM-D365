"use strict";

var OFM = OFM || {};
OFM.application_score_calculator = OFM.application_score_calculator || {};
OFM.application_score_calculator.Ribbon = OFM.application_score_calculator.Ribbon || {};

OFM.application_score_calculator.Ribbon = {

    approveCalculator: function (primaryControl) {
        var formContext = primaryControl;
        var entityId = formContext.data.entity.getId().replace("{", "").replace("}", "");
        var entityName = formContext.data.entity.getEntityName();

        var data = {
            "statuscode": 1 // Set statuscode to 1 (e.g., Active)
        };

        Xrm.WebApi.updateRecord(entityName, entityId, data).then(
            function success(result) {
                Xrm.Navigation.openAlertDialog({ text: "Status updated successfully!" });
                formContext.data.refresh(false); // Refresh form without saving
            },
            function error(error) {
                Xrm.Navigation.openAlertDialog({ text: "Error updating status: " + error.message });
            }
        );
    },

    isApproveVisible: function (primaryControl) {
        var userId = Xrm.Utility.getGlobalContext().userSettings.userId.replace("{", "").replace("}", "");
        var fetchData = {
            "role1": "OFM - System Administrator",
            "role2": "System Administrator",
            "systemuserid": userId
        };
        var fetchXmlTeams = [
            "<fetch top='50' aggregate='true'>",
            "  <entity name='role'>",
            "    <attribute name='name' alias='count' aggregate='count'/>",
            "    <filter>",
            "      <condition attribute='name' operator='in'>",
            "        <value>", fetchData.role1/*OFM - System Administrator*/, "</value>",
            "        <value>", fetchData.role2/*OFM - System Administrator*/, "</value>",
            "      </condition>",
            "    </filter>",
            "    <link-entity name='teamroles' from='roleid' to='roleid' intersect='true'>",
            "      <link-entity name='team' from='teamid' to='teamid' intersect='true'>",
            "        <link-entity name='teammembership' from='teamid' to='teamid' intersect='true'>",
            "          <link-entity name='systemuser' from='systemuserid' to='systemuserid' intersect='true'>",
            "            <filter>",
            "              <condition attribute='systemuserid' operator='eq' value='", fetchData.systemuserid/*5de6fde2-ef16-f011-998a-7ced8d05e0a9*/, "' uiname='Harpreet Singh-Hans' uitype='systemuser'/>",
            "            </filter>",
            "          </link-entity>",
            "        </link-entity>",
            "      </link-entity>",
            "    </link-entity>",
            "  </entity>",
            "</fetch>"
        ].join("");
        
        var fetchXmlUsers = [
            "<fetch top='50' aggregate='true'>",
            "  <entity name='role'>",
            "    <attribute name='name' alias='count' aggregate='count'/>",
            "    <filter>",
            "      <condition attribute='name' operator='in'>",
            "        <value>", fetchData.role1/*OFM - System Administrator*/, "</value>",
            "        <value>", fetchData.role2/*OFM - System Administrator*/, "</value>",
            "      </condition>",
            "    </filter>",
            "    <link-entity name='systemuserroles' from='roleid' to='roleid' intersect='true'>",
            "      <filter>",
            "        <condition attribute='systemuserid' operator='eq' value='", fetchData.systemuserid/*00000000-0000-0000-0000-000000000000*/, "'/>",
            "      </filter>",
            "    </link-entity>",
            "  </entity>",
            "</fetch>"
        ].join("");

        var roleQuery1 = "?fetchXml=" + fetchXmlTeams;
        var roleQuery2 = "?fetchXml=" + fetchXmlUsers;

        Promise.all([Xrm.WebApi.retrieveMultipleRecords("role", roleQuery1), Xrm.WebApi.retrieveMultipleRecords("role", roleQuery2)])
            .then((results) => {
                let roleCount = results.reduce((sum, res) => sum + (res.entities.length > 0 ? res.entities[0].count : 0), 0);
                return roleCount > 0;
            })
            .catch((error) => {
                console.error("Error retrieving user teams: " + error.message);
                return false;
            });

    }
};