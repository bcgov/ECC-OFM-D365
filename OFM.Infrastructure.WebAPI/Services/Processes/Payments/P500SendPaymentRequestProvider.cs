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
using System.Collections.Generic;
using System.IO;
using System.Transactions;

namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfile;

public class P500SendPaymentRequestProvider : ID365ProcessProvider
{

    private readonly BCCASApi _BCCASApi;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P500SendPaymentRequestProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _BCCASApi = bccasApiSettings.Value.BCCASApi;
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
        int supplierlength = 9;
        int invoicelength = 50;
        int polength = 20;
         int remitcodelength = 4;
        int termslength = 50;
        int oracleinvlength = 30;
        int distributionAcklength = 50;
        int descriptionlength = 60;
        int linedescriptionlength = 55;       
        int distributionsupplierlength = 30;
        int flowolength = 110;       
        string sspace = null;
        int transactionCount = 250;
        string source = "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{messageVersionNumber}}{{delimiter}}\n"+ "{{#each transaction}}{{this.feederNumber}}{{this.batchType}}{{this.headertransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.PONumber}}{{this.invoiceType}}{{this.invoiceDate}}{{this.payGroupLookup}}{{this.remittanceCode}}{{this.grossInvoiceAmount}}{{this.CAD}}{{this.invoiceDate}}{{this.termsName}}{{this.description}}{{this.goodsDate}}{{this.invoiceDate}}{{this.oracleBatchName}}{{this.SIN}}{{this.payflag}}{{this.flow}}{{this.delimiter}}\n" +
                       "{{this.feederNumber}}{{this.batchType}}{{this.linetransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.invoiceLineNumber}}{{this.committmentLine}}{{this.lineAmount}}{{this.lineCode}}{{this.distributionACK}}{{this.lineDescription}}{{this.invoiceDate}}{{this.quantity}}{{this.unitPrice}}{{this.space}}{{this.distributionSupplierNumber}}{{this.flow}}{{this.delimiter}}\n" +
                       "{{this.feederNumber}}{{this.batchType}}{{this.trailertransactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{this.controlCount}}{{this.controlAmount}}{{this.delimiter}}\n{{/each}}";

        #region commentedcode
        //string subdata = "{{this.feederNumber}}{{this.batchType}}{{this.headertransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.PONumber}}{{this.invoiceType}}{{this.invoiceDate}}{{this.payGroupLookup}}{{this.remittanceCode}}{{this.grossInvoiceAmount}}{{this.CAD}}{{this.invoiceDate}}{this.{oraclebatch}}{{this.SIN}}{{this.payflag}}{{this.delimiter}}\n" +
        //    "{{this.feederNumber}}{{this.batchType}}{{this.linetransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.invoiceLineNumber}}{{this.committmentLine}}{{this.lineAmount}}{{this.lineCode}}{{this.distributionACK}}{{this.description}}{{this.goodsDate}}{{this.invoiceDate}}{{this.quantity}}{{this.unitPrice}}{{this.space}}{{this.distributionSupplierNumber}}{{this.flow}}{{this.delimiter}}\n" +
        //    "{{this.feederNumber}}{{this.batchType}}{{this.trailertransactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{ths.ControlCount}}{{this.controlAmount}}{{this.delimiter}}";
        //string partialSource = "{{transaction}}";
        ////string source = "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{messageVersionNumber}}{{delimiter}}\n" +
        //                      "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{supplierNumber}}{{supplierSiteNumber}}FY1920AE125552                                                        {{invoiceType}}{{invoiceDate}}{{payGroupLookup}}{{remittanceCode}}{{grossInvoiceAmount}}{{CAD}}{{invoiceDate}}Immediate                                         Top-up CALP 2019-20                                                 20191115AE20NOVFSK03                           N                                                                                                                                             \n" +
        //                      "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{supplierNumber}}{{supplierSiteNumber}}FY1920AE125552                                    {{invoiceLineNumber}}0000000000009000.00D0191121118608776611304120000000000                Top-up CALP 2019-20                                    201911150000000.00000000000000.00                                                                                                                                                                   078766                                                                                                                                                                                               \n" +
        //                      "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{ControlCount}}{{controlAmount}}{{delimiter}}";

        //string[] subsource = subdata.Split("}}");
        //foreach (string s in subsource.Distinct())
        //{
        //    string partialSourceVar = s.Replace("{{", "");
        //    if (!string.IsNullOrEmpty(partialSourceVar))
        //        Handlebars.RegisterTemplate(partialSourceVar, partialSource);

        //}
        // Handlebars.RegisterTemplate("feederNumber", partialSource);
        // Handlebars.RegisterTemplate("batchType", partialSource);
        #endregion

        var template = Handlebars.Compile(source);
       
        var data = new
            {
                feederNumber = _BCCASApi.APInboxParam.feederNumber,
                batchType = _BCCASApi.APInboxParam.batchType,
                delimiter = _BCCASApi.APInboxParam.delimiter,
                transactionType = _BCCASApi.APInboxParam.transactionType,
                cGIBatchNumber = _BCCASApi.APInboxParam.cGIBatchNumber,
                messageVersionNumber = _BCCASApi.APInboxParam.messageVersionNumber,
                fiscalYear = _BCCASApi.APInboxParam.fiscalYear,
                transaction = new[] {
           new {
            feederNumber = _BCCASApi.APInboxParam.feederNumber,
            headertransactionType=_BCCASApi.APInboxParam.headertransactionType,
            linetransactionType= _BCCASApi.APInboxParam.linetransactionType,
            trailertransactionType= _BCCASApi.APInboxParam.trailertransactionType,
            batchType = _BCCASApi.APInboxParam.batchType,
            delimiter = _BCCASApi.APInboxParam.delimiter,
            transactionType = _BCCASApi.APInboxParam.transactionType,
            cGIBatchNumber = _BCCASApi.APInboxParam.cGIBatchNumber,
            messageVersionNumber = _BCCASApi.APInboxParam.messageVersionNumber,
            supplierNumber = _BCCASApi.APInboxParam.supplierNumber.PadRight(supplierlength).Substring(0, supplierlength),       // "078766ABH",
            supplierSiteNumber = _BCCASApi.APInboxParam.supplierSiteNumber,
            invoiceNumber = _BCCASApi.APInboxParam.invoiceNumber.PadRight(invoicelength).Substring(0, invoicelength),
            PONumber = sspace == null ? new string(' ', polength) : source.PadRight(polength).Substring(0, polength),
            invoiceType = _BCCASApi.APInboxParam.invoiceType,
            invoiceDate = DateTime.UtcNow.ToString("yyyyMMdd"),
            payGroupLookup = _BCCASApi.APInboxParam.payGroupLookup,
            remittanceCode = _BCCASApi.APInboxParam.remittanceCode.PadRight(remitcodelength).Substring(0, remitcodelength), // for payment stub it is 00 always.
            grossInvoiceAmount = _BCCASApi.APInboxParam.grossInvoiceAmount, // invoice amount come from OFM total base value.
            CAD = _BCCASApi.APInboxParam.CAD,
            termsName = _BCCASApi.APInboxParam.termsName.PadRight(termslength).Substring(0,termslength),
            description = _BCCASApi.APInboxParam.description.PadRight(descriptionlength).Substring(0, descriptionlength),
            goodsDate= sspace == null ? new string(' ', 8) : source.PadRight(8).Substring(0, 8),
            oracleBatchName = _BCCASApi.APInboxParam.oracleBatchName.PadRight(oracleinvlength).Substring(0,oracleinvlength),
           SIN= sspace == null ? new string(' ', 9) : source.PadRight(8).Substring(0, 9),
            payflag = _BCCASApi.APInboxParam.payflag,
            quantity=_BCCASApi.APInboxParam.quantity,
            flow= sspace == null ? new string(' ', flowolength) : source.PadRight(flowolength).Substring(0, flowolength),
            invoiceLineNumber = _BCCASApi.APInboxParam.invoiceLineNumber,
            committmentLine = _BCCASApi.APInboxParam.committmentLine,
            lineAmount = _BCCASApi.APInboxParam.lineAmount,
            lineCode = _BCCASApi.APInboxParam.lineCode,
            distributionACK = _BCCASApi.APInboxParam.distributionACK.PadRight(distributionAcklength).Substring(0, distributionAcklength),
            lineDescription = _BCCASApi.APInboxParam.description.PadRight(linedescriptionlength).Substring(0, linedescriptionlength),
            distributionSupplierNumber = _BCCASApi.APInboxParam.distributionSupplierNumber.PadRight(distributionsupplierlength).Substring(0,distributionsupplierlength),
            fiscalYear = _BCCASApi.APInboxParam.fiscalYear,
            controlCount = _BCCASApi.APInboxParam.controlCount,
            controlAmount = _BCCASApi.APInboxParam.controlAmount,
            unitPrice= _BCCASApi.APInboxParam.unitPrice,
            space= sspace == null ? new string(' ', 163) : source.PadRight(163).Substring(0, 163)
           }

         }

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