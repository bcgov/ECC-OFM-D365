﻿using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OFM.Infrastructure.WebAPI.Models;

#region Contact-related objects for Portal

public record FacilityPermission
{
    public required string ofm_bceid_facilityid { get; set; }
    public bool? ofm_portal_access { get; set; }
    public bool? ofm_is_expense_authority { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public required D365Facility facility { get; set; }
}

public record D365Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public string? name { get; set; }
    public int ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public int? ofm_program { get; set; }
    public DateTime? ofm_program_start_date { get; set; }
    public bool? ofm_ccof_requirement { get; set; }
    public int? ofm_unionized { get; set; }
    public FacilityLicence[]? ofm_facility_licence { get; set; }
}

public record D365Organization
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public int? ccof_accounttype { get; set; }
    public string? name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public int? ofm_program { get; set; }
}

public class ProviderProfile
{
    public string? contactid { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? emailaddress1 { get; set; }
    public string? telephone1 { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public D365Organization? organization { get; set; }
    public PortalRole? role { get; set; }
    public IList<FacilityPermission>? facility_permission { get; set; }

    public void MapProviderProfile(IEnumerable<D365Contact> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);

        if (contacts.Count() == 0) throw new ArgumentException($"Must have at least one facility permission! {nameof(D365Contact)}");

        var facilityPermissions = new List<FacilityPermission>();
        var firstContact = contacts.First();

        contactid = firstContact.contactid;
        ccof_userid = firstContact.ccof_userid;
        ccof_username = firstContact.ccof_username;
        ofm_first_name = firstContact.ofm_first_name;
        ofm_last_name = firstContact.ofm_last_name;
        emailaddress1 = firstContact.emailaddress1;
        telephone1 = firstContact.telephone1;

        organization = new D365Organization
        {
            accountid = firstContact!.parentcustomerid_account!.accountid!,
            accountnumber = firstContact.parentcustomerid_account.accountnumber,
            name = firstContact.parentcustomerid_account.name,
            ccof_accounttype = firstContact.parentcustomerid_account.ccof_accounttype,
            statecode = firstContact.parentcustomerid_account.statecode,
            statuscode = firstContact.parentcustomerid_account.statuscode,
            ofm_program = firstContact.parentcustomerid_account.ofm_program
        };

        role = new PortalRole
        {
            ofm_portal_roleid = firstContact.ofm_portal_role_id?.ofm_portal_roleid,
            ofm_portal_role_number = firstContact.ofm_portal_role_id?.ofm_portal_role_number

        };

        for (int i = 0; i < firstContact.ofm_facility_business_bceid!.Count(); i++)
        {
            if (firstContact.ofm_facility_business_bceid![i] is not null &&
                firstContact.ofm_facility_business_bceid[i].ofm_facility is not null)
            {
                var facility = firstContact.ofm_facility_business_bceid[i].ofm_facility!;
                facilityPermissions.Add(new FacilityPermission
                {
                    ofm_bceid_facilityid = firstContact.ofm_facility_business_bceid![i].ofm_bceid_facilityid!,
                    facility = new D365Facility
                    {
                        accountid = facility.accountid ?? "",
                        accountnumber = facility.accountnumber,
                        name = facility.name,
                        statecode = facility.statecode,
                        statuscode = facility.statuscode,
                        ofm_program = facility.ofm_program,
                        ofm_program_start_date = facility.ofm_program_start_date,
                        ofm_ccof_requirement = facility.ofm_ccof_requirement,
                        ofm_unionized = facility.ofm_unionized
                    },
                    ofm_portal_access = firstContact.ofm_facility_business_bceid[i].ofm_portal_access,
                    ofm_is_expense_authority = firstContact.ofm_facility_business_bceid[i].ofm_is_expense_authority,
                    statecode = firstContact.ofm_facility_business_bceid[i].statecode,
                    statuscode = firstContact.ofm_facility_business_bceid[i].statuscode
                });
            }
        }

        facility_permission = facilityPermissions;
    }
}

#endregion

#region Temp Contact-related objects for serialization

public record D365Contact
{
    public string? odataetag { get; set; }
    public string? ofm_first_name { get; set; }
    public string? ofm_last_name { get; set; }
    public string? ccof_userid { get; set; }
    public string? ccof_username { get; set; }
    public string? contactid { get; set; }
    public string? emailaddress1 { get; set; }
    public string? telephone1 { get; set; }
    public ofm_Facility_Business_Bceid[]? ofm_facility_business_bceid { get; set; }
    public Parentcustomerid_Account? parentcustomerid_account { get; set; }
    public PortalRole? ofm_portal_role_id { get; set; }
}

public record PortalRole
{
    public Guid? ofm_portal_roleid { get; set; }
    public string? ofm_portal_role_number { get; set; }
}

public record Parentcustomerid_Account
{
    public string? accountid { get; set; }
    public string? accountnumber { get; set; }
    public int ccof_accounttype { get; set; }
    public int? ofm_program { get; set; }
    public string? name { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record ofm_Facility_Business_Bceid
{
    public string? _ofm_bceid_value { get; set; }
    public string? _ofm_facility_value { get; set; }
    public string? ofm_name { get; set; }
    public bool? ofm_portal_access { get; set; }
    public bool? ofm_is_expense_authority { get; set; }
    public string? ofm_bceid_facilityid { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public ofm_Facility? ofm_facility { get; set; }
}

public record ofm_Facility
{
    public required string accountid { get; set; }
    public string? accountnumber { get; set; }
    public int? ccof_accounttype { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public string? name { get; set; }
    public int? ofm_program { get; set; }
    public DateTime? ofm_program_start_date { get; set; }
    public bool? ofm_ccof_requirement { get; set; }
    public int? ofm_unionized { get; set; }
}

public class Supplementary : ofm_allowance
{
    public string _ofm_application_value { get; set; }

}


public record ofm_reminders
{
    public required string ofm_reminderid { get; set; }
    public int ofm_template_number { get; set; }
    public int statuscode { get; set; }
    public int? ofm_year_number { get; set; }

    public DateTime ofm_due_date { get; set; }
}

#endregion

public record D365Template
{
    public string? title { get; set; }
    public string? safehtml { get; set; }
    public string? subjectsafehtml { get; set; }
    public string? body { get; set; }
    public string? templateid { get; set; }
    public string? templatecode { get; set; }
}

public record D365AssistanceRequest
{
    public string? ofm_assistance_requestid { get; set; }
    public string? ofm_name { get; set; }
    public string? ofm_subject { get; set; }
    public DateTime? ofm_submission_time { get; set; }
    public string? _ofm_contact_value { get; set; }
    public bool? ofm_is_read { get; set; }
    [property: JsonPropertyName("contact.emailaddress1")]
    public string? emailaddress1 { get; set; }
}
public record D365Email
{
    public string? activityid { get; set; }
    public string? subject { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
    public string? sender { get; set; }
    public string? torecipients { get; set; }
    public string? _ofm_communication_type_value { get; set; }
    public int? Toparticipationtypemask { get; set; }
    public bool? isworkflowcreated { get; set; }
    public DateTime? lastopenedtime { get; set; }
    public DateTime? ofm_sent_on { get; set; }
    public DateTime? ofm_expiry_time { get; set; }
    public string? _regardingobjectid_value { get; set; }

    public Email_Activity_Parties[] email_activity_parties { get; set; }

    public bool IsCompleted
    {
        get
        {
            return (statecode == 1);
        }
    }
}

public record D365Organization_Account
{
    public string? accountid { get; set; }
    public string? name { get; set; }
    public string? ofm_incorporation_number { get; set; }
    public string? ofm_business_number { get; set; }
    public bool? ofm_bypass_bc_registry_good_standing { get; set; }
    public int statecode { get; set; }
    public Guid _primarycontactid_value { get; set; }
    public Guid _ofm_primarycontact_value { get; set; }
    [property: JsonPropertyName("contact.emailaddress1")]
    public string? primarycontactemail { get; set; }
}


public record D365StandingHistory
{
    public string? ofm_standing_historyid { get; set; }
    public string? _ofm_organization_value { get; set; }
    public int? ofm_good_standing_status { get; set; }
    public DateTime? ofm_start_date { get; set; }
    public DateTime? ofm_end_date { get; set; }
    public DateTime? ofm_validated_on { get; set; }
    public decimal? ofm_duration { get; set; }
    public int? ofm_no_counter { get; set; }
    public int statecode { get; set; }
    public int statuscode { get; set; }
}

public record FileMapping
{
    public required string ofm_subject { get; set; }
    public required string ofm_description { get; set; }
    public required string ofm_extension { get; set; }
    public required decimal ofm_file_size { get; set; }
    public required string entity_name_set { get; set; }
    public required string regardingid { get; set; }
    public required string ofm_category { get; set; }
}

public record Email_Activity_Parties
{
    public int? participationtypemask { get; set; }
    public string? _partyid_value { get; set; }
    public string? _activityid_value { get; set; }
    public string? activitypartyid { get; set; }
    public string? addressused { get; set; }
}

public record D365CommunicationType
{
    public string? ofm_communication_typeid { get; set; }
    public Int16? ofm_communication_type_number { get; set; }
}
public class BRQuestion : ofm_question_business_rule
{
    [property: JsonPropertyName("true.ofm_source_question_id")]
    public string? TrueSourcequestionIdentifier { get; set; }
    [property: JsonPropertyName("false.ofm_source_question_id")]
    public string? FalseSourcequestionIdentifier { get; set; }
    [property: JsonPropertyName("child.ofm_source_question_id")]
    public string? childSourcequestionIdentifier { get; set; }
    [property: JsonPropertyName("survey.ofm_version")]
    public string? surveyVersion { get; set; }
    [property: JsonPropertyName("survey.statecode")]
    public int? survey_statecode { get; set; }

    [property: JsonPropertyName("que.ofm_source_question_id")]
    public string? brSourceQuestion { get; set; }

    public Guid _ofm_true_child_question_value { get; set; }
    public Guid _ofm_false_child_question_value { get; set; }
}
public class Question : ofm_question
{

    [property: JsonPropertyName("survey.ofm_version")]
    public string? surveyVersion { get; set; }
    [property: JsonPropertyName("survey.statecode")]
    public int? surveyStatecode { get; set; }
}

public record D365Reporting
{
    public string? msfp_name { get; set; }
    public Guid msfp_projectid { get; set; }
    [property: JsonPropertyName("questions.msfp_questiontype")]
    public int QuestionType { get; set; }

    [property: JsonPropertyName("questions.msfp_questionid")]
    public Guid QuestionId { get; set; }

    [property: JsonPropertyName("questions.msfp_choicetype")]
    public int QuestionChoiceType { get; set; }

    [property: JsonPropertyName("questions.msfp_questionchoices")]
    public string QuestionChoices { get; set; }

    [property: JsonPropertyName("questions.msfp_questiontext")]
    public string QuestionText { get; set; }

    [property: JsonPropertyName("questions.msfp_name")]
    public string QuestionName { get; set; }

    [property: JsonPropertyName("questions.msfp_subtitle")]
    public string QuestionSubtitle { get; set; }

    [property: JsonPropertyName("questions.msfp_sourcesurveyidentifier")]
    public string QuestionSourceSurveyIdentifier { get; set; }

    [property: JsonPropertyName("questions.msfp_responserequired")]
    public bool QuestionresponseRequired { get; set; }

    [property: JsonPropertyName("questions.msfp_sourcequestionidentifier")]

    public string QuestionSourcequestionIdentifier { get; set; }

    [property: JsonPropertyName("questions.msfp_multiline")]

    public bool QuestionMultiline { get; set; }

    [property: JsonPropertyName("questions.msfp_survey")]
    public Guid QuestionSurveyId { get; set; }

    [property: JsonPropertyName("questions.msfp_sequence")]
    public int QuestionSequence { get; set; }

    [property: JsonPropertyName("section.msfp_project")]
    public Guid SectionProject { get; set; }

    [property: JsonPropertyName("section.msfp_surveyid")]
    public Guid SectionSurveyId { get; set; }

    [property: JsonPropertyName("section.msfp_name")]
    public string CVSectionName { get; set; }

    [property: JsonPropertyName("section.msfp_sourcesurveyidentifier")]
    public string SectionSourceSurveyIdentifier { get; set; }
    public int? OrderNumber { get; set; }
    public string? SectionName { get; set; }

}

public class D365FiscalYear : ofm_fiscal_year
{
    public new string ofm_financial_year { get; set; } = string.Empty;
}

public class ExpenseApplication : ofm_expense
{
    public new decimal? ofm_amount { get; set; }
}

public class D365PaymentLine : ofm_payment
{
    public new decimal? ofm_amount { get; set; }
    public new Facility? ofm_facility { get; set; }
    public new Guid? _ofm_organization_value { get; set; }
    public new Application? ofm_application { get; set; }
    public new Funding? ofm_funding { get; set; }
    public new D365FiscalYear? ofm_fiscal_year { get; set; }
    public new string ofm_invoice_number { get; set; } = string.Empty;
    public new string ofm_siteid { get; set; } = string.Empty;
    public new string ofm_supplierid { get; set; } = string.Empty;
    public new int? ofm_payment_method { get; set; }
    public string _ofm_regardingid_value { get; set; } = string.Empty; // ToDo: a workaround for polymorphic lookup
   
}

public class PaymentLine
{
    [Required(ErrorMessage = "Amount cannot be null.")]
    public new decimal? ofm_amount { get; set; }

    [Required(ErrorMessage = "A facility is mandatory, and its name must be provided.")]
    [JsonPropertyName("ofm_facility.name")]
    public new string ofm_facility { get; set; }

     [Required(ErrorMessage = "Funding is required.")]
    public new Guid? _ofm_funding_value { get; set; }

    [Required(ErrorMessage = "Fiscal year is mandatory.")]
    [JsonPropertyName("ofm_fiscal_year.ofm_financial_year")]
    public new string? ofm_financial_year { get; set; }

    [Required(ErrorMessage = "Invoice number is required.")]
    public new string? ofm_invoice_number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site cannot be blank.")]
    public new string? ofm_siteid { get; set; } = string.Empty;

    [Required(ErrorMessage = "Supplier details is missing.")]
    public new string? ofm_supplierid { get; set; } = string.Empty;

    [Required(ErrorMessage = "Payment method is mandatory.")]
    public new int? ofm_payment_method { get; set; }

   [JsonPropertyName("Ack.ofm_cohortid")]
    [Required(ErrorMessage = "Cohort cannot be blank on funding, and a corresponding acknowledgement number is required.")]
    public string? ofm_cohort { get; set; } = string.Empty;

    [Required(ErrorMessage = "Line number is required.")]
    public new int? ofm_invoice_line_number { get; set; }

    [Required(ErrorMessage = "Effective date should be today date.")]
    public new DateTime? ofm_effective_date { get; set; }

    [Required(ErrorMessage = "Invoice date should be today date.")]
    public new DateTime? ofm_invoice_date { get; set; }

    [Required(ErrorMessage = "Invoice recieved date should be today date.")]
    public new DateTime? ofm_invoice_received_date { get; set; }

    [Required(ErrorMessage = "Organization detail is required.")]
    public new Guid? _ofm_organization_value { get; set; }

    [Required(ErrorMessage = "Payment type is required.")]
    public new int? ofm_payment_type { get; set; }
}

public class ProviderStaff
{
    [JsonPropertyName("ofm_initials")]
    public string Initials { get; set; }

    [JsonPropertyName("ofm_certificate_number")]
    public string CertificateNumber { get; set; }

    [JsonPropertyName("application.ofm_application")]
    public string Name { get; set; }

    [property: JsonPropertyName("application.ofm_contact")]
    public Guid ProviderId { get; set; }

    [property: JsonPropertyName("application.ofm_contact@OData.Community.Display.V1.FormattedValue")]
    public string ProviderName { get; set; }

    [property: JsonPropertyName("report.ofm_contact")]
    public Guid ProviderId_Report { get { return ProviderId; } set { ProviderId = value; } }

    [property: JsonPropertyName("report.ofm_contact@OData.Community.Display.V1.FormattedValue")]
    public string ProviderName_Report { get { return ProviderName; } set { ProviderName = value; } }

    [property: JsonPropertyName("facility.ofm_primarycontact")]
    public Guid FacilityContactId { get; set; }

    [property: JsonPropertyName("report.ofm_name")]
    public string ProviderReport_Name { get { return Name; } set { Name = value; } }

    [JsonPropertyName("facility.ofm_primarycontact@OData.Community.Display.V1.FormattedValue")]
    public string FacilityContact_Name { get; set; }

    [JsonPropertyName("application.ofm_facility@OData.Community.Display.V1.FormattedValue")]
    public string Facility_Name { get; set; }

    [property: JsonPropertyName("report.ofm_facility@OData.Community.Display.V1.FormattedValue")]
    public string Facility_Name_Report { get { return Facility_Name; } set { Facility_Name = value; } }
}

public class TopUp : ofm_top_up_fund
{
    public new decimal? ofm_programming_amount { get; set; }
    public Funding? ofm_funding { get; set; }
    public string ofm_funding_number { get; set; }
}

public class User 
{
       public Guid systemuserid { get; set; }
    public string internalemailaddress { get; set; }
}
public class QuestionResponse : ofm_question_response
{
    [property: JsonPropertyName("question.ofm_question_id")]
    public string ofm_question_qid { get; set; }
    [property: JsonPropertyName("header.ofm_question_id")]
    public string ofm_header_qid { get; set; }
}

public class LicenceDetail
{
    [JsonPropertyName("Total_Enrolled_Spaces")]
    public int Total_Enrolled_Spaces { get; set; }

    [JsonPropertyName("Total_Licenced_Spaces")]
    public int Total_Licenced_Spaces { get; set; }

    [JsonPropertyName("Total_Operational_Spaces")]
    public int Total_Operational_Spaces { get; set; }

    [JsonPropertyName("Total_Under_Three")]
    public decimal Total_Under_Three { get; set; }

    [JsonPropertyName("Total_Three_to_Five")]
    public decimal Total_Three_to_Five { get; set; }

    [property: JsonPropertyName("type")]
    public int Type { get; set; }

    [property: JsonPropertyName("type@OData.Community.Display.V1.FormattedValue")]
    public string TypeName { get; set; } = string.Empty;
}

#region External Parameters

#endregion