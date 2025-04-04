#pragma warning disable CS1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: Microsoft.Xrm.Sdk.Client.ProxyTypesAssemblyAttribute()]

namespace ECC.Core.DataContext
{
	
	
	/// <summary>
	/// Represents a source of entities bound to a Dataverse service. It tracks and manages changes made to the retrieved entities.
	/// </summary>
	public partial class DataverseContext : Microsoft.Xrm.Sdk.Client.OrganizationServiceContext
	{
		
		/// <summary>
		/// Constructor.
		/// </summary>
		public DataverseContext(Microsoft.Xrm.Sdk.IOrganizationService service) : 
				base(service)
		{
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Account"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Account> AccountSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Account>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Contact"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Contact> ContactSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Contact>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Email"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Email> EmailSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Email>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.msfp_project"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.msfp_project> msfp_projectSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.msfp_project>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.msfp_question"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.msfp_question> msfp_questionSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.msfp_question>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.msfp_survey"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.msfp_survey> msfp_surveySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.msfp_survey>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_ack_codes"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_ack_codes> ofm_ack_codesSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_ack_codes>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_allowance"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_allowance> ofm_allowanceSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_allowance>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_application"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_application> ofm_applicationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_application>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_assistance_request"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_assistance_request> ofm_assistance_requestSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_assistance_request>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_bceid_facility"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_bceid_facility> ofm_bceid_facilitySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_bceid_facility>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_cclr_ratio"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_cclr_ratio> ofm_cclr_ratioSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_cclr_ratio>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_communication_type"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_communication_type> ofm_communication_typeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_communication_type>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_conversation"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_conversation> ofm_conversationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_conversation>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_data_import"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_data_import> ofm_data_importSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_data_import>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_document"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_document> ofm_documentSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_document>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_employee_certificate"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_employee_certificate> ofm_employee_certificateSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_employee_certificate>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_employee_certificate_status"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_employee_certificate_status> ofm_employee_certificate_statusSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_employee_certificate_status>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_expense"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_expense> ofm_expenseSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_expense>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_facility_intake"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_facility_intake> ofm_facility_intakeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_facility_intake>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_facility_request"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_facility_request> ofm_facility_requestSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_facility_request>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_fiscal_year"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_fiscal_year> ofm_fiscal_yearSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_fiscal_year>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_funding"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_funding> ofm_fundingSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_funding>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_funding_rate"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_funding_rate> ofm_funding_rateSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_funding_rate>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_intake"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_intake> ofm_intakeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_intake>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_integration_log"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_integration_log> ofm_integration_logSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_integration_log>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_licence"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_licence> ofm_licenceSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_licence>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_licence_detail"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_licence_detail> ofm_licence_detailSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_licence_detail>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_payment"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_payment> ofm_paymentSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_payment>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_payment_file_exchange"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_payment_file_exchange> ofm_payment_file_exchangeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_payment_file_exchange>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_payment_request"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_payment_request> ofm_payment_requestSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_payment_request>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_pcm_review"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_pcm_review> ofm_pcm_reviewSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_pcm_review>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_portal_permission"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_portal_permission> ofm_portal_permissionSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_portal_permission>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_portal_privilege"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_portal_privilege> ofm_portal_privilegeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_portal_privilege>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_portal_role"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_portal_role> ofm_portal_roleSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_portal_role>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_progress_tracker"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_progress_tracker> ofm_progress_trackerSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_progress_tracker>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_provider_employee"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_provider_employee> ofm_provider_employeeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_provider_employee>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_question"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_question> ofm_questionSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_question>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_question_business_rule"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_question_business_rule> ofm_question_business_ruleSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_question_business_rule>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_question_response"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_question_response> ofm_question_responseSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_question_response>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_rate_schedule"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_rate_schedule> ofm_rate_scheduleSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_rate_schedule>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_reminder"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_reminder> ofm_reminderSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_reminder>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_request_category"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_request_category> ofm_request_categorySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_request_category>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_section"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_section> ofm_sectionSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_section>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_space_allocation"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_space_allocation> ofm_space_allocationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_space_allocation>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_standing_history"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_standing_history> ofm_standing_historySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_standing_history>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_stat_holiday"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_stat_holiday> ofm_stat_holidaySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_stat_holiday>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_subcategory"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_subcategory> ofm_subcategorySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_subcategory>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_supplementary_schedule"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_supplementary_schedule> ofm_supplementary_scheduleSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_supplementary_schedule>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_survey"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_survey> ofm_surveySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_survey>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_survey_response"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_survey_response> ofm_survey_responseSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_survey_response>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_system_configuration"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_system_configuration> ofm_system_configurationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_system_configuration>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_system_message"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_system_message> ofm_system_messageSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_system_message>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.ofm_top_up_fund"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.ofm_top_up_fund> ofm_top_up_fundSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.ofm_top_up_fund>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Team"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Team> TeamSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Team>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.TimeZoneDefinition"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.TimeZoneDefinition> TimeZoneDefinitionSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.TimeZoneDefinition>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.UserSettings"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.UserSettings> UserSettingsSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.UserSettings>();
			}
		}
	}
	
	/// <summary>
	/// Attribute to handle storing the OptionSet's Metadata.
	/// </summary>
	[System.AttributeUsageAttribute(System.AttributeTargets.Field)]
	public sealed class OptionSetMetadataAttribute : System.Attribute
	{
		
		private object[] _nameObjects;
		
		private System.Collections.Generic.Dictionary<int, string> _names;
		
		/// <summary>
		/// Color of the OptionSetValue.
		/// </summary>
		public string Color { get; set; }
		
		/// <summary>
		/// Description of the OptionSetValue.
		/// </summary>
		public string Description { get; set; }
		
		/// <summary>
		/// Display order index of the OptionSetValue.
		/// </summary>
		public int DisplayIndex { get; set; }
		
		/// <summary>
		/// External value of the OptionSetValue.
		/// </summary>
		public string ExternalValue { get; set; }
		
		/// <summary>
		/// Name of the OptionSetValue.
		/// </summary>
		public string Name { get; set; }
		
		/// <summary>
		/// Names of the OptionSetValue.
		/// </summary>
		public System.Collections.Generic.Dictionary<int, string> Names
		{
			get
			{
				return _names ?? (_names = CreateNames());
			} 
			set
			{
				_names = value;
				if (value == null)
				{
				    _nameObjects = new object[0];
				}
				else
				{
				    _nameObjects = null;
				}
			}
		}
		
		/// <summary>
		/// Initializes a new instance of the <see cref="OptionSetMetadataAttribute"/> class.
		/// </summary>
		/// <param name="name">Name of the value.</param>
		/// <param name="displayIndex">Display order index of the value.</param>
		/// <param name="color">Color of the value.</param>
		/// <param name="description">Description of the value.</param>
		/// <param name="externalValue">External value of the value.</param>
		/// <param name="names">Names of the value.</param>
		public OptionSetMetadataAttribute(string name, int displayIndex, string color = null, string description = null, string externalValue = null, params object[] names)
		{
			this.Color = color;
			this.Description = description;
			this._nameObjects = names;
			this.ExternalValue = externalValue;
			this.DisplayIndex = displayIndex;
			this.Name = name;
		}
		
		private System.Collections.Generic.Dictionary<int, string> CreateNames()
		{
			System.Collections.Generic.Dictionary<int, string> names = new System.Collections.Generic.Dictionary<int, string>();
			for (int i = 0; (i < _nameObjects.Length); i = (i + 2))
			{
				names.Add(((int)(_nameObjects[i])), ((string)(_nameObjects[(i + 1)])));
			}
			return names;
		}
	}
	
	/// <summary>
	/// Extension class to handle retrieving of OptionSetMetadataAttribute.
	/// </summary>
	public static class OptionSetExtension
	{
		
		/// <summary>
		/// Returns the OptionSetMetadataAttribute for the given enum value
		/// </summary>
		/// <typeparam name="T">OptionSet Enum Type</typeparam>
		/// <param name="value">Enum Value with OptionSetMetadataAttribute</param>
		public static OptionSetMetadataAttribute GetMetadata<T>(this T value)
			where T :  struct, System.IConvertible
		{
			System.Type enumType = typeof(T);
			if (!enumType.IsEnum)
			{
				throw new System.ArgumentException("T must be an enum!");
			}
			System.Reflection.MemberInfo[] members = enumType.GetMember(value.ToString());
			for (int i = 0; (i < members.Length); i++
			)
			{
				System.Attribute attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(members[i], typeof(OptionSetMetadataAttribute));
				if (attribute != null)
				{
					return ((OptionSetMetadataAttribute)(attribute));
				}
			}
			throw new System.ArgumentException("T must be an enum adorned with an OptionSetMetadataAttribute!");
		}
	}
}
#pragma warning restore CS1591
