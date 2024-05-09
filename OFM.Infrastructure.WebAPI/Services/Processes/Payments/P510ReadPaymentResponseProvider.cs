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
using FixedWidthParserWriter;
using static System.Net.WebRequestMethods;
using Microsoft.Extensions.Hosting;
using Microsoft.Xrm.Sdk;
using File = System.IO.File;
using ECC.Core.DataContext;
using System.Runtime.InteropServices.JavaScript;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;
using EntityReference = OFM.Infrastructure.WebAPI.Messages.EntityReference;



namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P510ReadPaymentResponseProvider : ID365ProcessProvider
{

    private readonly BCCASApi _BCCASApi;
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

    public P510ReadPaymentResponseProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
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
                        <condition attribute="modifiedon" operator="on-or-after" value="{DateTime.UtcNow.AddDays(-1)}" />
                        <condition attribute="ofm_feedback_file_name" operator="not-null" />
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

    public string PaymentInProcessUri
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
                        <attribute name="ofm_amount_paid_base" />
                        <attribute name="ofm_description" />
                        <attribute name="ofm_effective_date" />
                        <attribute name="ofm_fiscal_year" />
                        <attribute name="ofm_funding" />
                        <attribute name="ofm_invoice_line_number" />
                        <attribute name="owningbusinessunit" />
                        <attribute name="ofm_paid_date" />
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
                    <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ProcessingPayment}" />
                      </filter>
                     <link-entity name="ofm_fiscal_year" from="ofm_fiscal_yearid" to="ofm_fiscal_year" visible="false" link-type="outer" alias="ofm_fiscal_year">
                      <attribute name="ofm_financial_year" />                      
                    </link-entity>
                    <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner" alias="ofm_application">
                      <attribute name="ofm_application" />
                    </link-entity>
                     <link-entity name="account" from="accountid" to="ofm_facility" visible="false" link-type="outer" alias="ofm_facility">
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
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P510ReadPaymentResponseProvider));

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

    public async Task<ProcessData> GetPaylinesAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P510ReadPaymentResponseProvider));

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, PaymentInProcessUri, isProcess: true);
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


            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);


    }

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        List<string> batchfeedback = new List<string>();
        List<string> headerfeedback = new List<string>();
        List<string> listfeedback = new List<string>();
        List<string> linefeedback = new List<string>();
        List<feedbackHeader> headers = new List<feedbackHeader>();

        var startTime = _timeProvider.GetTimestamp();
       
        var localData = await GetDataAsync();
            var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<Payment_File_Exchange>>(localData.Data.ToString());

            HttpResponseMessage response1 = await _d365webapiservice.GetDocumentRequestAsync(_appUserService.AZPortalAppUser, "ofm_payment_file_exchanges", new Guid(serializedData[0].ofm_payment_file_exchangeid));
            if (!response1.IsSuccessStatusCode)
            {
                var responseBody = await response1.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to download file in the payment file exchange with  the server error {responseBody}", responseBody.CleanLog());
                return await Task.FromResult<JsonObject>(new JsonObject() { });

            }
            byte[] file1 = response1.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();


            listfeedback = System.Text.Encoding.UTF8.GetString(file1).Replace("APBG", "APBG#APBG").Split("APBG#").ToList();
            listfeedback.RemoveAll(item => string.IsNullOrWhiteSpace(item));           
            batchfeedback = listfeedback.Select(g => g.Replace("APBH", "APBH#APBH")).SelectMany(group => group.Split("APBH#")).ToList();
            headerfeedback = listfeedback.Select(g => g.Replace("APIH", "APIH#APIH")).SelectMany(group => group.Split("APIH#")).ToList();
          
            foreach (string data in headerfeedback)
            {
                List<feedbackLine> lines = new List<feedbackLine>();
                linefeedback = data.Split('\n').Where(g => g.StartsWith("APIL")).Select(g => g).ToList();
                foreach (string list1 in linefeedback)
                {
                    feedbackLine line = new CustomFileProvider<feedbackLine>().Parse(new List<string> { list1.TrimStart() });
                    lines.Add(line);

                }
                feedbackHeader header = new CustomFileProvider<feedbackHeader>().Parse(new List<string> { data });
                header.feedbackLine = lines;
                headers.Add(header);
            }
            var localPayData = await GetPaylinesAsync();
            var serializedPayData = System.Text.Json.JsonSerializer.Deserialize<List<Payment_Line>>(localPayData.Data.ToString());
            var updatePayRequests = new List<HttpRequestMessage>() { };

            serializedPayData?.ForEach(pay =>
            {
                var line = headers.SelectMany(p=>p.feedbackLine).SingleOrDefault(pl => pl.ILInvoice == pay.ofm_invoice_number && pl.ILDescription.StartsWith(string.Concat(pay.ofm_application_number, " ", pay.ofm_payment_type)));
                var header =  headers.Where(p=>p.IHInvoice==pay.ofm_invoice_number).First();
                string casResponse = (line?.ILCode != "0000")?string.Concat("Error:", line?.ILCode, " ", line?.ILError) : string.Empty;
                casResponse += (header.IHCode != "0000") ? string.Concat(header.IHCode, " ", header.IHError) : string.Empty;

                var payToUpdate = new JsonObject {
                {ofm_payment.Fields.ofm_cas_response, casResponse},
                {ofm_payment.Fields.statecode,(int)((line?.ILCode=="0000" &&header.IHCode=="0000") ?ofm_payment_statecode.Inactive:ofm_payment_statecode.Active)},
                {ofm_payment.Fields.statuscode,(int)((line?.ILCode=="0000" && header.IHCode=="0000")?ofm_payment_StatusCode.Paid:ofm_payment_StatusCode.ProcessingERROR)},
               };
                updatePayRequests.Add(new D365UpdateRequest(new EntityReference(ofm_payment.EntityLogicalCollectionName, new Guid(pay.ofm_paymentid)), payToUpdate));


            });

            var step2BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updatePayRequests, null);
            if (step2BatchResult.Errors.Any())
            {
                var errors = ProcessResult.Failure(ProcessId, step2BatchResult.Errors, step2BatchResult.TotalProcessed, step2BatchResult.TotalRecords);
                _logger.LogError(CustomLogEvent.Process, "Failed to update email notifications with an error: {error}", JsonValue.Create(errors)!.ToString());

                return errors.SimpleProcessResult;
            }
      
        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }


}














