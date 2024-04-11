using ECC.Core.DataContext;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using OFM.Infrastructure.WebAPI.Models;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IEmailRepository
{
    Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync();
}

public class EmailRepository(ID365AppUserService appUserService, ID365WebApiService service, ID365DataService dataService, ILoggerFactory loggerFactory) : IEmailRepository
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly ID365DataService _dataService = dataService;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;
    private Guid? _fundingId;

    #region Pre-Defined Queries

    private string CommunicationTypeRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = """
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_communication_type">
                                <attribute name="ofm_communication_typeid" />
                                <attribute name="ofm_communication_type_number" />
                                <attribute name="statecode" />
                                <filter>
                                  <condition attribute="statecode" operator="eq" value="0" />
                                </filter>
                              </entity>
                            </fetch>
                """;

            var requestUri = $"""
                              ofm_communication_types?fetchXml={WebUtility.UrlEncode(fetchXml)}
                              """;

            return requestUri;
        }
    }
    #endregion

    public async Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync()
    {
        var localdata = await _dataService.FetchDataAsync(CommunicationTypeRequestUri, "CommunicationTypes");
        var deserializedData = localdata.Data.Deserialize<List<D365CommunicationType>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!); ;
    }

   
}