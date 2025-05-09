using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Models.ApplicationScore
{
    /// <summary>
    /// Contains predefined queries for retrieving data from Dataverse.
    /// These queries are used to fetch application details, facility information, licensing data, and more.
    /// </summary>

    public static class DataverseQueries
    {
        /// <summary>
        /// Query to fetch details of a specific funding application.
        /// Retrieves application ID, facility, modification date, processing status, and provider type.
        /// </summary>

        public const string FundingApplicationQuery = "ofm_applications({0})?$select=ofm_applicationid,_ofm_facility_value,modifiedon,ofm_score_lastprocessed,ofm_summary_submittedon,ofm_provider_type,ofm_costs_lease_start_date,ofm_costs_lease_end_date,ofm_month_to_month,ofm_costs_facility_type&$expand=ofm_document_application($select=ofm_category)";

        /// <summary>
        /// Query to fetch unprocessed submitted applications based on modification date.
        /// Filters applications that have been modified after a given date and are in an active state.
        /// </summary>

        public const string UnprocessedApplicationsQuery = @$"ofm_applications?fetchXml=<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"">
  <entity name=""ofm_application"">
    <attribute name=""ofm_applicationid"" />
    <attribute name=""ofm_facility"" />
    <attribute name=""modifiedon"" />
    <attribute name=""ofm_score_lastprocessed"" />
    <attribute name=""ofm_summary_submittedon"" />
    <filter>
      <condition attribute=""modifiedon"" operator=""gt"" value=""{{0}}"" />
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
<condition attribute=""ofm_summary_submittedon"" operator=""not-null"" />

      <filter type=""or"">
        <condition attribute=""ofm_score_processing_status"" operator=""eq"" value=""1"" />
        <condition attribute=""ofm_score_processing_status"" operator=""eq"" value=""3"" />
      </filter>
    </filter>
  </entity>
</fetch>";
        /// <summary>
        /// Query to fetch submitted applications modified after a certain date.
        /// </summary>

        public const string ModifiedApplicationsQuery = @$"ofm_applications?fetchXml=<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"">
  <entity name=""ofm_application"">
    <attribute name=""ofm_applicationid"" />
    <attribute name=""ofm_facility"" />
    <attribute name=""modifiedon"" />
    <attribute name=""ofm_score_lastprocessed"" />
    <attribute name=""ofm_summary_submittedon"" />
    <filter>
      <condition attribute=""ofm_summary_submittedon"" operator=""gt"" value=""{{0}}"" />
    </filter>
  </entity>
</fetch>
";
        /// <summary>
        /// Query to fetch application score parameters for an application score calculator.
        /// Retrieves scoring categories, comparison operators, and maximum scores.
        /// </summary>

        public const string ScoreParametersQuery = "ofm_application_score_parameteres?$filter=_ofm_application_score_calculator_value eq '{0}'&$expand=ofm_application_score_category($select=ofm_name,ofm_maximum_score)&$select=ofm_key,ofm_comparison_operator,ofm_score,_ofm_application_score_category_value";

        /// <summary>
        /// Query to fetch facility details.
        /// Retrieves facility location, organization details, and licensing information.
        /// </summary>

        public const string facilityQuery = "accounts({0})?$select=address1_city,accountid,name,address1_postalcode&$expand=ofm_facility_licence($select=ofm_acility_id_historical),parentaccountid($select=ofm_indigenous_led,accountid,accountnumber,name,ofm_business_type,ccof_typeoforganization,ofm_provider_type,ofm_is_public_sector,ofm_date_of_incorporation,ofm_open_membership,ofm_board_members_elected_unpaid,ofm_board_members_selected_membership,ofm_board_members_residents_of_bc),ccof_facility_feeregion($expand=ccof_region($expand=ccof_region_period_start,ccof_region_period_end))";
        /// <summary>
        /// Query to fetch license data for a facility.
        /// Retrieves ofm_licence_details such as start and end dates, facility ID, and operational spaces.
        /// </summary>
        public const string LicenseDataQuery = "ofm_licences?$filter=_ofm_facility_value eq {0}&$select=ofm_licence,ofm_acility_id_historical,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_start_date,ofm_end_date&$expand=ofm_funding_licencedetail($select=ofm_licence_type,ofm_start_date,ofm_end_date,ofm_licence_spaces,ofm_operational_spaces,ofm_enrolled_spaces)";
        /// <summary>
        /// Query to fetch income data based on postal code from ACCB assocaiated with Application Score Calculator
        /// Retrieves ACCB income indicators for a given region.
        /// </summary>

        public const string IncomeDataQuery = @$"ofm_accbs?fetchXml=
                                            <fetch>
                                              <entity name=""ofm_accb"">
                                                <filter>
                                                  <condition attribute=""ofm_postal_code"" operator=""eq"" value=""{{0}}"" />
                                                  <condition attribute=""ofm_application_score_calculator"" operator=""eq"" value=""{{1}}"" />
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                                </filter>
                                              </entity>
                                            </fetch>";
        /// <summary>
        /// Query to fetch approved parent fee data.
        /// Retrieves financial details related to childcare fees.
        /// </summary>
        public const string AppovedFeeDataQuery = @$"ccof_parent_feeses?fetchXml=<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
  <entity name=""ccof_parent_fees"">
    <attribute name=""ccof_parent_feesid"" />
    <attribute name=""ccof_name"" />
    <attribute name=""createdon"" />
    <attribute name=""ccof_type"" />
    <attribute name=""statuscode"" />
    <attribute name=""statecode"" />
    <attribute name=""ccof_dm_sourceid"" />
    <attribute name=""ccof_sep_base"" />
    <attribute name=""ccof_sep"" />
    <attribute name=""overriddencreatedon"" />
    <attribute name=""ccof_programyear"" />
    <attribute name=""owningbusinessunit"" />
    <attribute name=""ownerid"" />
    <attribute name=""ccof_oct_base"" />
    <attribute name=""ccof_oct"" />
    <attribute name=""ccof_nov_base"" />
    <attribute name=""ccof_nov"" />
    <attribute name=""modifiedon"" />
    <attribute name=""modifiedonbehalfby"" />
    <attribute name=""modifiedby"" />
    <attribute name=""ccof_may_base"" />
    <attribute name=""ccof_may"" />
    <attribute name=""ccof_mar_base"" />
    <attribute name=""ccof_mar"" />
    <attribute name=""ccof_jun_base"" />
    <attribute name=""ccof_jun"" />
    <attribute name=""ccof_jul_base"" />
    <attribute name=""ccof_jul"" />
    <attribute name=""ccof_jan_base"" />
    <attribute name=""ccof_jan"" />
    <attribute name=""ccof_frequency"" />
    <attribute name=""ccof_feb_base"" />
    <attribute name=""ccof_feb"" />
    <attribute name=""ccof_facility"" />
    <attribute name=""exchangerate"" />
    <attribute name=""ccof_dec_base"" />
    <attribute name=""ccof_dec"" />
    <attribute name=""transactioncurrencyid"" />
    <attribute name=""createdonbehalfby"" />
    <attribute name=""createdby"" />
    <attribute name=""ccof_childcarecategory"" />
    <attribute name=""ccof_changeactionmtfi"" />
    <attribute name=""ccof_availability"" />
    <attribute name=""ccof_aug_base"" />
    <attribute name=""ccof_aug"" />
    <attribute name=""ccof_apr_base"" />
    <attribute name=""ccof_apr"" />
    <attribute name=""ccof_approveddate"" />
    <attribute name=""ccof_regarding"" />
    <order attribute=""ccof_name"" descending=""false"" />
    <filter type=""and"">
      <condition attribute=""ccof_facility"" operator=""eq"" value=""{{0}}"" />
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
    </filter>
</entity>
</fetch>";
        public const string ThresholdFeeDataQuery = @$"ofm_forty_percentile_fees?fetchXml=
                                                <fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                                                  <entity name=""ofm_forty_percentile_fee"">
                                                    <all-attributes/>
                                                    <order attribute=""ofm_name"" descending=""false"" />
                                                    <filter type=""and"">
                                                      <condition attribute=""ofm_application_score_calculator"" operator=""eq"" value=""{{0}}""/>                                                    
      <condition attribute=""statecode"" operator=""eq"" value=""0"" />
    </filter>
    <link-entity alias=""region"" name=""ccof_fee_region"" to=""ofm_region"" from=""ccof_fee_regionid"" link-type=""outer"" visible=""false"">
      <attribute name=""ccof_region_period_start"" />
      <attribute name=""ccof_region_period_end"" />
    </link-entity>
                                                  </entity>
                                                </fetch>";
        public const string CreateScoreEndpoint = "ofm_application_scores";
        public const string UpdateApplicationEndpoint = "ofm_applications({0})";
        public const string UpsertApplicationScoreEndpoint = "ofm_application_scores({0})";
        public const string SchoolDistrictQuery = $@"ofm_school_districts?fetchXml=
                                                <fetch>
                                                  <entity name=""ofm_school_district"">
                                                    <attribute name=""ofm_school_district_fullname"" />
                                                    <filter>
                                                      <condition attribute=""ofm_postal_code"" operator=""eq"" value=""{{0}}"" />
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                                    </filter>
<link-entity name=""ofm_asc_sd"" from=""ofm_school_districtid"" to=""ofm_school_districtid"" alias=""asc"">
                                                      <filter>
                                                        <condition attribute=""ofm_application_score_calculatorid"" operator=""eq"" value=""{{1}}"" />

                                                      </filter>
                                                    </link-entity>

                                                  </entity>
                                                </fetch>";
        public const string PopulationCentreQuery = $@"ofm_population_centres?fetchXml=
                                                <fetch>
                                                    <entity name=""ofm_population_centre"">
                                                        <filter>

                                                          <condition attribute=""ofm_city"" operator=""eq"" value=""{{0}}"" />
<condition attribute=""ofm_application_score_calculator"" operator=""eq"" value=""{{1}}"" />
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
                                                        </filter>
                                                        <attribute name=""ofm_projected_population"" />
                                                      </entity>
                                                    </fetch>";




        public const string LicenseSpaces = @$"ofm_licences?fetchXml=<fetch aggregate=""true"">
  <entity name=""ofm_licence"">
   <filter type=""and"">
<condition attribute=""statecode"" operator=""eq"" value=""0"" />
      <condition attribute=""ofm_facility"" operator=""eq"" value=""{{0}}"" />
      <filter type=""or"">
        <filter type=""and"">
          <condition attribute=""ofm_start_date"" operator=""on-or-before"" value=""{{1}}"" />
          <condition attribute=""ofm_end_date"" operator=""null"" />
        </filter>
        <filter type=""and"">
          <condition attribute=""ofm_start_date"" operator=""on-or-before"" value=""{{1}}"" />
          <condition attribute=""ofm_end_date"" operator=""on-or-after"" value=""{{1}}"" />
        </filter>
      </filter>
    </filter>
    <link-entity name=""ofm_licence_detail"" from=""ofm_licence"" to=""ofm_licenceid"" link-type=""outer"">
      <attribute name=""ofm_operational_spaces"" alias=""TotalSpaces"" aggregate=""sum"" />
      <filter>
        <condition attribute=""ofm_licence_type"" operator=""in"">
           <value>1</value>
 <value>2</value>
 <value>4</value>
 <value>5</value>
 <value>6</value>
 <value>12</value>
 <value>3</value>
 <value>7</value>
 <value>8</value>
 <value>9</value>
 <value>10</value>
 <value>11</value>
 <value>12</value>
        </condition>
      </filter>
    </link-entity>
    <link-entity name=""ofm_licence_detail"" from=""ofm_licence"" to=""ofm_licenceid"" link-type=""outer"">
      <attribute name=""ofm_operational_spaces"" alias=""MaxSpaces"" aggregate=""sum"" />
      <filter>
        <condition attribute=""ofm_licence_type"" operator=""in"">
          <value>1</value>
<value>2</value>
<value>4</value>
<value>5</value>
<value>6</value>
<value>12</value>
        </condition>
      </filter>
    </link-entity>
  </entity>
</fetch>";
        



    }
    /// <summary>
    /// Represents a OFM Application entity.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class OFMApplication
    {
        protected readonly JsonObject _data;
        public JsonObject? ToJson()
        {
            return _data;
        }
        public OFMApplication(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }
        public string? ProviderType => _data.GetFormattedValue("ofm_provider_type");
        public Guid Id => _data.GetPropertyValue<Guid>("ofm_applicationid");
        public Guid? FacilityId => _data.GetPropertyValue<Guid>("_ofm_facility_value");

        public DateTime? LastModified => _data.GetPropertyValue<DateTime>("modifiedon");
        public DateTime? LastProcessed => _data.GetPropertyValue<DateTime>("ofm_score_lastprocessed");

        public string? MonthToMonth => _data.GetFormattedValue("ofm_month_to_month");
        public string? FacilityType => _data.GetFormattedValue("ofm_costs_facility_type");
        public DateTime? LeaseStartDate => _data.GetPropertyValue<DateTime>("ofm_costs_lease_start_date");
        public DateTime? LeaseEndDate => _data.GetPropertyValue<DateTime>("ofm_costs_lease_end_date");



        public DateTime? SubmittedOn => _data.GetPropertyValue<DateTime>("ofm_summary_submittedon");

        public bool? LetterOfSupportExists => _data.GetPropertyValue<JsonArray>("ofm_document_application")?.AsArray()?.Where(x => x.AsObject().GetPropertyValue<string>("ofm_category") == "Community Support Letter")?.Any();
        public bool? GetProcessingStatus(string defaultValue = null) => _data.GetPropertyValue<bool>("ofm_score_processing_status");
    }
    /// <summary>
    /// Represents a Application Score Paramter table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class ScoreParameter
    {
        protected readonly JsonObject _data;

        public ScoreParameter(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string? CategoryName => _data.GetPropertyValue<JsonObject>("ofm_application_score_category")?.AsObject()?.GetPropertyValue<string>("ofm_name");

        public string? Key => _data.GetPropertyValue<string>("ofm_key");
        public int? ComparisonOperator => _data.GetPropertyValue<int>("ofm_comparison_operator");
        public string? ComparisonValue => _data.GetPropertyValue<string>("ofm_key");
        public int? Score => _data.GetPropertyValue<int>("ofm_score");
        public Guid? ScoreCategoryId => _data.GetPropertyValue<Guid>("_ofm_application_score_category_value");

        public int? MaxScore => _data.GetPropertyValue<JsonObject>("ofm_application_score_category")?.AsObject()?.GetPropertyValue<int>("ofm_maximum_score");
    }
    /// <summary>
    /// Represents a Facility table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class Facility
    {
        //"accounts({0})?$select=address1_city,accountid,name,address1_postalcode,ofm_indigenous_led,ofm_date_of_incorporation,ofm_open_membership,ofm_board_members_elected_unpaid,ofm_board_members_selected_membership,ofm_board_members_residents_of_bc&$expand=ofm_facility_licence($select=ofm_acility_id_historical),parentaccountid($select=accountid,accountnumber,ofm_business_type)"
        protected readonly JsonObject _data;
        public Facility(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public Guid Id => _data.GetPropertyValue<Guid>("accountid");
        public string? City => _data.GetPropertyValue<string>("address1_city");
        public string? PostalCode => _data.GetPropertyValue<string>("address1_postalcode");
        public bool? IndigenousLead => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetPropertyValue
<bool?>("ofm_indigenous_led");

        public string? FacilityIdentifier => _data.GetPropertyValue<string>("accountnumber");
        public string[] HistoricalFacilityIdentifier => _data.GetPropertyValue<JsonArray>("ofm_facility_licence")?.AsArray()?.Select(x => x.AsObject().GetPropertyValue<string>("ofm_acility_id_historical"))?.ToArray();
        public string? OrganizationIdentifier => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetPropertyValue<string>("accountnumber");
        public string? OrganizationBusinessType => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_business_type") ?? _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ccof_typeoforganization");
        public string? OrganizationLegalName => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetPropertyValue<string>("name");
        public string? OrganizationPublicSector => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_is_public_sector");



        public DateTime? OrganizationDateOfIncorporation => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetPropertyValue<DateTime>("ofm_date_of_incorporation");
        public string? OrganizationOpenMembership => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_open_membership");
        public string? OrganizationBoardMembersUnpaid => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_board_members_elected_unpaid");
        public string? OrganizationBoardMembersMembership => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_board_members_selected_membership");
        public string? OrganizationBoardMembersBCResidents => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_board_members_residents_of_bc");
        //ofm_provider_type

        public string Region => _data.GetPropertyValue<JsonArray>("ccof_facility_feeregion")?.Select(fr => fr?.AsObject().GetPropertyValue<JsonObject>("ccof_region"))?.Where(r => r?.GetPropertyValue<JsonObject>("ccof_region_period_end") == null)?.Select(x => x?.GetPropertyValue<string>("ccof_name")).FirstOrDefault() ?? string.Empty;



    }
    /// <summary>
    /// Represents a Application Score Parameter table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class LicenseSpaces
    {
        protected readonly JsonObject _data;

        public LicenseSpaces(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }
        public decimal? MaxPreSchoolChildCareSpaces => _data.GetPropertyValue<decimal>("MaxSpaces");
        public decimal? TotalChildCareSpaces => _data.GetPropertyValue<decimal>("TotalSpaces");
        public Guid? FacilityId => _data.GetPropertyValue<Guid>("_ofm_facility_value");

    }
    /// <summary>
    /// Represents ACCB table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class ACCBIncomeIndicator
    {
        protected readonly JsonObject _data;

        public ACCBIncomeIndicator(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string MedianIncome => _data.GetFormattedValue("ofm_income_indicator");


    }
    /// <summary>
    /// Represents a Poplulation Centre table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class PopulationCentre
    {
        protected readonly JsonObject _data;

        public PopulationCentre(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int ProjectedPopulation => _data.GetPropertyValue<int>("ofm_projected_population");


    }
    /// <summary>
    /// Represents a School District table.
    /// Contains properties mapping Dataverse fields.
    /// </summary>
    public class SchoolDistrict
    {
        protected readonly JsonObject _data;

        public SchoolDistrict(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string? SchoolDistrictFullName => _data.GetPropertyValue<string>("ofm_school_district_fullname");


    }
    public class PublicOrganization
    {
        protected readonly JsonObject _data;

        public PublicOrganization(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string? OrganizationName => _data.GetPropertyValue<string>("ofm_legal_name");


    }
    public class ApprovedParentFee
    {
        protected readonly JsonObject _data;

        public ApprovedParentFee(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string? FinancialYear => _data.GetFormattedValue("_ccof_programyear_value");
        public decimal? FeeAmount => GetMaximumFee();
        public string? ProgramType => _data.GetFormattedValue("_ccof_childcarecategory_value");
        //ccof_approveddate
        public DateTime? ApproveDate => _data.GetPropertyValue<DateTime?>("ccof_approveddate");
        private decimal? GetMaximumFee()
        {
            string[] monthFees = {
                        "ccof_jan", "ccof_feb", "ccof_mar", "ccof_apr",
                        "ccof_may", "ccof_jun", "ccof_jul", "ccof_aug",
                        "ccof_sep", "ccof_oct", "ccof_nov", "ccof_dec"
                    };
            // Filter out null values and check if the property exists
            var validValues = _data.Where(kv => monthFees.Contains(kv.Key) && kv.Value != null)
                                     .Select(kv => decimal.Parse(kv.Value?.ToString()));
            decimal? maxValue = validValues.Any() ? validValues.Max() : null;
            if (_data.GetFormattedValue("ccof_frequency")?.ToLower() == "weekly")
                return maxValue * 5;
            if (_data.GetFormattedValue("ccof_frequency")?.ToLower() == "daily")
                return maxValue * 21;

            return maxValue;
        }
    }

    public class FortyPercentileThresholdFee
    {
        protected readonly JsonObject _data;

        public FortyPercentileThresholdFee(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public decimal? MaximumFeeAmount => _data.GetPropertyValue<decimal>("ofm_threshold_fee");
        public string? ProgramType => _data.GetFormattedValue("_ofm_childcare_category_value");
        public string? Region => _data.GetFormattedValue("_ofm_region_value");
        public string? ProviderType => _data.GetFormattedValue("ofm_provider_type");

        //region.ccof_region_period_start
        public string? ProgramYear => _data.ContainsKey("region.ccof_region_period_start") ? _data.GetFormattedValue("region.ccof_region_period_start") : null;
    }
}
