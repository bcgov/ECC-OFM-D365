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



namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P505ReadPaymentResponseProvider: ID365ProcessProvider
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
                        <condition attribute="modifiedon" operator="on-or-after" value="{DateTime.UtcNow.AddDays(-6)}" />
                        <condition attribute="ofm_input_file_name" operator="not-null" />
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

    public async Task<ProcessData> GetDataAsync()
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
       
        var startTime = _timeProvider.GetTimestamp();

        var localData = await GetDataAsync();
        var serializedData = System.Text.Json.JsonSerializer.Deserialize<List<Payment_File_Exchange>>(localData.Data.ToString());

        HttpResponseMessage response1 = await _d365webapiservice.GetDocumentRequestAsync(_appUserService.AZPortalAppUser, "ofm_payment_file_exchanges", new Guid(serializedData[0].ofm_payment_file_exchangeid));
        if (!response1.IsSuccessStatusCode)
        {
            var responseBody = await response1.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to upload file in the payment file exchange with  the server error {responseBody}", responseBody.CleanLog());
            return await Task.FromResult<JsonObject>(new JsonObject() { });

        }
        byte[] file1 = response1.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

        //  byte[] file1 = File.ReadAllBytes("output.txt");
        List<string> listfeedback = System.Text.Encoding.UTF8.GetString(file1).Split("APBG").ToList();
        listfeedback.RemoveAll(item => string.IsNullOrWhiteSpace(item));
        int counter = 0;
        List<string> batchfeedback = listfeedback.Select(g => g.Replace("APBH", "APBH#APBH")).SelectMany(group => group.Split("APBH#")).ToList();
        List<string> headerfeedback = listfeedback.Select(g => g.Replace("APIH", "APIH#APIH")).SelectMany(group => group.Split("APIH#")).ToList();
         List<string> linefeedback;
        List<feedbackHeader> headers = new List<feedbackHeader>();
        foreach (string data in headerfeedback)
        {
            List<feedbackLine> lines = new List<feedbackLine>();
            linefeedback = data.Split('\n').Where(g => g.StartsWith("APIL")).Select(g=>g).ToList();
            foreach (string list1 in linefeedback)
            {
                feedbackLine line = new CustomFileProvider<feedbackLine>().Parse(new List<string> { list1 });
                lines.Add(line);

            }
            feedbackHeader header = new CustomFileProvider<feedbackHeader>().Parse(new List<string> { data });
            header.feedbackLine = lines;
            headers.Add(header);
        }
       return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }


}














