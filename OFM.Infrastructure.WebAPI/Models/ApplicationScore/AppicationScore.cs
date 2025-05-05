using ECC.Core.DataContext;
using Microsoft.Crm.Sdk.Messages;
using Newtonsoft.Json.Linq;
using OFM.Infrastructure.WebAPI.Extensions;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Models.ApplicationScore
{
    public static class DataverseQueries
    {
        public const string FundingApplicationQuery = "ofm_applications({0})?$select=ofm_applicationid,_ofm_facility_value,modifiedon,ofm_score_lastprocessed,ofm_summary_submittedon&$expand=ofm_document_application($select=ofm_category)";

        public const string UnprocessedApplicationsQuery = "ofm_applications?$select=ofm_applicationid,_ofm_facility_value,modifiedon,ofm_score_lastprocessed,ofm_summary_submittedon";
        public const string ModifiedApplicationsQuery = "ofm_applications?$filter=modifiedon gt '{0}'&$select=ofm_applicationid,_ofm_facility_value,modifiedon,ofm_score_lastprocessed,ofm_summary_submittedon";
        public const string ScoreParametersQuery = "ofm_application_score_parameteres?$filter=_ofm_application_score_calculator_value eq '{0}'&$expand=ofm_application_score_category($select=ofm_name)&$select=ofm_key,ofm_comparison_operator,ofm_score,_ofm_application_score_category_value";
        public const string facilityQuery = "accounts({0})?$select=address1_city,accountid,name,address1_postalcode,ofm_indigenous_led,ofm_date_of_incorporation,ofm_open_membership,ofm_board_members_elected_unpaid,ofm_board_members_selected_membership,ofm_board_members_residents_of_bc&$expand=ofm_facility_licence($select=ofm_acility_id_historical),parentaccountid($select=accountid,accountnumber,ofm_business_type)";
        public const string LicenseDataQuery = "ofm_licences?$filter=_ofm_facility_value eq {0}&$select=ofm_licence,ofm_acility_id_historical,ofm_ccof_facilityid,ofm_ccof_organizationid,_ofm_facility_value,ofm_start_date,ofm_end_date&$expand=ofm_funding_licencedetail($select=ofm_licence_type,ofm_start_date,ofm_end_date,ofm_licence_spaces,ofm_operational_spaces,ofm_enrolled_spaces)";
        public const string IncomeDataQuery = @$"ofm_accbs?fetchXml=
                                            <fetch>
                                              <entity name=""ofm_accb"">
                                                <filter>
                                                  <condition attribute=""ofm_postal_code"" operator=""eq"" value=""{{0}}"" />
                                                </filter>
                                              </entity>
                                            </fetch>";
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
                                                    </filter>
<link-entity name=""ofm_bcssa_chapter"" from=""ofm_bcssa_chapterid"" to=""ofm_bcssa_chapter"" alias=""b"">
                                                     <link-entity name=""ofm_school_district"" from=""ofm_bcssa_chapter"" to=""ofm_bcssa_chapterid"" alias=""s"">
                                                      <filter>
                                                        <condition attribute=""ofm_postal_code"" operator=""eq"" value=""{{1}}"" />
                                                      </filter>
                                                    </link-entity>
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
                                                    </filter>
                                                  </entity>
                                                </fetch>";
        public const string PopulationCentreQuery = $@"ofm_population_centres?fetchXml=
                                                <fetch>
                                                    <entity name=""ofm_population_centre"">
                                                        <filter>
                                                          <condition attribute=""ofm_city"" operator=""eq"" value=""{{0}}"" />
                                                        </filter>
                                                        <attribute name=""ofm_projected_population"" />
                                                      </entity>
                                                    </fetch>";

        public const string TotalOperationSpaces = @$"ofm_licence_details?fetchXml=
                                                <fetch aggregate=""true"">
                                                  <entity name=""ofm_licence_detail"">
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
                                                      </condition>
                                                    </filter>
                                                    <link-entity name=""ofm_licence"" from=""ofm_licenceid"" to=""ofm_licence"" alias=""f"">
                                                      <filter>
                                                        <condition attribute=""ofm_facility"" operator=""eq"" value=""{{0}}"" />
                                                      </filter>
                                                    </link-entity>
                                                  </entity>
                                                </fetch>
                                                ";
        public const string MaxChildSpaces = @$"ofm_licence_details?fetchXml=
                          <fetch aggregate=""true"">
                              <entity name=""ofm_licence_detail"">
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
                                <link-entity name=""ofm_licence"" from=""ofm_licenceid"" to=""ofm_licence"" alias=""f"">
                                  <filter>
                                    <condition attribute=""ofm_facility"" operator=""eq"" value=""{{0}}"" />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";




    }
    public class FundingApplication
    {
        protected readonly JsonObject _data;
        public JsonObject? ToJson()
        {
            return _data;
        }
        public FundingApplication(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public Guid Id => _data.GetPropertyValue<Guid>("ofm_applicationid");
        public Guid? FacilityId => _data.GetPropertyValue<Guid>("_ofm_facility_value");

        public DateTime? LastModified => _data.GetPropertyValue<DateTime>("modifiedon");
        public DateTime? LastProcessed => _data.GetPropertyValue<DateTime>("ofm_score_lastprocessed");


        public DateTime? LeaseStartDate => _data.GetPropertyValue<DateTime>("ofm_costs_lease_start_date");
        public DateTime? LeaseEndDate => _data.GetPropertyValue<DateTime>("ofm_costs_lease_end_date");



        public DateTime? SubmittedOn => _data.GetPropertyValue<DateTime>("ofm_summary_submittedon");

        public bool? LetterOfSupportExists => _data.GetPropertyValue<JsonArray>("ofm_document_application")?.AsArray()?.Where(x => x.AsObject().GetPropertyValue<string>("ofm_category") == "Community Support Letter")?.Any();
        public bool? GetProcessingStatus(string defaultValue = null) => _data.GetPropertyValue<bool>("ofm_score_processing_status");
    }

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

    }

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
        public string? IndigenousLead => _data.GetFormattedValue("ofm_indigenous_led");        

        public string? FacilityIdentifier => _data.GetPropertyValue<string>("accountnumber");
        public string[] HistoricalFacilityIdentifier => _data.GetPropertyValue<JsonArray>("ofm_facility_licence")?.AsArray()?.Select(x => x.AsObject().GetPropertyValue<string>("ofm_acility_id_historical"))?.ToArray();
        public string? OrganizationIdentifier => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetPropertyValue<string>("ofm_business_type");
        public string? OrganizationBusinessType => _data.GetPropertyValue<JsonObject>("parentaccountid")?.AsObject()?.GetFormattedValue("ofm_business_type");

        public DateTime? DateOfIncorporation => _data.GetPropertyValue<DateTime>("ofm_date_of_incorporation");
        public bool? OpenMembership => _data.GetPropertyValue<bool>("ofm_open_membership");
        public bool? BoardMembersUnpaid => _data.GetPropertyValue<bool>("ofm_board_members_elected_unpaid");
        public bool? BoardMembersMembership => _data.GetPropertyValue<bool>("ofm_board_members_selected_membership");
        public bool? BoardMembersBCResidents => _data.GetPropertyValue<bool>("ofm_board_members_residents_of_bc");




        protected T GetFormattedValue<T>(string attributeName, Func<object, T> formatter, T defaultValue = default)
        {
            if (!_data.ContainsKey(attributeName) || _data[attributeName] == null) return defaultValue;
            return formatter(_data[attributeName]);
        }
    }

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

    public class ACCBIncomeIndicator
    {
        protected readonly JsonObject _data;

        public ACCBIncomeIndicator(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string MedianIncome => _data.GetFormattedValue("ofm_income_indicator");

        
    }
    public class PopulationCentre
    {
        protected readonly JsonObject _data;

        public PopulationCentre(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int ProjectedPopulation => _data.GetPropertyValue<int>("ofm_projected_population");


    }
    public class SchoolDistrict
    {
        protected readonly JsonObject _data;

        public SchoolDistrict(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string? SchoolDistrictFullName => _data.GetPropertyValue<string>("ofm_school_district_fullname");


    }
    public class ApprovedParentFee
    {
        protected readonly JsonObject _data;

        public ApprovedParentFee(JsonObject data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }
        public string? FinancialYear => _data.GetFormattedValue("ccof_programyear");
        public decimal? FeeAmount => GetMaximumFee();
        public string? ProgramType => _data.GetPropertyValue<string>("_ofm_childcare_category_value");
        
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
            decimal? maxValue = validValues.Any() ? validValues.Max() * 5 : null;
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
        public string? ProgramType => _data.GetPropertyValue<string>("_ofm_childcare_category_value");
        public string? BCSSAChapter => _data.GetFormattedValue("_ofm_bcssa_chapter_value");

    }
}
