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
using FixedWidthParserWriter;
using System.Collections.Generic;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using Polly;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using static OFM.Infrastructure.WebAPI.Extensions.Setup.Process;
using System.ComponentModel.DataAnnotations;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;
public class P530ValidatePaymentRequest(IPaymentValidator paymentvalidator,IOptionsSnapshot<NotificationSettings> notificationSettings, IOptionsSnapshot<D365AuthSettings> d365AuthSettings,IEmailRepository emailRepository, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ID365DataService dataService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
   private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILoggerFactory loggerFactory = loggerFactory;
    private readonly NotificationSettings _notificationSettings= notificationSettings.Value;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly ID365DataService _dataService = dataService;
    private readonly D365AuthSettings _d365AuthSettings = d365AuthSettings.Value;
    private readonly IEmailRepository _emailRepository = emailRepository;
    private readonly IPaymentValidator _paymentvalidator = paymentvalidator;
    private string? _informationCommunicationType;

    private ProcessData? _data;
    private ProcessParameter? _processParams;
   

    public Int16 ProcessId => Setup.Process.Payments.ValidatePaymentRequestId;
    public string ProcessName => Setup.Process.Payments.ValidatePaymentRequestName;

    public string RequestPaymentLineUri
    {
        get
        {

            var localDateOnlyPST = DateTime.UtcNow.ToLocalPST().Date.AddDays(2);

            // For reference only
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="ofm_payment">
                        <attribute name="ofm_paymentid"/>
                        <attribute name="ofm_name"/> 
                        <attribute name="ofm_invoice_number"/>
                        <attribute name="ofm_invoice_line_number"/>
                        <attribute name="ofm_payment_type"/>
                        <attribute name="ofm_amount"/>
                        <attribute name="ofm_organization"/>
                        <attribute name="ofm_facility"/>
                        <attribute name="ofm_effective_date"/>
                        <attribute name="ofm_fiscal_year"/>
                        <attribute name="ofm_funding"/>
                        <attribute name="ofm_siteid"/>
                        <attribute name="ofm_payment_method" />
                        <attribute name="ofm_supplierid"/>
                       <attribute name="ofm_invoice_received_date"/>
                       <attribute name="ofm_invoice_date"/>
                       <order attribute="ofm_name" descending="false"/>
                        <filter type="and">
                        <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ApprovedforPayment}" />
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
                            <attribute name="ofm_cohortid" />
                           <link-entity name="ofm_ack_codes" from="ofm_cohortid" to="ofm_cohortid" link-type="outer" alias="Ack">
                      <attribute name="ofm_cohortid" />
                    </link-entity>
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

    
    public async Task<JsonObject> RunProcessAsync( ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        List<PaymentLine> serializedPaymentData = [];
        List<Guid> opsuserID = [];
         Dictionary<string, List<ValidationResult>> invalidLine = new Dictionary<string, List<ValidationResult>>();
        List<HttpRequestMessage> SendEmailRequest = [];
        //     
        var paymentData = await GetDataAsync();
     
       serializedPaymentData = JsonSerializer.Deserialize<List<PaymentLine>>(paymentData.Data.ToString());
        if (serializedPaymentData.Count > 0 && serializedPaymentData != null)
        {
            invalidLine = await _paymentvalidator.AreAllPropertiesValid(serializedPaymentData);
            if (invalidLine.Count > 0)
            {
                opsuserID = await _paymentvalidator.GetOpssupervisorEmail();

                await _paymentvalidator.SendPaymentErrorEmail(530,_processParams.Notification?.TemplateNumber,(Guid)(_processParams.Notification.SenderId), invalidLine);
            }
        }


        return ProcessResult.Completed(ProcessId).SimpleProcessResult;
    }
  
}





