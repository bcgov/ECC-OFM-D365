using ECC.Core.DataContext;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using OFM.Infrastructure.WebAPI.Models;
using Microsoft.Extensions.Options;
using SelectPdf;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IEmailRepository
{
    Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync();
    Task<Guid?> CreateAndUpdateEmail(string subject, string emailDescription, List<Guid> toRecipient, Guid? senderId, string communicationType, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, Int16 processId);
    Task<ProcessData> GetTemplateDataAsync(int templateNumber);
    string StripHTML(string source);
    Task<bool> CreateAllowanceEmail(SupplementaryApplication allowance, Guid? senderId, string communicationType, Int16 processId, ID365WebApiService d365WebApiService);
    Task<JsonObject> NotificationSentSupp(Guid allowanceId, ID365WebApiService d365WebApiService, Int16 processId);

}

public class EmailRepository(ID365AppUserService appUserService, ID365WebApiService service, ID365DataService dataService, ILoggerFactory loggerFactory, IOptionsSnapshot<NotificationSettings> notificationSettings) : IEmailRepository
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
    private readonly ID365DataService _dataService = dataService;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;
    private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
    private int _templateNumber;
    Guid? newEmailId;
    bool emailCreated = false;

    #region Pre-Defined Queries

    private string CommunicationTypeRequestUri
    {
        get
        {
            // For reference only
            var fetchXml = """
                            <fetch distinct="true" no-lock="true">
                              <entity name="ofm_communication_type">
                                <attribute name="ofm_communication_typeid" />
                                <attribute name="ofm_communication_type_number" />
                                <attribute name="statecode" />
                                <filter>
                                  <condition attribute="statecode" operator="eq" value="0" />
                                </filter>
                              </entity>
                            </fetch>
                """;

            var requestUri = $"""
                              ofm_communication_types?fetchXml={WebUtility.UrlEncode(fetchXml)}
                              """;

            return requestUri;
        }
    }

    private string TemplatetoRetrieveUri
    {
        get
        {
            var fetchXml = $"""
                <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                  <entity name="template">
                    <attribute name="title" />
                    <attribute name="subjectsafehtml" />
                    <attribute name="templatetypecode" />
                    <attribute name="safehtml" />
                    <attribute name="subjectsafehtml" />        
                    <attribute name="languagecode" />
                    <attribute name="templateid" />
                    <attribute name="description" />
                    <attribute name="body" />
                    <order attribute="title" descending="false" />
                    <filter type="or">
                      <condition attribute="ccof_templateid" operator="eq"  uitype="template" value="{_templateNumber}" />
                          </filter>
                  </entity>
                </fetch>
                """;

            var requestUri = $"""
                            templates?fetchXml={WebUtility.UrlEncode(fetchXml)}
                            """;

            return requestUri.CleanCRLF();
        }
    }
    #endregion

    public async Task<ProcessData> GetTemplateDataAsync(int templateNumber)
    {
        _templateNumber = templateNumber;
        _logger.LogDebug(CustomLogEvent.Process, "Calling GetTemplateToSendEmail");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TemplatetoRetrieveUri);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Emmail Template to update with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No template found with query {requestUri}", TemplatetoRetrieveUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }

    public async Task<IEnumerable<D365CommunicationType>> LoadCommunicationTypeAsync()
    {
        var localdata = await _dataService.FetchDataAsync(CommunicationTypeRequestUri, "CommunicationTypes");
        var deserializedData = localdata.Data.Deserialize<List<D365CommunicationType>>(Setup.s_writeOptionsForLogs);

        return await Task.FromResult(deserializedData!);
    }


    #region Create and Update Email

    public async Task<Guid?> CreateAndUpdateEmail(string subject, string emailDescription, List<Guid> toRecipient, Guid? senderId, string communicationType, ID365AppUserService appUserService, ID365WebApiService d365WebApiService, Int16 processId)
    {
        toRecipient.ForEach(async recipient =>
        {
            var requestBody = new JsonObject(){
                            {"subject",subject },
                            {"description",emailDescription },
                            {"email_activity_parties", new JsonArray(){
                                new JsonObject
                                {
                                    { "partyid_systemuser@odata.bind", $"/systemusers({senderId})"},
                                    { "participationtypemask", 1 } //From Email
                                },
                                new JsonObject
                                {
                                    { "partyid_contact@odata.bind", $"/contacts({recipient})" },
                                    { "participationtypemask",   2 } //To Email                             
                                }
                            }},
                            { "ofm_communication_type_Email@odata.bind", $"/ofm_communication_types({communicationType})"}
                        };

            var response = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, "emails", requestBody.ToString());

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to create the record with the server error {responseBody}", responseBody.CleanLog());

                return;
            }
            else
            {

                var newEmail = await response.Content.ReadFromJsonAsync<JsonObject>();
                newEmailId = (Guid)newEmail?["activityid"];

                var emailStatement = $"emails({newEmailId})";

                var payload = new JsonObject {
                        { "ofm_sent_on", DateTime.UtcNow },
                        { "statuscode", (int) Email_StatusCode.Completed },
                        { "statecode", (int) email_statecode.Completed }};

                var requestBody1 = JsonSerializer.Serialize(payload);

                var patchResponse = await d365WebApiService.SendPatchRequestAsync(appUserService.AZSystemAppUser, emailStatement, requestBody1);

                if (!patchResponse.IsSuccessStatusCode)
                {
                    var responseBody = await patchResponse.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());

                    return;
                }
            }
        });


        return await Task.FromResult(newEmailId);
    }
    public string StripHTML(string source)
    {
        try
        {
            string result;
            // Remove HTML Development formatting
            // Replace line breaks with space
            // because browsers inserts space
            result = source.Replace("\r", " ");
            // Replace line breaks with space
            // because browsers inserts space
            result = result.Replace("\n", " ");
            // Remove step-formatting
            result = result.Replace("\t", string.Empty);
            // Remove repeating spaces because browsers ignore them
            result = System.Text.RegularExpressions.Regex.Replace(result,
     @"( )+", " ");
            // Remove the header (prepare first by clearing attributes)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*head([^>])*>", "<head>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<( )*(/)( )*head( )*>)", "</head>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, "(<head>).*(</head>)", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // remove all scripts (prepare first by clearing attributes)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*script([^>])*>", "<script>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<( )*(/)( )*script( )*>)", "</script>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            //result = System.Text.RegularExpressions.Regex.Replace(result,
            //@"(<script>)([^(<script>\.</script>)])*(</script>)",
            //string.Empty,
            // System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<script>).*(</script>)", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // remove all styles (prepare first by clearing attributes)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*style([^>])*>", "<style>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(<( )*(/)( )*style( )*>)", "</style>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, "(<style>).*(</style>)", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // insert tabs in spaces of <td> tags
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*td([^>])*>", "\t", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // insert line breaks in places of <BR> and <LI> tags
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*br( )*>", "\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*li( )*>", "\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // insert line paragraphs (double line breaks) in place
            // if <P>, <DIV> and <TR> tags
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*div([^>])*>", "\r\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*tr([^>])*>", "\r\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<( )*p([^>])*>", "\r\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove remaining tags like <a>, links, images,
            // comments etc - anything that's enclosed inside < >
            result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]*>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // replace special characters:
            result = System.Text.RegularExpressions.Regex.Replace(result, @" ", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&bull;", " * ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&lsaquo;", "<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&rsaquo;", ">", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&trade;", "(tm)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&frasl;", "/", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&lt;", "<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&gt;", ">", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&copy;", "(c)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&reg;", "(r)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove all others.
            result = System.Text.RegularExpressions.Regex.Replace(result, @"&(.{2,6});", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // for testing
            // System.Text.RegularExpressions.Regex.Replace(result,
            //this.txtRegex.Text,string.Empty,
            //       System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // make line breaking consistent
            result = result.Replace("\n", "\r");
            // Remove extra line breaks and tabs:
            // replace over 2 breaks with 2 and over 4 tabs with 4.
            // Prepare first to remove any whitespaces in between
            // the escaped characters and remove redundant tabs in between line breaks
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\r)( )+(\r)", "\r\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\t)( )+(\t)", "\t\t", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\t)( )+(\r)", "\t\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\r)( )+(\t)", "\r\t", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove redundant tabs
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\r)(\t)+(\r)", "\r\r", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Remove multiple tabs following a line break with just one tab
            result = System.Text.RegularExpressions.Regex.Replace(result, "(\r)(\t)+", "\r\t", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            //Initial replacement target string for line breaks
            string breaks = "\r\r\r";
            // Initial replacement target string for tabs
            string tabs = "\t\t\t\t\t";
            for (int index = 0; index < result.Length; index++)
            {
                result = result.Replace(breaks, "\r\r");
                result = result.Replace(tabs, "\t\t\t\t");
                breaks = breaks + "\r";
                tabs = tabs + "\t";
            }
            // That's it.
            return result;
        }
        catch
        {
            //MessageBox.Show("Error");
            return source;
        }
    }

    public async Task<bool> CreateAllowanceEmail(SupplementaryApplication allowance, Guid? senderId, string communicationType, Int16 processId, ID365WebApiService d365WebApiService)
    {

        var contactName = allowance.ofm_first_name + " " + allowance.ofm_last_name;
        var MonthlyAmount = allowance.ofm_monthly_amount;
        var RetroActiveAmount = allowance.ofm_retroactive_amount;
        var allowanceType = (ecc_allowance_type)allowance.ofm_allowance_type;
        var allowanceNumber = allowance.ofm_allowance_number;
        var allownaceStatusReason = allowance.statuscode;
        Guid applicationPrimaryContact = (Guid)allowance._ofm_contact_value != null ? (Guid)allowance._ofm_contact_value : Guid.Empty;
        Guid submittedBy = allowance._ofm_summary_submittedby_value != null ? (Guid)allowance._ofm_summary_submittedby_value : Guid.Empty;
        var fundingNumber = allowance.ofm_funding_number_base;
        var effectiveDate = allowance.ofm_start_date;
        var retroActiveDate = allowance.ofm_retroactive_date;
        var VIN = allowance.ofm_transport_vehicle_vin;
        // DateOnly retroActiveDate = allowance.ofm_ret;

        // funding status
        _logger.LogInformation("Got the Status", allownaceStatusReason);



        //_allowanceId = _processParams.SupplementaryApplication.allowanceId;
        if (allownaceStatusReason == (int)ofm_allowance_StatusCode.Approved)
        {
            ProcessData localDataTemplate = null;


            _logger.LogInformation("Entered if Approved", allownaceStatusReason);
            if (allowanceType == ecc_allowance_type.SupportNeedsProgramming)
                // Get template details to create emails.
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "SupportNeedsProgramAllowanceApproved").TemplateNumber); //240
            else if (allowanceType == ecc_allowance_type.IndigenousProgramming)
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "IndigenousAllowanceApproved").TemplateNumber);//255
            else if (allowanceType == ecc_allowance_type.Transportation && RetroActiveAmount > 0)
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "TransportationAllowanceApprovedwithRetroActive").TemplateNumber);//260
            else if (allowanceType == ecc_allowance_type.Transportation && (RetroActiveAmount == 0 || RetroActiveAmount == null))
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "TransportationAllowanceApprovedwithoutRetroActive").TemplateNumber);//290

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

            var templateobj = serializedDataTemplate?.FirstOrDefault();

            string? subject = (string)templateobj?.subjectsafehtml;
            subject = subject.Replace("#FANumber#", fundingNumber);
            subject = StripHTML(subject);

            string? emaildescription = templateobj?.safehtml;
            emaildescription = emaildescription?.Replace("[PrimaryContactName]", contactName);
            emaildescription = emaildescription?.Replace("{Amount}", MonthlyAmount?.ToString());

            //emaildescription = emaildescription?.Replace("{AllowanceType}", allowanceType.ToString());
            if (allowanceType == ecc_allowance_type.Transportation)
            {
                emaildescription = emaildescription?.Replace("{AllowanceStartDate}", effectiveDate?.ToString("MM/dd/yyyy"));
                emaildescription = emaildescription?.Replace("{VINnumber}", VIN?.ToString());
                if (RetroActiveAmount > 0)
                {
                    emaildescription = emaildescription?.Replace("{retroactiveAmount}", RetroActiveAmount?.ToString());
                    emaildescription = emaildescription?.Replace("{retroactivedate}", retroActiveDate?.ToString("MM/dd/yyyy"));
                }
            }

            List<Guid> recipientsList = new List<Guid>();
            if (submittedBy != Guid.Empty)
            {
                _logger.LogInformation("Got the recipientsList submittedBy", submittedBy);
                recipientsList.Add(submittedBy);
            }
            if (submittedBy != applicationPrimaryContact)
            {
                recipientsList.Add(applicationPrimaryContact);
            }
            //Task.Run(() => CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId)).Wait();
            // var result = Task.Run(() => CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId)).Result;
            // await CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId);
            //CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId).GetAwaiter().GetResult();

            // if (task.Value != null)

            Guid? newEmailId = await CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId);
            if (newEmailId != null)
                await NotificationSentSupp(allowance.ofm_allowanceid, _d365webapiservice, processId);



        }
        if (allownaceStatusReason == (int)ofm_allowance_StatusCode.DeniedIneligible)
        {
            ProcessData localDataTemplate = null;
            if (allowanceType == ecc_allowance_type.SupportNeedsProgramming)
                // Get template details to create emails.
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "SupportNeedsProgramAllowanceDenied").TemplateNumber);//275
            else if (allowanceType == ecc_allowance_type.IndigenousProgramming)
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "IndigenousAllowanceDenied").TemplateNumber);//280
            else if (allowanceType == ecc_allowance_type.Transportation)
                localDataTemplate = await GetTemplateDataAsync(_notificationSettings.EmailTemplates.First(t => t.Description == "TransportationAllowanceDenied").TemplateNumber);//285

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            _logger.LogInformation("Got the Template", serializedDataTemplate.Count);

            var templateobj = serializedDataTemplate?.FirstOrDefault();

            string? subject = (string)templateobj?.subjectsafehtml;
            string? emaildescription = templateobj?.safehtml;

            List<Guid> recipientsList = new List<Guid>();
            if (submittedBy != Guid.Empty)
            {
                _logger.LogInformation("Got the recipientsList submittedBy", submittedBy);
                recipientsList.Add(submittedBy);
            }
            if (submittedBy != applicationPrimaryContact)
            {
                recipientsList.Add(applicationPrimaryContact);
            }
            await CreateAndUpdateEmail(subject, emaildescription, recipientsList, senderId, communicationType, appUserService, _d365webapiservice, processId);
            emailCreated = true;

        }
        #endregion Create the Supp email notifications


        return await Task.FromResult(emailCreated);
    }

    public async Task<JsonObject> NotificationSentSupp(Guid allowanceId, ID365WebApiService d365WebApiService, Int16 processId)
    {
        if (emailCreated)
        {
            var updateSupplementalUrl = @$"ofm_allowances({allowanceId})";
            var updateContent = new
            {
                ofm_notification_sent = true
            };
            var requestBody = JsonSerializer.Serialize(updateContent);
            var patchResponse = await d365WebApiService.SendPatchRequestAsync(_appUserService.AZSystemAppUser, updateSupplementalUrl, requestBody);

            _logger.LogDebug(CustomLogEvent.Process, "Update Supplemental Record {supplemental.ofm_allowanceid}", allowanceId);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var responseBody = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogError(CustomLogEvent.Process, "Failed to patch the record with the server error {responseBody}", responseBody.CleanLog());
                // return ProcessResult.Failure(ProcessId, new String[] { responseBody }, 0, 0).SimpleProcessResult;
            }
        }
        return ProcessResult.Completed(processId).SimpleProcessResult;
    }


}