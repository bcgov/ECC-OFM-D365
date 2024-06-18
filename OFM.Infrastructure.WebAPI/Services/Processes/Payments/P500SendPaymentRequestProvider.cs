using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using System.Text;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;
using ECC.Core.DataContext;
using System.Globalization;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P500SendPaymentRequestProvider : ID365ProcessProvider
{

    private readonly BCCASApi _BCCASApi;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private int _controlCount;
    private double _controlAmount;
    private int _oraclebatchnumber;
    private string _cGIBatchNumber;
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

    public Int16 ProcessId => Setup.Process.Payments.SendPaymentRequestId;
    public string ProcessName => Setup.Process.Payments.SendPaymentRequestName;

    public string RequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" count="1">
                      <entity name="ofm_payment_file_exchange">
                        <attribute name="ofm_payment_file_exchangeid" />
                        <attribute name="ofm_name" />
                        <attribute name="ofm_batch_number" />
                       <attribute name="ofm_oracle_batch_name" />
                         <order attribute="ofm_batch_number" descending="true" />
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payment_file_exchanges?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public string RequestPaymentLineUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_payment">
                        <attribute name="ofm_paymentid" />
                        <attribute name="ofm_name" />
                        <attribute name="createdon" />
                        <attribute name="ofm_amount" />
                         <attribute name="ofm_description" />
                        <attribute name="ofm_effective_date" />
                        <attribute name="ofm_fiscal_year" />
                        <attribute name="ofm_funding" />
                        <attribute name="ofm_invoice_line_number" />
                        <attribute name="owningbusinessunit" />
                        <attribute name="ofm_payment_type" />
                        <attribute name="ofm_remittance_message" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_invoice_number" />
                        <attribute name="ofm_application" />
                        <attribute name="ofm_siteid" />
                        <attribute name="ofm_payment_method" />
                        <attribute name="ofm_supplierid" />
                       <attribute name="ofm_invoice_received_date" />
                       <attribute name="ofm_invoice_date" />
                        <order attribute="ofm_name" descending="false" />
                     <filter type="and">
                    <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ApprovedforPayment}" />
                    <condition attribute="ofm_supplierid" operator="not-null" />
                    <condition attribute="ofm_siteid" operator="not-null" />
                    <condition attribute="ofm_payment_method" operator="not-null" />
                    <condition attribute="ofm_amount" operator="not-null" />
                    <condition attribute="ofm_invoice_date" operator="today" />
                      </filter>
                        <link-entity name="ofm_fiscal_year" from="ofm_fiscal_yearid" to="ofm_fiscal_year" visible="false" link-type="outer" alias="ofm_fiscal_year">
                      <attribute name="ofm_financial_year" />                      
                    </link-entity>
                    <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner" alias="ofm_application">
                      <attribute name="ofm_application" />
                    </link-entity>
                     <link-entity name="account" from="accountid" to="ofm_facility" visible="false" link-type="outer" alias="ofm_facility">
                       <attribute name="accountnumber" />                     
                     <attribute name="name" />
                    </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payments?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public async Task<ProcessData> GetDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P500SendPaymentRequestProvider));

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


        return await Task.FromResult(_data);


    }

    public async Task<ProcessData> GetPaymentLineData()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P500SendPaymentRequestProvider));


        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestPaymentLineUri, isProcess: true);
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


        return await Task.FromResult(_data);


    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var d = DateTime.Now;
        List<InvoiceHeader> invoiceHeaders = new List<InvoiceHeader>();
        List<List<InvoiceHeader>> headerList = new List<List<InvoiceHeader>>();
        var line = typeof(InvoiceLines);
        var header = typeof(InvoiceHeader);
        string result = string.Empty;
        string oracleBatchName;

        var paymentData = await GetPaymentLineData();
        List<D365PaymentLine> paymentserializedData = new List<D365PaymentLine>();
        try
        {

            paymentserializedData = System.Text.Json.JsonSerializer.Deserialize<List<D365PaymentLine>>(paymentData.Data.ToString());


            var grouppayment = paymentserializedData?.GroupBy(p => p.ofm_invoice_number).ToList();


            var _fiscalyear = paymentserializedData?.FirstOrDefault()?.ofm_financial_year;

            var localData = await GetDataAsync();
            var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<PaymentFileExchange>>(localData.Data.ToString());

            if (serializedData != null && serializedData[0].ofm_batch_number != null)
            {
                _oraclebatchnumber = Convert.ToInt32(serializedData[0].ofm_oracle_batch_name) + 1;
                _cGIBatchNumber = (Convert.ToInt32(serializedData[0].ofm_batch_number) + 1).ToString("D9").Substring(0, 9);
                oracleBatchName = _BCCASApi.clientCode + _fiscalyear?.Substring(2) + "OFM" + (_oraclebatchnumber).ToString("D5");
            }
            else
            {
                _cGIBatchNumber = _BCCASApi.cGIBatchNumber;
                oracleBatchName = _BCCASApi.clientCode + _fiscalyear?.Substring(2) + "OFM" + _BCCASApi.oracleBatchNumber;

            }

            #region Step 1: Handlebars format to generate Inbox data

            string source = "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{messageVersionNumber}}{{delimiter}}\n" + "{{#each InvoiceHeader}}{{this.feederNumber}}{{this.batchType}}{{this.headertransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.PONumber}}{{this.invoiceType}}{{this.invoiceDate}}{{this.payGroupLookup}}{{this.remittanceCode}}{{this.grossInvoiceAmount}}{{this.CAD}}{{this.invoiceDate}}{{this.termsName}}{{this.description}}{{this.goodsDate}}{{this.invoiceRecDate}}{{this.oracleBatchName}}{{this.SIN}}{{this.payflag}}{{this.flow}}{{this.delimiter}}\n" +
                            "{{#each InvoiceLines}}{{this.feederNumber}}{{this.batchType}}{{this.linetransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.invoiceLineNumber}}{{this.committmentLine}}{{this.lineAmount}}{{this.lineCode}}{{this.distributionACK}}{{this.lineDescription}}{{this.effectiveDate}}{{this.quantity}}{{this.unitPrice}}{{this.optionalData}}{{this.distributionSupplierNumber}}{{this.flow}}{{this.delimiter}}\n{{/each}}{{/each}}" +
                            "{{this.feederNumber}}{{this.batchType}}{{this.trailertransactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{this.controlCount}}{{this.controlAmount}}{{this.delimiter}}\n";

            var template = Handlebars.Compile(source);

            // add invoice header for each organization and invoice lines for each facility
            foreach (var headeritem in grouppayment)
            {
                var pay_method = (ecc_payment_method)headeritem.First().ofm_payment_method;
                double invoiceamount = 0.00;
                List<InvoiceLines> invoiceLines = new List<InvoiceLines>();

                foreach (var lineitem in headeritem.Select((item, i) => (item, i)))
                {
                   
                    invoiceamount = invoiceamount + Convert.ToDouble(lineitem.item.ofm_amount);//line amount should come from funding
                    var paytype = lineitem.item.ofm_payment_typename;
                    invoiceLines.Add(new InvoiceLines
                    {
                        feederNumber = _BCCASApi.feederNumber,// Static value:3540
                        batchType = _BCCASApi.batchType,//Static  value :AP
                        delimiter = _BCCASApi.delimiter,//Static value:\u001d
                        linetransactionType = _BCCASApi.InvoiceLines.linetransactionType,//Static value:IL for each line
                        invoiceNumber = lineitem.item.ofm_invoice_number.PadRight(line.FieldLength("invoiceNumber")),// Autogenerated and unique for supplier transaction
                        invoiceLineNumber = (lineitem.i + 1).ToString("D4"),// Incremented by 1 for each line in case for multiple lines
                        supplierNumber = lineitem.item.ofm_supplierid.PadRight(line.FieldLength("supplierNumber")),// Populate from Organization Supplier info
                        supplierSiteNumber = lineitem.item.ofm_siteid.PadLeft(line.FieldLength("supplierSiteNumber"), '0'),// Populate from Organization Supplier info
                        committmentLine = _BCCASApi.InvoiceLines.committmentLine,//Static value:0000
                        lineAmount = (lineitem.item.ofm_amount < 0 ? "-" : "") + Math.Abs(lineitem.item.ofm_amount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(line.FieldLength("lineAmount") - (lineitem.item.ofm_amount < 0 ? 1 : 0), '0'),// come from split funding amount per facility
                        lineCode = _BCCASApi.InvoiceLines.lineCode,//Static Value:D (debit)
                        distributionACK = _BCCASApi.InvoiceLines.distributionACK.PadRight(line.FieldLength("distributionACK")),// using test data shared by CAS,should be changed for prod
                        lineDescription = string.Concat(lineitem.item.ofm_application_number, " ", lineitem.item.ofm_payment_type).PadRight(line.FieldLength("lineDescription")), // Pouplate extra info from facility/funding amount
                        effectiveDate = lineitem.item.ofm_effective_date?.ToString("yyyyMMdd"), //2 days after invoice posting
                        quantity = _BCCASApi.InvoiceLines.quantity,//Static Value:0000000.00 not used by feeder
                        unitPrice = _BCCASApi.InvoiceLines.unitPrice,//Static Value:000000000000.00 not used by feeder
                        optionalData = string.Empty.PadRight(line.FieldLength("optionalData")),// PO ship to asset tracking values are set to blank as it is optional
                        distributionSupplierNumber = lineitem.item.ofm_supplierid.PadRight(line.FieldLength("distributionSupplierNumber")),// Supplier number from Organization
                        flow = string.Empty.PadRight(line.FieldLength("flow")), //can be use to pass additional info from facility or application
                    });

                    _controlCount++;
                }
                invoiceHeaders.Add(new InvoiceHeader
                {
                    feederNumber = _BCCASApi.feederNumber,// Static value:3540
                    batchType = _BCCASApi.batchType,//Static  value :AP
                    headertransactionType = _BCCASApi.InvoiceHeader.headertransactionType,//Static value:IH for each header
                    delimiter = _BCCASApi.delimiter,//Static value:\u001d
                    supplierNumber = headeritem.First().ofm_supplierid.PadRight(header.FieldLength("supplierNumber")),// Populate from Organization Supplier info
                    supplierSiteNumber = headeritem.First().ofm_siteid.PadLeft(header.FieldLength("supplierSiteNumber"), '0'),// Populate from Organization Supplier info
                    invoiceNumber = headeritem.First().ofm_invoice_number.PadRight(header.FieldLength("invoiceNumber")),// Autogenerated and unique for supplier transaction
                    PONumber = string.Empty.PadRight(header.FieldLength("PONumber")),// sending blank as not used by feeder
                    invoiceDate = headeritem.First().ofm_invoice_date?.ToString("yyyyMMdd"), // set to current date
                    invoiceType = _BCCASApi.InvoiceHeader.invoiceType,// static to ST (standard invoice)
                    payGroupLookup = string.Concat("GEN ", pay_method, " N"),//GEN CHQ N if using cheque or GEN EFT N if direct deposit
                    remittanceCode = _BCCASApi.InvoiceHeader.remittanceCode.PadRight(header.FieldLength("remittanceCode")), // for payment stub it is 00 always.
                    grossInvoiceAmount = (invoiceamount < 0 ? "-" : "") + Math.Abs(invoiceamount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(header.FieldLength("grossInvoiceAmount") - (invoiceamount < 0 ? 1 : 0), '0'), // invoice amount come from OFM total base value.
                    
                    CAD = _BCCASApi.InvoiceHeader.CAD,// static value :CAD
                    termsName = "Immediate".PadRight(header.FieldLength("termsName")),//setting it to immediate for successful testing, this needs to be dynamic going forward.
                    goodsDate = string.Empty.PadRight(header.FieldLength("goodsDate")),//optional field so set to null
                    invoiceRecDate = headeritem.First().ofm_invoice_received_date?.ToString("yyyyMMdd"),//ideally is is 4 days before current date
                    oracleBatchName = (_BCCASApi.clientCode + _fiscalyear?.Substring(2) + "OFM" + (_oraclebatchnumber).ToString("D5")).PadRight(header.FieldLength("oracleBatchName")),//6225OFM00001 incremented by 1 for each header
                    SIN = string.Empty.PadRight(header.FieldLength("SIN")), //optional field set to blank
                    payflag = _BCCASApi.InvoiceHeader.payflag,// Static value: Y (separate chq for each line)
                    description = string.Concat(headeritem.First().accountname).PadRight(header.FieldLength("description")),// can be used to pass extra info
                    flow = string.Empty.PadRight(header.FieldLength("flow")),// can be used to pass extra info
                    invoiceLines = invoiceLines

                });
                _controlAmount = _controlAmount + invoiceamount;
                _controlCount++;
                _oraclebatchnumber++;


            }

            // break transaction list into multiple list if it contains more than 250 transaction
            headerList = invoiceHeaders
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / _BCCASApi.transactionCount)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();
            #endregion

            #region Step 2: Generate and process inbox file in CRM
            // for each set of transaction create and upload inbox file in payment file exchange
            foreach (List<InvoiceHeader> headeritem in headerList)
            {
                //_controlAmount = (Double)headeritem.SelectMany(x => x.invoiceLines).ToList().Sum(x => Convert.ToDecimal(x.lineAmount));
                //_controlCount = headeritem.SelectMany(x => x.invoiceLines).ToList().Count + headeritem.Count;
                var data = new
                {
                    feederNumber = _BCCASApi.feederNumber,// Static value:3540
                    batchType = _BCCASApi.batchType,//Static  value :AP
                    delimiter = _BCCASApi.delimiter,//Static value:\u001d
                    transactionType = _BCCASApi.transactionType,//Static  value :BH
                    fiscalYear = _fiscalyear,//cureent fiscal year
                    cGIBatchNumber = _cGIBatchNumber,//unique autogenerated number
                    messageVersionNumber = _BCCASApi.messageVersionNumber,//Static  value :0001
                    controlCount = _controlCount.ToString("D15"),// total number of lines count except BH and BT
                    controlAmount = _controlAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(header.FieldLength("grossInvoiceAmount"), '0'),// total sum of amount
                    trailertransactionType = _BCCASApi.trailertransactionType,//Static  value :BT
                    InvoiceHeader = headeritem

                };

                _cGIBatchNumber = ((Convert.ToInt32(_cGIBatchNumber)) + 1).ToString("D9");
                result += template(data);

            }



            if (!string.IsNullOrEmpty(result))
            {
                #region  Step 3: Create Payment Records for all approved applications.

                await CreatePaymentFile(appUserService, d365WebApiService, _BCCASApi.feederNumber, result, paymentserializedData);

                #endregion

            }


        }
        catch (Exception ex) { }
        #endregion
        return ProcessResult.Completed(ProcessId).SimpleProcessResult;

    }

    private async Task<JsonObject> MarkPayAsProcessed(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, List<D365PaymentLine> payment)
    {
        var updatePayRequests = new List<HttpRequestMessage>() { };
        payment.ForEach(pay =>
        {
            var payToUpdate = new JsonObject {
                  { "statuscode", Convert.ToInt16(ofm_payment_StatusCode.ProcessingPayment) }

             };

            updatePayRequests.Add(new D365UpdateRequest(new EntityReference(ofm_payment.EntityLogicalCollectionName, pay.ofm_paymentid), payToUpdate));
        });

        var step2BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updatePayRequests, null);
        if (step2BatchResult.Errors.Any())
        {
            var errors = ProcessResult.Failure(ProcessId, step2BatchResult.Errors, step2BatchResult.TotalProcessed, step2BatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to update email notifications with an error: {error}", JsonValue.Create(errors)!.ToString());

            return errors.SimpleProcessResult;
        }

        return step2BatchResult.SimpleBatchResult;
    }

    private async Task<JsonObject> CreatePaymentFile(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string feederNumber, string result, List<D365PaymentLine> payment)
    {
        var filename = ("INBOX.F" + feederNumber + "." + DateTime.Now.ToString("yyyyMMddHHMMss"));
        var requestBody = new JsonObject()
        {
            ["ofm_input_file_name"] = filename,
            ["ofm_name"] = filename + "-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
            ["ofm_batch_number"] = _cGIBatchNumber,
            ["ofm_oracle_batch_name"] = _oraclebatchnumber.ToString()
        };


        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, "ofm_payment_file_exchanges", requestBody.ToString());


        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create payment file exchange record with  the server error {responseBody}", responseBody.CleanLog());


            //log the error
            // return await Task.FromResult<JsonObject>(new JsonObject() { });
        }

        var paymentRecord = await response.Content.ReadFromJsonAsync<JsonObject>();

        if (paymentRecord is not null && paymentRecord.ContainsKey("ofm_payment_file_exchangeid"))
        {
            //  paymentdocumentsResult.Add(paymentRecord);

            if (filename.Length > 0)
            {
                // Attach the file to the new document record
                HttpResponseMessage response1 = await _d365webapiservice.SendDocumentRequestAsync(_appUserService.AZPortalAppUser, "ofm_payment_file_exchanges", new Guid(paymentRecord["ofm_payment_file_exchangeid"].ToString()), Encoding.ASCII.GetBytes(result.TrimEnd()), filename);

                if (!response1.IsSuccessStatusCode)
                {
                    var responseBody = await response1.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to upload file in the payment file exchange with  the server error {responseBody}", responseBody.CleanLog());


                    //log the error
                    return await Task.FromResult<JsonObject>(new JsonObject() { });

                }


                #region  Step 4: Mark payment as processed.
                await MarkPayAsProcessed(appUserService, d365WebApiService, payment);

                #endregion


            }

        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
}