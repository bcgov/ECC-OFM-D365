
using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json.Nodes;
using System.Text;
using ECC.Core.DataContext;
using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P500SendPaymentRequestProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ID365DataService dataService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
    private readonly IOptionsSnapshot<ExternalServices> bccasApiSettings = bccasApiSettings;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILoggerFactory loggerFactory = loggerFactory;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly ID365DataService _dataService = dataService;

    private int _controlCount;
    private double _controlAmount;
    private int _oracleBatchNumber;
    private string _cgiBatchNumber = string.Empty;

    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _currentFiscalYearId;

    public Int16 ProcessId => Setup.Process.Payments.SendPaymentRequestId;
    public string ProcessName => Setup.Process.Payments.SendPaymentRequestName;

    public string RequestUri
    {
        get
        {
            // For reference only
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" count="1">
                      <entity name="ofm_payment_file_exchange">
                        <attribute name="ofm_payment_file_exchangeid" />
                        <attribute name="ofm_name" />
                        <attribute name="ofm_batch_number" />
                        <attribute name="ofm_oracle_batch_name" />
                        <order attribute="ofm_batch_number" descending="true" />
                        <filter type="and">
                          <condition attribute="ofm_fiscal_year" operator="eq" value="{_currentFiscalYearId}" />                  
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payment_file_exchanges?$select=ofm_payment_file_exchangeid,ofm_name,ofm_batch_number,ofm_oracle_batch_name&$filter=(_ofm_fiscal_year_value eq '{_currentFiscalYearId}')&$orderby=ofm_batch_number desc
                         """;

            return requestUri;
        }
    }

    public string AllFiscalYearsRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = $$"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_fiscal_year">
                        <attribute name="ofm_caption" />
                        <attribute name="createdon" />
                        <attribute name="ofm_agreement_number_seed" />
                        <attribute name="ofm_end_date" />
                        <attribute name="ofm_fiscal_year_number" />
                        <attribute name="owningbusinessunit" />
                        <attribute name="ofm_start_date" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_fiscal_yearid" />
                        <order attribute="ofm_caption" descending="false" />
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_fiscal_years?$select=ofm_caption,createdon,ofm_financial_year, ofm_agreement_number_seed,ofm_end_date,ofm_fiscal_year_number,_owningbusinessunit_value,ofm_start_date,statuscode,ofm_fiscal_yearid&$orderby=ofm_caption asc
                         """;

            return requestUri;
        }
    }
    public string RequestPaymentLineUri
    {
        get
        {
            var localDateOnlyPST = DateTime.UtcNow.ToLocalPST().Date;

            // For reference only
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
                     <attribute name="ofm_organization" />
                    <attribute name="ofm_revised_invoice_received_date" />
                    <attribute name="ofm_revised_invoice_date" />
                    <attribute name="ofm_revised_effective_date" />
                        <order attribute="ofm_name" descending="false" />
                         <filter type="and">
                        <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ApprovedforPayment}" />
                        <condition attribute="ofm_supplierid" operator="not-null" />
                        <condition attribute="ofm_siteid" operator="not-null" />
                        <condition attribute="ofm_payment_method" operator="not-null" />
                        <condition attribute="ofm_amount" operator="not-null" />
                        <filter type="or">
                          <condition attribute="ofm_invoice_date" operator="eq" value="{localDateOnlyPST}" />
                          <condition attribute="ofm_revised_invoice_date" operator="eq" value="{localDateOnlyPST}" />
                        </filter>
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
                         <link-entity name="ofm_funding" from="ofm_fundingid" to="ofm_funding" link-type="inner" alias="Funding">
                            <attribute name="ofm_cohort" />
                         </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,createdon,ofm_amount,ofm_description,ofm_effective_date,_ofm_fiscal_year_value,ofm_revised_invoice_received_date,ofm_revised_invoice_date,ofm_revised_effective_date,_ofm_funding_value,ofm_invoice_line_number,_owningbusinessunit_value,ofm_payment_type,ofm_remittance_message,statuscode,ofm_invoice_number,_ofm_application_value,ofm_siteid,ofm_payment_method,ofm_supplierid,ofm_invoice_received_date,ofm_invoice_date&$expand=ofm_fiscal_year($select=ofm_financial_year),ofm_application($select=ofm_application),ofm_facility($select=accountnumber,name),ofm_funding($select=ofm_cohort)&$filter=(statuscode eq {(int)ofm_payment_StatusCode.ApprovedforPayment} and ofm_supplierid ne null and ofm_siteid ne null and ofm_payment_method ne null and ofm_amount ne null and (ofm_invoice_date eq '{localDateOnlyPST}' or ofm_revised_invoice_date eq '{localDateOnlyPST}')) and (ofm_application/ofm_applicationid ne null)and (ofm_funding/ofm_fundingid ne null)&$orderby=ofm_name asc
                         """;

            return requestUri;
        }
    }

    public string RequestACKCodeUri
    {
        get
        {
            // For reference only
            var fetchXml = $"""
                    <fetch>
                      <entity name="ofm_ack_codes">
                        <attribute name="ofm_ack_number" />
                        <attribute name="ofm_cohort" />
                        <attribute name="ofm_payment_type" />
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_ack_codeses?$select=ofm_ack_number,ofm_cohort,ofm_payment_type
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

    public async Task<ProcessData> GetAllFiscalYearsDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, nameof(GetAllFiscalYearsDataAsync));

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, AllFiscalYearsRequestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query fiscal year record information with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Fiscal Year records found with query {requestUri}", AllFiscalYearsRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
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
        List<InvoiceHeader> invoiceHeaders = [];
        List<List<InvoiceHeader>> headerList = [];
        List<D365PaymentLine> serializedPaymentData = [];

        var line = typeof(InvoiceLines);
        var header = typeof(InvoiceHeader);
        string inboxFileBytes = string.Empty;

        #region Step 0.1: Get paymentlines data & current Financial Year

        var paymentData = await GetPaymentLineData();
        serializedPaymentData = JsonSerializer.Deserialize<List<D365PaymentLine>>(paymentData.Data.ToString());
        var grouppayment = serializedPaymentData?.GroupBy(p => p.ofm_invoice_number).ToList();
        var fiscalyear = serializedPaymentData?.FirstOrDefault()?.ofm_fiscal_year.ofm_financial_year;

        #endregion

        #region Step 0.2: Get latest Oracle Batch Number

        var fiscalYearsData = await GetAllFiscalYearsDataAsync();

        List<D365FiscalYear> fiscalYears = [.. JsonSerializer.Deserialize<List<D365FiscalYear>>(fiscalYearsData.Data)];
        _currentFiscalYearId = DateTime.UtcNow.ToLocalPST().Date.MatchFiscalYear(fiscalYears).ToString();
        string oracleBatchName;
        var latestPaymentFileExchangeData = await GetDataAsync();
        var serializedPFXData = JsonSerializer.Deserialize<List<ofm_payment_file_exchange>>(latestPaymentFileExchangeData.Data.ToString());

        if (serializedPFXData is not null && serializedPFXData.Count != 0 && serializedPFXData[0].ofm_batch_number != null)
        {
            _oracleBatchNumber = Convert.ToInt32(serializedPFXData[0].ofm_oracle_batch_name) + 1;
            _cgiBatchNumber = (Convert.ToInt32(serializedPFXData[0].ofm_batch_number)).ToString("D9").Substring(0, 9);
            oracleBatchName = _BCCASApi.clientCode + fiscalyear?.Substring(2) + "OFM" + (_oracleBatchNumber).ToString("D5");
        }
        else
        {
            _cgiBatchNumber = _BCCASApi.cGIBatchNumber;
            oracleBatchName = _BCCASApi.clientCode + fiscalyear?.Substring(2) + "OFM" + _BCCASApi.oracleBatchNumber;
        }

        #endregion

        #region Step 0.3: Get ACK Codes

        IEnumerable<ofm_ack_codes> ackCode = await LoadACKCodeAsync();

        #endregion

        #region Step 1: Handlebars format to generate Inbox data

        string source = "{{feederNumber}}{{batchType}}{{transactionType}}{{delimiter}}{{feederNumber}}{{fiscalYear}}{{cGIBatchNumber}}{{messageVersionNumber}}{{delimiter}}\n" + "{{#each InvoiceHeader}}{{this.feederNumber}}{{this.batchType}}{{this.headertransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.PONumber}}{{this.invoiceType}}{{this.invoiceDate}}{{this.payGroupLookup}}{{this.remittanceCode}}{{this.grossInvoiceAmount}}{{this.CAD}}{{this.invoiceDate}}{{this.termsName}}{{this.description}}{{this.goodsDate}}{{this.invoiceRecDate}}{{this.oracleBatchName}}{{this.SIN}}{{this.payflag}}{{this.flow}}{{this.delimiter}}\n" +
                        "{{#each InvoiceLines}}{{this.feederNumber}}{{this.batchType}}{{this.linetransactionType}}{{this.delimiter}}{{this.supplierNumber}}{{this.supplierSiteNumber}}{{this.invoiceNumber}}{{this.invoiceLineNumber}}{{this.committmentLine}}{{this.lineAmount}}{{this.lineCode}}{{this.distributionACK}}{{this.lineDescription}}{{this.effectiveDate}}{{this.quantity}}{{this.unitPrice}}{{this.optionalData}}{{this.distributionSupplierNumber}}{{this.flow}}{{this.delimiter}}\n{{/each}}{{/each}}" +
                        "{{this.feederNumber}}{{this.batchType}}{{this.trailertransactionType}}{{this.delimiter}}{{this.feederNumber}}{{this.fiscalYear}}{{this.cGIBatchNumber}}{{this.controlCount}}{{this.controlAmount}}{{this.delimiter}}\n";

        var template = Handlebars.Compile(source);

        // add invoice header for each organization and invoice lines for each facility
        foreach (var headeritem in grouppayment)
        {
            var pay_method = (ecc_payment_method)headeritem.First().ofm_payment_method;
            var paymentType = (ecc_payment_type)headeritem.First().ofm_payment_type; //from payment line
            var cohort = headeritem?.First().ofm_funding?.ofm_cohort; //from funding
            string ackNumber = string.Empty;
            var ackCodeList = ackCode?.Where(ack => ack.ofm_payment_type == paymentType).ToList();
            if (ackCodeList.Any() && ackCodeList.Count > 1)
            {
                ackNumber = ackCodeList.Where(code => code.ofm_cohort == cohort).Select(code => code.ofm_ack_number).FirstOrDefault();
            }
            else
            {
                ackNumber = ackCodeList.Select(code => code.ofm_ack_number).FirstOrDefault();
            }
            double invoiceamount = 0.00;
            List<InvoiceLines> invoiceLines = [];

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
                    lineAmount = (lineitem.item.ofm_amount < 0 ? "-" : "") + Math.Abs(lineitem.item.ofm_amount.Value).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(line.FieldLength("lineAmount") - (lineitem.item.ofm_amount < 0 ? 1 : 0), '0'),// come from split funding amount per facility
                    lineCode = (lineitem.item.ofm_amount > 0 ? "D" : "C"),//if it is positive then line code is Debit otherwise credit
                    //distributionACK = _BCCASApi.InvoiceLines.distributionACK.PadRight(line.FieldLength("distributionACK")),// using test data shared by CAS,should be changed for prod
                    distributionACK = ackNumber.PadRight(line.FieldLength("distributionACK")), //fetching from ACK Codes from dataverse based on payment type and cohort
                    lineDescription = (lineitem.item.ofm_payment_type).ToString().PadRight(line.FieldLength("lineDescription")), // Pouplate extra info from facility/funding amount
                    effectiveDate = lineitem.item.ofm_revised_effective_date?.ToString("yyyyMMdd") ?? lineitem.item.ofm_effective_date?.ToString("yyyyMMdd"),//same as invoice date
                    quantity = _BCCASApi.InvoiceLines.quantity,//Static Value:0000000.00 not used by feeder
                    unitPrice = _BCCASApi.InvoiceLines.unitPrice,//Static Value:000000000000.00 not used by feeder
                    optionalData = string.Empty.PadRight(line.FieldLength("optionalData")),// PO ship to asset tracking values are set to blank as it is optional
                    distributionSupplierNumber = lineitem.item.ofm_supplierid.PadRight(line.FieldLength("distributionSupplierNumber")),// Supplier number from Organization
                    flow = string.Empty.PadRight(line.FieldLength("flow")), //can be use to pass additional info from facility or application
                }); ; ;

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
                invoiceDate = headeritem.First().ofm_revised_invoice_date?.ToString("yyyyMMdd") ?? headeritem.First().ofm_invoice_date?.ToString("yyyyMMdd"), // set to current date
                invoiceType = invoiceamount < 0 ? "CM" : "ST",// static to ST (standard invoice)
                payGroupLookup = string.Concat("GEN ", pay_method, " N"),//GEN CHQ N if using cheque or GEN EFT N if direct deposit
                remittanceCode = _BCCASApi.InvoiceHeader.remittanceCode.PadRight(header.FieldLength("remittanceCode")), // for payment stub it is 00 always.
                grossInvoiceAmount = (invoiceamount < 0 ? "-" : "") + Math.Abs(invoiceamount).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(header.FieldLength("grossInvoiceAmount") - (invoiceamount < 0 ? 1 : 0), '0'), // invoice amount come from OFM total base value.
                CAD = _BCCASApi.InvoiceHeader.CAD,// static value :CAD
                termsName = _BCCASApi.InvoiceHeader.termsName.PadRight(header.FieldLength("termsName")),//setting it to immediate for successful testing, this needs to be dynamic going forward.
                goodsDate = string.Empty.PadRight(header.FieldLength("goodsDate")),//optional field so set to null
                invoiceRecDate = headeritem.First().ofm_revised_invoice_received_date?.ToString("yyyyMMdd") ?? headeritem.First().ofm_invoice_received_date?.ToString("yyyyMMdd"),// 5 days from invoice date
                oracleBatchName = (_BCCASApi.clientCode + fiscalyear?.Substring(2) + "OFM" + (_oracleBatchNumber).ToString("D5")).PadRight(header.FieldLength("oracleBatchName")),//6225OFM00001 incremented by 1 for each header
                SIN = string.Empty.PadRight(header.FieldLength("SIN")), //optional field set to blank
                payflag = _BCCASApi.InvoiceHeader.payflag,// Static value: Y (separate chq for each line)
                description = Regex.Replace(headeritem.First()?.ofm_facility?.name, @"[^\w $\-]", "").PadRight(header.FieldLength("description")).Substring(0, header.FieldLength("description")),// can be used to pass extra info
                flow = string.Empty.PadRight(header.FieldLength("flow")),// can be used to pass extra info
                invoiceLines = invoiceLines
            }); ;
            _controlAmount = _controlAmount + invoiceamount;
            _controlCount++;

        }

        // break transaction list into multiple list if it contains more than 250 transactions
        headerList = invoiceHeaders
        .Select((x, i) => new { Index = i, Value = x })
        .GroupBy(x => x.Index / _BCCASApi.transactionCount)
        .Select(x => x.Select(v => v.Value).ToList())
        .ToList();

        #endregion

        #region Step 2: Compose the inbox file string
        List<D365PaymentLine> paylinesToUpdate = new List<D365PaymentLine>();
        // for each set of transaction create and upload inbox file in payment file exchange
        foreach (List<InvoiceHeader> headeritem in headerList)
        {
            _cgiBatchNumber = ((Convert.ToInt32(_cgiBatchNumber)) + 1).ToString("D9"); // Increase the CGI Batch Number by 1 

            headeritem.ForEach(x =>
            {

                foreach (var paydata in serializedPaymentData.Where(paydata => paydata.ofm_invoice_number == x.invoiceLines.First().invoiceNumber.TrimEnd()))
                {
                    paydata.ofm_batch_number = _cgiBatchNumber;
                    paylinesToUpdate.Add(paydata);
                }
            });

            _controlAmount = (Double)headeritem.SelectMany(x => x.invoiceLines).ToList().Sum(x => Convert.ToDecimal(x.lineAmount));
            _controlCount = headeritem.SelectMany(x => x.invoiceLines).ToList().Count + headeritem.Count;

            var data = new
            {
                feederNumber = _BCCASApi.feederNumber,// Static value:3540
                batchType = _BCCASApi.batchType,//Static  value :AP
                delimiter = _BCCASApi.delimiter,//Static value:\u001d
                transactionType = _BCCASApi.transactionType,//Static  value :BH
                fiscalYear = fiscalyear,//current fiscal year
                cGIBatchNumber = _cgiBatchNumber,//unique autogenerated number
                messageVersionNumber = _BCCASApi.messageVersionNumber,//Static  value :0001
                controlCount = _controlCount.ToString("D15"),// total number of lines count except BH and BT
                controlAmount = _controlAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(header.FieldLength("grossInvoiceAmount"), '0'),// total sum of amount
                trailertransactionType = _BCCASApi.trailertransactionType,//Static  value :BT
                InvoiceHeader = headeritem
            };

            inboxFileBytes += template(data);
        }

        #endregion

        #region  Step 3: Create Payment File Exchange Record for all approved payment lines and Mark Processed Payment Lines

        if (!string.IsNullOrEmpty(inboxFileBytes))
        {
            bool savePFEResult = await SaveInboxFileOnNewPaymentFileExchangeRecord(appUserService, d365WebApiService, _BCCASApi.feederNumber, inboxFileBytes);

            if (savePFEResult)
                await MarkPaymentLinesAsProcessed(appUserService, d365WebApiService, paylinesToUpdate);
        }

        #endregion

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    private async Task<JsonObject> MarkPaymentLinesAsProcessed(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, List<D365PaymentLine> payments)
    {
        var updatePayRequests = new List<HttpRequestMessage>() { };
        payments.ForEach(pay =>
        {
            var payToUpdate = new JsonObject {
                  { "statuscode", Convert.ToInt16(ofm_payment_StatusCode.ProcessingPayment) },
                   {"ofm_batch_number",pay.ofm_batch_number }
             };

            updatePayRequests.Add(new D365UpdateRequest(new D365EntityReference(ofm_payment.EntityLogicalCollectionName, pay.ofm_paymentid), payToUpdate));
        });

        var paymentBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updatePayRequests, null);
        if (paymentBatchResult.Errors.Any())
        {
            var errors = ProcessResult.Failure(ProcessId, paymentBatchResult.Errors, paymentBatchResult.TotalProcessed, paymentBatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to update payment status with an error: {error}", JsonValue.Create(errors)!.ToString());

            return errors.SimpleProcessResult;
        }

        return paymentBatchResult.SimpleBatchResult;
    }

    private async Task<bool> SaveInboxFileOnNewPaymentFileExchangeRecord(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, string feederNumber, string result)
    {
        var inboxFileName = ("INBOX.F" + feederNumber + "." + DateTime.UtcNow.ToLocalPST().ToString("yyyyMMddHHmmss"));
        var requestBody = new JsonObject()
        {
            ["ofm_input_file_name"] = inboxFileName,
            ["ofm_name"] = inboxFileName,
            ["ofm_batch_number"] = _cgiBatchNumber,
            ["ofm_oracle_batch_name"] = _oracleBatchNumber.ToString(),
            ["ofm_fiscal_year@odata.bind"] = $"/ofm_fiscal_years({_currentFiscalYearId})"
        };

        var pfeCreateResponse = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_payment_file_exchange.EntitySetName, requestBody.ToString());

        if (!pfeCreateResponse.IsSuccessStatusCode)
        {
            var pfeCreateError = await pfeCreateResponse.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create payment file exchange record with the server error {responseBody}", JsonValue.Create(pfeCreateError)!.ToString());

            return await Task.FromResult(false);
        }

        var pfeRecord = await pfeCreateResponse.Content.ReadFromJsonAsync<JsonObject>();

        if (pfeRecord is not null && pfeRecord.ContainsKey(ofm_payment_file_exchange.Fields.ofm_payment_file_exchangeid))
        {
            if (inboxFileName.Length > 0)
            {
                // Update the new Payment File Exchange record with the new document
                HttpResponseMessage pfeUpdateResponse = await _d365webapiservice.SendDocumentRequestAsync(_appUserService.AZPortalAppUser, ofm_payment_file_exchange.EntitySetName,
                                                                                                    new Guid(pfeRecord[ofm_payment_file_exchange.Fields.ofm_payment_file_exchangeid].ToString()),
                                                                                                    Encoding.ASCII.GetBytes(result.TrimEnd()),
                                                                                                    inboxFileName);

                if (!pfeUpdateResponse.IsSuccessStatusCode)
                {
                    var pfeUpdateError = await pfeUpdateResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to update payment file exchange record with the new inbox document with the server error {responseBody}", pfeUpdateError.CleanLog());

                    return await Task.FromResult(false);
                }
            }
        }
        return await Task.FromResult(true);
    }
    private async Task<IEnumerable<ofm_ack_codes>> LoadACKCodeAsync()
    {
        var localdata = await _dataService.FetchDataAsync(RequestACKCodeUri, "ACK_Codes");
        var deserializedData = localdata.Data.Deserialize<List<ofm_ack_codes>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!); ;
    }
}