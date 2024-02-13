using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;

public interface IProviderProfileRepository
{
    Account? GetProviderProfile(Guid facilityId);
}

public class ProviderProfileRepository : IProviderProfileRepository
{
    public ProviderProfileRepository(ID365AppUserService appUserService, ID365WebApiService service)
    {

    }

    public Account? GetProviderProfile(Guid facilityId)
    {
        // ToDo: Fetch the associated licence details from the application in dataverse.
        // Below is an example

        //List<LicenceDetail> uniqueCoreServices = [
        //            new(new ofm_licence_detail()) { OrderId = 1, DaysPerWeek = 5 },
        //    new(new ofm_licence_detail()) { OrderId = 2, DaysPerWeek = 5 },
        //    new(new ofm_licence_detail()) { OrderId = 3, DaysPerWeek = 5 }
        //];


        //facilityId = "99917998-537b-ee11-8179-000d3a09d499";

        var fetchXML = $"""
             <fetch>
              <entity name="account">
                <filter>
                  <condition attribute="accountid" operator="eq" value="{facilityId}" />
                </filter>
                <link-entity name="ofm_licence" from="ofm_facility" to="accountid" alias="ApplicationLicense">
                  <attribute name="ofm_application" />
                  <attribute name="ofm_licenceid" />
                  <attribute name="createdon" />
                  <attribute name="ofm_accb_providerid" />
                  <attribute name="ofm_ccof_facilityid" />
                  <attribute name="ofm_ccof_organizationid" />
                  <attribute name="ofm_facility" />
                  <attribute name="ofm_health_authority" />
                  <attribute name="ofm_licence" />
                  <attribute name="ofm_tdad_funding_agreement_number" />
                  <attribute name="ownerid" />
                  <attribute name="statuscode" />
                  <filter>
                    <condition attribute="statecode" operator="eq" value="0" />
                  </filter>
                  <link-entity name="ofm_licence_detail" from="ofm_licence" to="ofm_licenceid" alias="Licence">
                    <attribute name="createdon" />
                    <attribute name="ofm_care_type" />
                    <attribute name="ofm_enrolled_spaces" />
                    <attribute name="ofm_licence" />
                    <attribute name="ofm_licence_detail" />
                    <attribute name="ofm_licence_spaces" />
                    <attribute name="ofm_licence_type" />
                    <attribute name="ofm_operation_hours_from" />
                    <attribute name="ofm_operation_hours_to" />
                    <attribute name="ofm_operational_spaces" />
                    <attribute name="ofm_overnight_care" />
                    <attribute name="ofm_week_days" />
                    <attribute name="ofm_weeks_in_operation" />
                    <attribute name="ownerid" />
                    <attribute name="statuscode" />
                  </link-entity>
                </link-entity>
              </entity>
            </fetch>
            """;

        // Return the Facility with the Licence Details
        return new Account();
    }
}