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
using Newtonsoft.Json.Linq;
using Microsoft.Crm.Sdk.Messages;
using System;
using System.Linq;
using System.Reflection.Metadata;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text;
using System.Collections;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;
using System.Net.Http.Json;
using Microsoft.VisualBasic.FileIO;


namespace OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfile;

public class P505ReadPaymentResponseProvider
    : ID365ProcessProvider
{

    private readonly BCCASApi _BCCASApi;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P505ReadPaymentResponseProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _BCCASApi = bccasApiSettings.Value.BCCASApi;
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Payment.GetPaymentResponseId;
    public string ProcessName => Setup.Process.Payment.GetPaymentResponseName;

    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_payment_file_exchange">
                        <attribute name="ofm_payment_file_exchangeid" />
                        <attribute name="ofm_name" />
                        <attribute name="createdon" />
                        <order attribute="ofm_name" descending="false" />
                        <filter type="and">
                          <condition attribute="ofm_payment_file_exchangeid" operator="eq" value="9dea6f53-1af1-ee11-a1fe-000d3a0a18e7" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payment_file_exchanges?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P505ReadPaymentResponseProvider));

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
    public string ToFinancialYear(DateTime dateTime)
    {
        return (dateTime.Month >= 4 ? dateTime.AddYears(1).ToString("yyyy") : dateTime.ToString("yyyy"));
    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        List<APInboxParam> transactions = new List<APInboxParam>();
        List<List<APInboxParam>> transactionlist = new List<List<APInboxParam>>();
        List<JsonObject> paymentdocumentsResult = new() { };
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
        int transactionCount = 5;
        string cGIBatchNumber;
        string _fiscalyear = ToFinancialYear(DateTime.UtcNow);

        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetData();
        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<Payment_File_Exchange>>(localData.Data.ToString());

        HttpResponseMessage response1 = await _d365webapiservice.GetDocumentRequestAsync(_appUserService.AZPortalAppUser, "ofm_payment_file_exchanges", new Guid(serializedData[0].ofm_payment_file_exchangeid));
            if (!response1.IsSuccessStatusCode)
            {
                var responseBody = await response1.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to upload file in the payment file exchange with  the server error {responseBody}", responseBody.CleanLog());
                 return await Task.FromResult<JsonObject>(new JsonObject() { });

            }
            byte[] file1 = response1.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            File.WriteAllBytes("output.txt", file1);
        int l = 0;List<string> parsedata = new List<string>();
        string datajson = System.Text.Encoding.UTF8.GetString(file1);

        //foreach (string line in File.ReadLines(@"output.txt"))
        //{
        //    if (line.Length("episode") & line.Contains("2006"))
        //    {
        //        Console.WriteLine(line);
        //    }
        //}
        try
        {
            StreamReader reader = File.OpenText("output.txt"); string line;
            while ((line = (string)reader.ReadLine()) != null)
            {
               string[] items = line.Split('\t');
                string test = items[0];
                TextReader sr = new StringReader(test);
                TextFieldParser parser = new TextFieldParser(sr);
                parser.TextFieldType = FieldType.FixedWidth;
                if (l == 0 * 1) parser.SetFieldWidths(28);
                else if (l == 1 * 1) parser.SetFieldWidths(11, 150, 3);//, 428, 150, 3, 538, 150, 58, 150, 3);
                else if (l == 2) parser.SetFieldWidths();
                while (!parser.EndOfData)
                {
                    //Processing row
                    string[] fields = parser.ReadFields();
                    parsedata.AddRange(fields);

                    
                }

                parser.Close();
                l++;

            }

        }
        catch (Exception ex)
        {
        }

        var context = JsonConvert.SerializeObject(datajson);

        #region Step 1: Handlebars format to generate Inbox data
        string source = "{{#each transaction}}{{this.feederNumber}}{{this.batchType}}{{this.transactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{this.messageVersionNumber}}{{this.delimiter}}\n" + "{{this.feederNumber}}{{this.batchType}}{{this.headertransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.PONumber}}{{this.invoiceType}}{{this.invoiceDate}}{{this.payGroupLookup}}{{this.remittanceCode}}{{this.grossInvoiceAmount}}{{this.CAD}}{{this.invoiceDate}}{{this.termsName}}{{this.description}}{{this.goodsDate}}{{this.invoiceDate}}{{this.oracleBatchName}}{{this.SIN}}{{this.payflag}}{{this.flow}}{{this.delimiter}}\n" +
                       "{{this.feederNumber}}{{this.batchType}}{{this.linetransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.invoiceLineNumber}}{{this.committmentLine}}{{this.lineAmount}}{{this.lineCode}}{{this.distributionACK}}{{this.lineDescription}}{{this.invoiceDate}}{{this.quantity}}{{this.unitPrice}}{{this.space}}{{this.distributionSupplierNumber}}{{this.flow}}{{this.delimiter}}\n" +
                       "{{this.feederNumber}}{{this.batchType}}{{this.trailertransactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{this.controlCount}}{{this.controlAmount}}{{this.delimiter}}\n{{/each}}";


        var template = Handlebars.Compile(source);


        for (int i = 0; i < 1; i++)
        {

            transactions.Add(new APInboxParam
            {
                payflag = _BCCASApi.APInboxParam.payflag,
                fiscalYear = _fiscalyear,
                feederNumber = _BCCASApi.APInboxParam.feederNumber,
                headertransactionType = _BCCASApi.APInboxParam.headertransactionType,
                linetransactionType = _BCCASApi.APInboxParam.linetransactionType,
                batchType = _BCCASApi.APInboxParam.batchType,
                delimiter = _BCCASApi.APInboxParam.delimiter,
                transactionType = _BCCASApi.APInboxParam.transactionType,
               // cGIBatchNumber = cGIBatchNumber,
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
                termsName = _BCCASApi.APInboxParam.termsName.PadRight(termslength).Substring(0, termslength),
                description = _BCCASApi.APInboxParam.description.PadRight(descriptionlength).Substring(0, descriptionlength),
                goodsDate = sspace == null ? new string(' ', 8) : source.PadRight(8).Substring(0, 8),
                oracleBatchName = _BCCASApi.APInboxParam.oracleBatchName.PadRight(oracleinvlength).Substring(0, oracleinvlength),
                SIN = sspace == null ? new string(' ', 9) : source.PadRight(8).Substring(0, 9),
                quantity = _BCCASApi.APInboxParam.quantity,
                flow = sspace == null ? new string(' ', flowolength) : source.PadRight(flowolength).Substring(0, flowolength),
                invoiceLineNumber = _BCCASApi.APInboxParam.invoiceLineNumber,
                committmentLine = _BCCASApi.APInboxParam.committmentLine,
                lineAmount = _BCCASApi.APInboxParam.lineAmount,
                lineCode = _BCCASApi.APInboxParam.lineCode,
                distributionACK = _BCCASApi.APInboxParam.distributionACK.PadRight(distributionAcklength).Substring(0, distributionAcklength),
                lineDescription = _BCCASApi.APInboxParam.description.PadRight(linedescriptionlength).Substring(0, linedescriptionlength),
                distributionSupplierNumber = _BCCASApi.APInboxParam.distributionSupplierNumber.PadRight(distributionsupplierlength).Substring(0, distributionsupplierlength),
                space = sspace == null ? new string(' ', 163) : source.PadRight(163).Substring(0, 163),
                controlCount = _BCCASApi.APInboxParam.controlCount,
                controlAmount = _BCCASApi.APInboxParam.controlAmount,
                unitPrice = _BCCASApi.APInboxParam.unitPrice,
                trailertransactionType = _BCCASApi.APInboxParam.trailertransactionType,
            });
          //  cGIBatchNumber = ((Convert.ToInt32(cGIBatchNumber)) + 1).ToString("D9");
        }

        // break transaction list into multiple list if it contains more than 250 transaction
        transactionlist = transactions
         .Select((x, i) => new { Index = i, Value = x })
         .GroupBy(x => x.Index / transactionCount)
         .Select(x => x.Select(v => v.Value).ToList())
         .ToList();
        #endregion

        #region Step 2: Generate and process inbox file in CRM
        // for each set of transaction create and upload inbox file in payment file exchange
        foreach (List<APInboxParam> list in transactionlist)
        {
            var data = new
            {
                transaction = list
            };
            var result = template(data);

            var filename = ("INBOX.F" + _BCCASApi.APInboxParam.feederNumber + "." + DateTime.UtcNow.ToString("yyyyMMddHHMMss"));


          
        }
        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;

    }


}














