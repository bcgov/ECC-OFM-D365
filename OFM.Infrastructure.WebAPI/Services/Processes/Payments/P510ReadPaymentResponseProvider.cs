﻿using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using static OFM.Infrastructure.WebAPI.Models.BCRegistrySearchResult;
using FixedWidthParserWriter;
using ECC.Core.DataContext;
using EntityReference = OFM.Infrastructure.WebAPI.Messages.EntityReference;
using System.Text.Json;
using OFM.Infrastructure.WebAPI.Models.Fundings;



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

    public Int16 ProcessId => Setup.Process.Payments.GetPaymentResponseId;
    public string ProcessName => Setup.Process.Payments.GetPaymentResponseName;

    public string RequestUri
    {
        get
        {
            // this query is just for info,payment file data is coming from requesturl
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_payment_file_exchange">
                        <attribute name="ofm_payment_file_exchangeid" />
                        <attribute name="ofm_name" />
                        <attribute name="createdon" />
                        <order attribute="ofm_name" descending="false" />
                        <filter type="and">    
                          <condition attribute="ofm_payment_file_exchangeid" operator="eq" value="{_processParams?.paymentfile?.paymentfileId}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payment_file_exchanges({_processParams?.paymentfile?.paymentfileId})/ofm_feedback_document_memo
                         """;

            return requestUri;
        }
    }
    //Retrieve Business Closures.
    public string BusinessClosuresRequestUri
    {
        get
        {
            var fetchXml = $$"""
                    <fetch>
                      <entity name="msdyn_businessclosure">
                        <attribute name="msdyn_starttime" />
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         msdyn_businessclosures?fetchXml={WebUtility.UrlEncode(fetchXml)}
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
                        <attribute name="ofm_cas_response" />
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
                
                d365Result = currentValue!;
            }

            _data = new ProcessData(d365Result);

            _logger.LogDebug(CustomLogEvent.Process, "Query Result {_data}", _data.Data.ToJsonString());
        }

        return await Task.FromResult(_data);


    }
    public async Task<ProcessData> GetBusinessClosuresDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, nameof(GetBusinessClosuresDataAsync));

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, BusinessClosuresRequestUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Funding record information with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Funding records found with query {requestUri}", BusinessClosuresRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
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
        var createIntregrationLogTasks = new List<Task>();
        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetDataAsync();
        var downloadfile = Convert.FromBase64String(localData.Data.ToString());
        
        listfeedback = System.Text.Encoding.UTF8.GetString(downloadfile).Replace("APBG", "APBG#APBG").Split("APBG#").ToList();
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
        
        var businessclosuresdata = await GetBusinessClosuresDataAsync();
        serializedPayData?.ForEach(async pay =>
        {
            var line = headers.SelectMany(p => p.feedbackLine).SingleOrDefault(pl => pl.ILInvoice == pay.ofm_invoice_number && pl.ILDescription.StartsWith(string.Concat(pay.ofm_application_number, " ", pay.ofm_payment_type)));
            var header = headers.Where(p => p.IHInvoice == pay.ofm_invoice_number).FirstOrDefault();
           
          
            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());
            DateTime revisedInvoiceDate = TimeExtensions.GetRevisedInvoiceDate(DateTime.Today, 3,holidaysList);
            DateTime revisedInvoiceReceivedDate = revisedInvoiceDate.AddDays(-4);
            DateTime revisedEffectiveDate = TimeExtensions.GetCFSEffectiveDate(revisedInvoiceReceivedDate, holidaysList);

            if (line != null && header != null)
            {
                string casResponse = (line?.ILCode != "0000") ? string.Concat("Error:", line?.ILCode, " ", line?.ILError) : string.Empty;
                casResponse += (header?.IHCode != "0000") ? string.Concat(header?.IHCode, " ", header?.IHError) : string.Empty;
                //Check if payment faced error in previous processing.
                if (pay.ofm_cas_response != null && pay.ofm_cas_response.Contains("Error:"))
                {
                    var subject = pay.ofm_name;
                    //create Integration log with old error message.
                    createIntregrationLogTasks.Add(CreateIntegrationErrorLog(subject, pay._ofm_application_value , pay.ofm_cas_response, "P510 Read Response from CFS", appUserService, d365WebApiService));
                }
                //Update it with latest cas response.
                var payToUpdate = new JsonObject {  
                {ofm_payment.Fields.ofm_cas_response, casResponse},
                {ofm_payment.Fields.statecode,(int)((line?.ILCode=="0000" &&header?.IHCode=="0000") ?ofm_payment_statecode.Inactive:ofm_payment_statecode.Active)},
                {ofm_payment.Fields.statuscode,(int)((line?.ILCode=="0000" && header?.IHCode=="0000")?ofm_payment_StatusCode.Paid:ofm_payment_StatusCode.ProcessingERROR)},
                {ofm_payment.Fields.ofm_revised_invoice_date,(line?.ILCode!="0000" && header?.IHCode!="0000")?revisedInvoiceDate: null},
                {ofm_payment.Fields.ofm_revised_invoice_received_date,(line?.ILCode!="0000" && header?.IHCode!="0000")?revisedInvoiceReceivedDate:null },
                {ofm_payment.Fields.ofm_revised_effective_date,(line?.ILCode!="0000" && header?.IHCode!="0000")?revisedEffectiveDate:null }
               };
                
                updatePayRequests.Add(new D365UpdateRequest(new EntityReference(ofm_payment.EntityLogicalCollectionName, new Guid(pay.ofm_paymentid)), payToUpdate));
            }
        });

        var step2BatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, updatePayRequests, null);
        if (step2BatchResult.Errors.Any())
        {
            var errors = ProcessResult.Failure(ProcessId, step2BatchResult.Errors, step2BatchResult.TotalProcessed, step2BatchResult.TotalRecords);
            _logger.LogError(CustomLogEvent.Process, "Failed to update email notifications with an error: {error}", JsonValue.Create(errors)!.ToString());

            return errors.SimpleProcessResult;
        }
        await Task.WhenAll(createIntregrationLogTasks);
        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }

    private async Task<JsonObject> CreateIntegrationErrorLog(string subject, Guid regardingId, string message, string serviceName, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
    {
        var entitySetName = "ofm_integration_logs";

        var payload = new JsonObject
    {
        { "ofm_category", (int)IntegrationLog_Category.Error },
        { "ofm_subject", "Payment Process Error " + subject },
        { "ofm_regardingid_ofm_application@odata.bind",$"/ofm_applications({regardingId.ToString()})"  },
        { "ofm_message", message },
        { "ofm_service_name", serviceName }
    };

        var requestBody = JsonSerializer.Serialize(payload);

        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, entitySetName, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create integration error log with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
    private static List<DateTime> GetStartTimes(string jsonData)
    {
        var closures = JsonSerializer.Deserialize<List<BusinessClosure>>(jsonData);

        List<DateTime> startTimeList = closures.Select(closure => DateTime.Parse(closure.msdyn_starttime)).ToList();

        return startTimeList;
    }


}














