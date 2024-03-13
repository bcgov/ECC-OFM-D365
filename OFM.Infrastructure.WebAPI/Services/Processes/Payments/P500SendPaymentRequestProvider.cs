using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Interfaces;
using Newtonsoft.Json;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfile;

public class P500SendPaymentRequestProvider : ID365ProcessProvider
{
    private readonly ProcessSettings _processSettings;
    private readonly BCRegistrySettings _BCRegistrySettings;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P500SendPaymentRequestProvider(IOptionsSnapshot<ProcessSettings> processSettings, IOptionsSnapshot<ExternalServices> ApiKeyBCRegistry, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _processSettings = processSettings.Value;
        _BCRegistrySettings = ApiKeyBCRegistry.Value.BCRegistryApi;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Payment.SendPaymentRequestId;
    public string ProcessName => Setup.Process.Payment.SendPaymentRequestName;

    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch>
                      <entity name="ofm_application">
                        <attribute name="ofm_application_type" />
                        <attribute name="ofm_applicationid" />
                        <attribute name="ofm_contact" />
                        <filter>
                          <condition attribute="statuscode" operator="eq" value="5" />
                        </filter>
                        <link-entity name="ofm_funding" from="ofm_application" to="ofm_applicationid">
                          <attribute name="ofm_envelope_grand_total" />
                          <attribute name="ofm_fundingid" />
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_applications?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
   
    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P500SendPaymentRequestProvider));

        if (_data is null)
        {
            var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestUri, isProcess: true);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to query the requests with the server error {responseBody}", responseBody);

                return await Task.FromResult(new ProcessData(string.Empty));
            }

            var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

            JsonNode d365Result = string.Empty;
            if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
            {
                if (currentValue?.AsArray().Count == 0)
                {
                    _logger.LogInformation(CustomLogEvent.Process, "No records found");
                }
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);
    }
  

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
      

        var startTime = _timeProvider.GetTimestamp();
        string source = "{{feederNumber}}{{batchType}}{{transactionType}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{messageVersionNumber}}\n" +
                              "{{feederNumber}}{{batchType}}{{transactionType}}{{supplierNumber}}{{supplierSiteNumber}}FY1920AE125552                                                        {{invoiceType}}{{invoiceDate}}{{payGroupLookup}}{{remittanceCode}}{{grossInvoiceAmount}}{{CAD}}{{invoiceDate}}Immediate                                         Top-up CALP 2019-20                                                 20191115AE20NOVFSK03                           N                                                                                                                                             \n" +
                              "{{feederNumber}}{{batchType}}{{transactionType}}{{supplierNumber}}{{supplierSiteNumber}}FY1920AE125552                                    {{invoiceLineNumber}}0000000000009000.00D0191121118608776611304120000000000                Top-up CALP 2019-20                                    201911150000000.00000000000000.00                                                                                                                                                                   078766                                                                                                                                                                                               \n" +
                              "{{feederNumber}}{{batchType}}{{transactionType}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{ControlCount}}{{controlAmount}}";
        var template = Handlebars.Compile(source);

        var data = new
        {
            feederNumber = "f3540t",
            batchType = "AP",
            transactionType = "BH",
            cGIBatchNumber = "000100111",
            messageVersionNumber = "0001",
            supplierNumber = "078766ABH",
            supplierSiteNumber = "001",
            // dynamic invoice number from OFM CRM.
            invoiceNumber = "FY1920AE125552                                                        ",
            invoiceType = "ST",
            invoiceDate = "20240322", // this should be current date time
            payGroupLookup = "GEN EFT N",
            remittanceCode = "00  ", // for payment stub it is 00 always.
            grossInvoiceAmount = "000000009000.00", // invoice amount come from OFM total base value.
            currencyCode = "CAD",
           // defaultEffectiveDate = invoiceDate, // default effectiveDate is same as invoiceDAte.
            termsName = "Immediate",
            description = "                                         Top-up CALP 2019-20                                                 ",
           // invoiceReceivedDate = invoiceDate,
            oracleBatchName = "AE20NOVFSK03                           ",
            payAlone = "Y",
            invoiceLineNumber = "0001",
            lineAmount = "0000000000009000.00",
            lineCode = "D",
            distributionACK = "0191121118608776611304120000000000",
            distributionSupplierNumber = "078766   ",
            fiscalYear = "2025",
            ControlCount = "000000000000002",
            controlAmount = "000000009000.00"
    };

        var result = template(data);


        // Save the result to a text file
        File.WriteAllText("output.txt", result);

        Console.WriteLine("Template converted and saved to output.txt");

        string filePath = "example.txt";

        // Specify the character indices
        int startIndex = 5;
        int endIndex = 10;

        // Read the content of the file
        string fileContent;
        using (StreamReader reader = new StreamReader(filePath))
        {
            fileContent = reader.ReadToEnd();
        }

        // Extract characters based on indices
        string parsedData = fileContent.Substring(startIndex, endIndex - startIndex + 1);

        // Print the parsed data
        Console.WriteLine(parsedData);
        var localData = await GetData();

       var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<PaymentApplication>>(localData.Data.ToString());

        #region  Step 1: Create Payment Records for all approved applications.


        List<HttpRequestMessage> sendCreatePaymentRequests = [];
        serializedData?.ForEach(Application =>
        {
            sendCreatePaymentRequests.Add(new CreateRequest("ofm_payments",
                new JsonObject(){
                       // {"ofm_facility",Application._ofm_facility_value },
                        {"ofm_funding_amount", 6778}
                      //  {"ofm_application", Application.ofm_applicationid }
                }));
        });

        var sendPaymentBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, sendCreatePaymentRequests, new Guid(processParams.CallerObjectId.ToString()));

        if (sendPaymentBatchResult.Errors.Any())
        {
            var sendCreatePaymentError = ProcessResult.Failure(ProcessId, sendPaymentBatchResult.Errors, sendPaymentBatchResult.TotalProcessed, sendPaymentBatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to create payment with an error: {error}", JsonValue.Create(sendCreatePaymentError)!.ToString());

            return sendCreatePaymentError.SimpleProcessResult;
        }


        

            

       
        return sendPaymentBatchResult.SimpleBatchResult;
        #endregion

    }

 


  

    
}