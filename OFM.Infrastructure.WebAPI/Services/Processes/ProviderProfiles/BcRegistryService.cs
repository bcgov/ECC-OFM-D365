using OFM.Infrastructure.WebAPI.Models;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles
{
   
    public class BcRegistryService : IBcRegistryService
    {
        private readonly BCRegistrySettings _BCRegistrySettings;
        private readonly HttpClient _httpClient;

      
        public BcRegistryService( BCRegistrySettings BCRegistrySettings)
        {
           _BCRegistrySettings = BCRegistrySettings;
            _httpClient = new HttpClient();
        }

        public async Task<HttpResponseMessage> GetRegistryDataAsync(string organizationId, string legalName, string incorporationNumber)
        {
            
                string? queryValue = !string.IsNullOrEmpty(incorporationNumber) ? incorporationNumber.Trim() : legalName.Trim();
                var legalType = "A,B,BC,BEN,C,CC,CCC,CEM,CP,CS,CUL,EPR,FI,FOR,GP,LIC,LIB,LL,LLC,LP,MF,PA,PAR,PFS,QA,QB,QC,QD,QE,REG,RLY,S,SB,SP,T,TMY,ULC,UQA,UQB,UQC,UQD,UQE,XCP,XL,XP,XS";
                var status = "active";

                var queryString = $"?query=value:{queryValue}::identifier:::bn:::name:" +
                                  $"&categories=legalType:{legalType}::status:{status}";

                var path = $"{_BCRegistrySettings.RegistrySearchUrl}{queryString}";

                var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add(_BCRegistrySettings.AccoutIdName, _BCRegistrySettings.AccoutIdValue);
                request.Headers.Add(_BCRegistrySettings.KeyName, _BCRegistrySettings.KeyValue);

                var retryPolicy = Policy
                          .HandleResult<HttpResponseMessage>(response =>
                              response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || // HTTP 429
                              response.Content.ReadAsStringAsync().Result.Contains("Rate exceeded")) // Custom check for "rate exceeded"
                          .WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(30)); // Retry after 30 seconds




                return await retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(request));
        
            //return await _httpClient.SendAsync(request);
            //return await response.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> GetDBADataAsync( string incorporationNumber)
        {

            var path = $"{_BCRegistrySettings.BusinessSearchUrl}" + $"/{incorporationNumber}";

            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add(_BCRegistrySettings.AccoutIdName, _BCRegistrySettings.AccoutIdValue);
            request.Headers.Add(_BCRegistrySettings.KeyName, _BCRegistrySettings.KeyValue);

             var retryPolicy = Policy
                      .HandleResult<HttpResponseMessage>(response =>
                          response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || // HTTP 429
                          response.Content.ReadAsStringAsync().Result.Contains("Rate exceeded")) // Custom check for "rate exceeded"
                      .WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(30)); // Retry after 30 seconds




            return await retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(request));

            //return await _httpClient.SendAsync(request);
            //return await response.Content.ReadAsStringAsync();
        }

    }

}
