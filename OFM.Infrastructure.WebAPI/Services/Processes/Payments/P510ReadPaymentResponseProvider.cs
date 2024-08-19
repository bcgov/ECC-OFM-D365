using HandlebarsDotNet;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Net;
using System.Text.Json.Nodes;
using FixedWidthParserWriter;
using ECC.Core.DataContext;
using System.Text.Json;
using OFM.Infrastructure.WebAPI.Models.Fundings;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P510ReadPaymentResponseProvider(IOptionsSnapshot<ExternalServices> bccasApiSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly BCCASApi _BCCASApi = bccasApiSettings.Value.BCCASApi;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly TimeProvider _timeProvider = timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;

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
                          <condition attribute="ofm_payment_file_exchangeid" operator="eq" value="{_processParams?.PaymentFile?.paymentfileId}" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payment_file_exchanges({_processParams?.PaymentFile?.paymentfileId})/ofm_feedback_document_memo
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
                        <attribute name="ofm_fiscal_year" />
                        <attribute name="ofm_payment_type" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_invoice_number" />
                        <attribute name="ofm_cas_response" />
                        <attribute name="ofm_application" />
                        <order attribute="ofm_name" descending="false" />
                     <filter type="and">
                    <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ProcessingPayment}" />
                      </filter>
                     <link-entity name="ofm_fiscal_year" from="ofm_fiscal_yearid" to="ofm_fiscal_year" visible="false" link-type="outer" alias="ofm_fiscal_year">
                      <attribute name="ofm_financial_year" />                      
                    </link-entity>
                    <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner" alias="ofm_application">
                      <attribute name="ofm_application" />
                          <attribute name="ofm_applicationid" />
                    </link-entity>
                     <link-entity name="account" from="accountid" to="ofm_facility" visible="false" link-type="outer" alias="ofm_facility">
                       <attribute name="name" />
                    </link-entity>
                       </entity>
                    </fetch>
                    """;
            var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,_ofm_fiscal_year_value,ofm_payment_type,statuscode,ofm_invoice_number,ofm_cas_response,_ofm_application_value&$expand=ofm_fiscal_year($select=ofm_financial_year),ofm_application($select=ofm_application,ofm_applicationid),ofm_facility($select=name)&$filter=(statuscode eq {(int)ofm_payment_StatusCode.ProcessingPayment}) and (ofm_application/ofm_applicationid ne null)&$orderby=ofm_name asc
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
        List<string> batchfeedback = [];
        List<string> headerfeedback = [];
        List<string> listfeedback = [];
        List<string> linefeedback = [];
        List<FeedbackHeader> headers = [];
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
            List<FeedbackLine> lines = [];
            linefeedback = data.Split('\n').Where(g => g.StartsWith("APIL")).Select(g => g).ToList();
            foreach (string list1 in linefeedback)
            {
                FeedbackLine line = new CustomFileProvider<FeedbackLine>().Parse(new List<string> { list1.TrimStart() });
                lines.Add(line);

            }
            FeedbackHeader header = new CustomFileProvider<FeedbackHeader>().Parse(new List<string> { data });
            header.feedbackLine = lines;
            headers.Add(header);
        }

        var localPayData = await GetPaylinesAsync();
        var serializedPayData = JsonSerializer.Deserialize<List<D365PaymentLine>>(localPayData.Data.ToString());
        List<HttpRequestMessage> updatePayRequests = [];

        var businessclosuresdata = await GetBusinessClosuresDataAsync();
        serializedPayData?.ForEach(async pay =>
        {
            var line = headers.SelectMany(p => p.feedbackLine).FirstOrDefault(pl => pl.ILInvoice == pay.ofm_invoice_number && pl.ILDescription.Contains(pay?.ofm_payment_type.ToString()));
            var header = headers.Where(p => p.IHInvoice == pay.ofm_invoice_number).FirstOrDefault();

            List<DateTime> holidaysList = GetStartTimes(businessclosuresdata.Data.ToString());
            DateTime revisedInvoiceDate = DateTime.Today.Date.AddBusinessDays(_BCCASApi.DaysToCorrectPayments, holidaysList);
            DateTime revisedInvoiceReceivedDate = revisedInvoiceDate.AddDays(-4);
            DateTime revisedEffectiveDate = revisedInvoiceDate;

            if (line != null && header != null)
            {
                string casResponse = (line?.ILCode != "0000") ? string.Concat("Error:", line?.ILCode, " ", line?.ILError) : string.Empty;
                casResponse += (header?.IHCode != "0000") ? string.Concat(header?.IHCode, " ", header?.IHError) : string.Empty;
                //Check if payment faced error in processing.
                if (header?.IHCode != "0000")
                {
                    var subject = pay.ofm_name;
                    //create Integration log with an error message.
                   createIntregrationLogTasks.Add(CreateIntegrationErrorLog(subject, pay.ofm_application.ofm_applicationid.Value, casResponse, "CFS Integration Error", appUserService, d365WebApiService));
                }

                //Update it with latest cas response.
                var payToUpdate = new JsonObject {
                    {ofm_payment.Fields.ofm_cas_response, casResponse},
                    {ofm_payment.Fields.statecode,(int)((line?.ILCode=="0000" &&header?.IHCode=="0000") ?ofm_payment_statecode.Inactive:ofm_payment_statecode.Active)},
                    {ofm_payment.Fields.statuscode,(int)((line?.ILCode=="0000" && header?.IHCode=="0000")?ofm_payment_StatusCode.Paid:ofm_payment_StatusCode.ProcessingError)},
                    {ofm_payment.Fields.ofm_revised_invoice_date,(header?.IHCode!="0000")?revisedInvoiceDate.ToString("yyyy-MM-dd"): null},
                    {ofm_payment.Fields.ofm_revised_invoice_received_date,(header?.IHCode!="0000")?revisedInvoiceReceivedDate.ToString("yyyy-MM-dd"):null },
                    {ofm_payment.Fields.ofm_revised_effective_date,(header?.IHCode!="0000")?revisedEffectiveDate.ToString("yyyy-MM-dd"):null }
                };

                updatePayRequests.Add(new D365UpdateRequest(new D365EntityReference(ofm_payment.EntityLogicalCollectionName, pay.ofm_paymentid), payToUpdate));
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
        var payload = new JsonObject
        {
            { "ofm_category", (int)ecc_integration_log_category.Error },
            { "ofm_subject", "Payment Process Error " + subject },
            { "ofm_regardingid_ofm_application@odata.bind",$"/ofm_applications({regardingId.ToString()})"  },
            { "ofm_message", message },
            { "ofm_service_name", serviceName }
        };

        var requestBody = JsonSerializer.Serialize(payload);

        var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_integration_log.EntitySetName, requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create integration error log with the server error {responseBody}", responseBody.CleanLog());

            return ProcessResult.Failure(ProcessId, [responseBody], 0, 0).SimpleProcessResult;
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














