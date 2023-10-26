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
		/// Gets a binding to the set of all <see cref="ECC.Core.DataContext.Task"/> entities.
		/// </summary>
		public System.Linq.IQueryable<ECC.Core.DataContext.Task> TaskSet
		{
			get
			{
				return this.CreateQuery<ECC.Core.DataContext.Task>();
			}
		}
	}
}
#pragma warning restore CS1591
