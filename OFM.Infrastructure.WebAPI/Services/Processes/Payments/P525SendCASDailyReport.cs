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
using Microsoft.AspNetCore.Mvc;
using System.Buffers.Text;
using System;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments;

public class P525SendCASDailyReport(IOptionsSnapshot<NotificationSettings> notificationSettings, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider) : ID365ProcessProvider
{
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = d365WebApiService;
    private readonly ILoggerFactory loggerFactory = loggerFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);


    private ProcessData? _data;
    private ProcessParameter? _processParams;

    private readonly NotificationSettings _notificationSettings = notificationSettings.Value;

    public Int16 ProcessId => Setup.Process.Payments.SendCASDailyReportId;
    public string ProcessName => Setup.Process.Payments.SendCASDailyReportName;


    public string RequestUri
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
                        <attribute name="ofm_amount" />
                        <attribute name="ofm_description" />
                        <attribute name="ofm_effective_date" />
                        <attribute name="ofm_payment_type" />
                        <attribute name="statuscode" />
                        <attribute name="ofm_invoice_number" />
                        <attribute name="ofm_application" />
                        <attribute name="ofm_siteid" />
                        <attribute name="ofm_payment_method" />
                        <attribute name="ofm_supplierid" />
                        <attribute name="ofm_invoice_date" />
                        <attribute name="ofm_batch_number" />
                        <order attribute="ofm_name" descending="false" />
                        <filter type="and">
                          <condition attribute="statuscode" operator="eq" value="{(int)ofm_payment_StatusCode.ProcessingPayment}" />
                          <condition attribute="ofm_supplierid" operator="not-null" />
                          <condition attribute="ofm_siteid" operator="not-null" />
                          <condition attribute="ofm_payment_method" operator="not-null" />
                          <condition attribute="ofm_amount" operator="not-null" />
                          <filter type="or">
                            <condition attribute="ofm_invoice_date" operator="eq" value="{localDateOnlyPST}" />
                            <condition attribute="ofm_revised_invoice_date" operator="eq" value="{localDateOnlyPST}" />
                          </filter>
                        </filter>
                        <link-entity name="ofm_application" from="ofm_applicationid" to="ofm_application" link-type="inner" alias="ofm_application">
                          <attribute name="ofm_application" />
                        </link-entity>
                        <link-entity name="account" from="accountid" to="ofm_facility" visible="false" link-type="outer" alias="ofm_facility">
                          <attribute name="accountnumber" />
                          <attribute name="name" />
                          <link-entity name="account" from="accountid" to="parentaccountid" alias="org">
                            <attribute name="name" />
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_payments?$select=ofm_paymentid,ofm_name,ofm_amount,ofm_description,ofm_effective_date,ofm_payment_type,statuscode,ofm_invoice_number,_ofm_application_value,ofm_siteid,ofm_payment_method,ofm_supplierid,ofm_invoice_date,ofm_batch_number&$expand=ofm_application($select=ofm_application),ofm_facility($select=accountnumber,name;$expand=parentaccountid($select=name))&$filter=(statuscode eq {(int)ofm_payment_StatusCode.ProcessingPayment} and ofm_supplierid ne null and ofm_siteid ne null and ofm_payment_method ne null and ofm_amount ne null and (ofm_invoice_date eq '{localDateOnlyPST}' or ofm_revised_invoice_date eq '{localDateOnlyPST}')) and (ofm_application/ofm_applicationid ne null)&$orderby=ofm_name asc
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

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;

        if (_processParams == null || _processParams.Notification == null || _processParams.Notification.CasReportContactId == null)
        {
            _logger.LogError(CustomLogEvent.Process, "CasReportContactId is missing.");
            throw new Exception("CasReportContactId is missing.");

        }
        var startTime = _timeProvider.GetTimestamp();

        #region Step 1: Get paymentlines data

        var paymentData = await GetDataAsync();
        var deserializedPaymentData = JsonSerializer.Deserialize<List<D365PaymentLine>>(paymentData.Data,Setup.s_writeOptionsForLogs);

        #endregion

        #region Step 2: Create CSV file in memory

        string csvFile = String.Empty;

        //Create csv file
        var line = typeof(InvoiceLines);

        var csvfileName = "CAS Report - " + DateTime.UtcNow.ToLocalPST().ToString("yyyyMMddHHmmss") + ".csv";
        var memoryStream = new MemoryStream();
        using(var writer = new StreamWriter(memoryStream))
        {
                
            string[] columns = { "BatchNumber", "SupplierNumber", "OrganizationName", "FacilityName", "SupplierSite", "InvoiceNumber", "InvoiceDate", "InvoiceAmount", "Description", "EffectiveDate" };
            string separator = ",";
            writer.Write(String.Join(separator, columns));
            writer.Write("\r\n");
            if (deserializedPaymentData is not null && deserializedPaymentData.Count > 0)
            {
                foreach (var payment in deserializedPaymentData)
                {
                    var batchNumber = payment.ofm_batch_number;
                    var supplierNumber = payment.ofm_supplierid;
                    var supplierSiteNumber = payment.ofm_siteid.PadLeft(line.FieldLength("supplierSiteNumber"), '0');
                    var organizationName = payment.ofm_facility?.parentaccountid?.name??"";
                    var facilityName = payment.ofm_facility?.name??"";
                    var invoiceNumber = payment.ofm_invoice_number;
                    var invoiceDate = payment.ofm_invoice_date?.ToString("yyyyMMdd")??"";
                    var lineAmount = (payment?.ofm_amount < 0 ? "-" : "") + Math.Abs((payment.ofm_amount)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(line.FieldLength("lineAmount") - (payment?.ofm_amount < 0 ? 1 : 0), '0');
                    var lineDescription = string.Concat(payment?.ofm_application?.ofm_application, " ", payment?.ofm_payment_type);
                    var effectiveDate = payment?.ofm_effective_date?.ToString("yyyyMMdd")??"";

                    string[] paymentline = { batchNumber, supplierNumber, organizationName, facilityName, supplierSiteNumber, invoiceNumber, invoiceDate, lineAmount, lineDescription, effectiveDate };
                    writer.Write(String.Join(separator, paymentline));
                    writer.Write("\r\n");
                }
            }
            writer.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
        }

        csvFile = Encoding.UTF8.GetString(memoryStream.ToArray());

        #endregion

        #region Step 3: Create Email Notification

        string subject = _processParams.Notification.Subject;
        string emaildescription = _processParams.Notification.EmailBody;

        var emailObject = new JsonObject(){
                        {"subject",subject },
                        {"description",emaildescription },
                        {"email_activity_parties", new JsonArray(){
                            new JsonObject
                            {
                                {"partyid_systemuser@odata.bind", $"/systemusers({_notificationSettings.DefaultSenderId})"},
                                { "participationtypemask", 1 } //From Email
                            },
                            new JsonObject
                            {
                                { "partyid_contact@odata.bind", $"/contacts({_processParams.Notification.CasReportContactId})" },
                                { "participationtypemask",   2 } //To Email                             
                            }
                        }},
                        {"regardingobjectid_contact@odata.bind",$"/contacts({_processParams.Notification.CasReportContactId})" }
            };

        //Send email request
        var sendNewEmailRequestResult = await d365WebApiService.SendCreateRequestAsync(_appUserService.AZSystemAppUser, "emails", JsonSerializer.Serialize(emailObject));

        if (!sendNewEmailRequestResult.IsSuccessStatusCode)
        {
            var responseBody = await sendNewEmailRequestResult.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create the record with the server error {responseBody}", responseBody.CleanLog());
        }

        var newEmail = await sendNewEmailRequestResult.Content.ReadFromJsonAsync<JsonObject>();
        var emailId = newEmail?["activityid"];

        csvFile = System.Convert.ToBase64String(new ASCIIEncoding().GetBytes(csvFile));

        var attachmentObject = new JsonObject() { 
                            { "objectid_email@odata.bind", $"/emails({emailId})"},
                            { "objecttypecode", "email"},
                            { "filename", csvfileName},
                            { "mimetype", "application/octet-stream"},
                            {"body", csvFile}    
        };

        var body = JsonSerializer.Serialize(attachmentObject);
        //Send Attachment request 
        var sendAttachmentRequestResult = await d365WebApiService.SendCreateRequestAsync(_appUserService.AZSystemAppUser, "activitymimeattachments", JsonSerializer.Serialize(attachmentObject));

        if (!sendAttachmentRequestResult.IsSuccessStatusCode)
        {
            var responseBody = await sendAttachmentRequestResult.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to create the record with the server error {responseBody}", responseBody.CleanLog());
        }
        #endregion

        #region Step 4: Send Email Notification
        
        var updateEmailData = new JsonObject
            {
                {"IssueSend", true }
            };

        var updateEmailRequestResult = await d365WebApiService.SendCreateRequestAsync(_appUserService.AZSystemAppUser, $"emails({emailId})/Microsoft.Dynamics.CRM.SendEmail", JsonSerializer.Serialize(updateEmailData));

        if (!updateEmailRequestResult.IsSuccessStatusCode)
        {
            var responseBody = await updateEmailRequestResult.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to update the record with the server error {responseBody}", responseBody.CleanLog());
        }

        #endregion

        var endTime = _timeProvider.GetTimestamp();

        var result = ProcessResult.Success(ProcessId, 1);
        _logger.LogInformation(CustomLogEvent.Process, "Send CAS Daily Report process finished in {totalElapsedTime} minutes. Result {result}", _timeProvider.GetElapsedTime(startTime, endTime).TotalMinutes, JsonValue.Create(result)!.ToString());
        return result.SimpleProcessResult;

    }
}