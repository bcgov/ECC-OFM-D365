#pragma warning disable CS1591
// Code Generated by DLaB.ModelBuilderExtensions
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
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Application"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Application> OfM_ApplicationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Application>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Assistance_Request"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Assistance_Request> OfM_Assistance_RequestSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Assistance_Request>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_BcEId_Facility"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_BcEId_Facility> OfM_BcEId_FacilitySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_BcEId_Facility>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Communication_Type"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Communication_Type> OfM_Communication_TypeSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Communication_Type>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Conversation"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Conversation> OfM_ConversationSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Conversation>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Document"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Document> OfM_DocumentSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Document>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Facility_Request"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Facility_Request> OfM_Facility_RequestSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Facility_Request>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Funding"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Funding> OfM_FundingSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Funding>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Licence"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Licence> OfM_LicenceSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Licence>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Licence_Detail"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Licence_Detail> OfM_Licence_DetailSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Licence_Detail>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.OfM_Request_Category"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.OfM_Request_Category> OfM_Request_CategorySet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.OfM_Request_Category>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.SystemUser"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.SystemUser> SystemUserSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.SystemUser>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Task"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Task> TaskSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Task>();
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
}
#pragma warning restore CS1591
