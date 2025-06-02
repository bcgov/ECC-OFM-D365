namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles
{
    public interface IBcRegistryService
    {
     
        Task<HttpResponseMessage> GetRegistryDataAsync(string organizationId, string legalName, string incorporationNumber);
        Task<HttpResponseMessage> GetDBADataAsync(string incorporationNumber);

    }
}
