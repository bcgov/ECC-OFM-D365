using ECC.Core.DataContext;
using Microsoft.Extensions.Options;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Payments
{
    public interface IPaymentValidator
    {
        Task<List<Guid>>? GetOpssupervisorEmail(bool ecflag = false);
        Task<List<string>>? GetccuserEmail(bool ecflag = false);
        Task<ProcessData> GetRecipientAsync(string RequestRecipent);
        Task<Dictionary<string, List<ValidationResult>>> AreAllPropertiesValid(List<PaymentLine> paymentLines);
        Task<JsonObject> SendPaymentErrorEmail(Int16 processId,string templateNumber, Guid senderId, Dictionary<string,List<ValidationResult>> invalidline=null);

    }

    public class PaymentValidator(ID365AppUserService appUserService, IEmailRepository emailRepository, ID365WebApiService service, ID365DataService dataService, ILoggerFactory loggerFactory, IOptionsSnapshot<NotificationSettings> notificationSettings) : IPaymentValidator
    {
        private readonly ID365DataService _dataService = dataService;
        private readonly ID365AppUserService _appUserService = appUserService;
        private readonly ID365WebApiService _d365webapiservice = service;
        private readonly NotificationSettings _notificationSettings = notificationSettings.Value;
        private readonly ILogger _logger = loggerFactory.CreateLogger(LogCategory.Process);
        private readonly IEmailRepository _emailRepository = emailRepository;
        List<Guid> opsuserID = [];
        List<string> ccuserID = [];
        Guid userId = new Guid();
        private ProcessData? _data;
        public string Requestccuser
        {
            get
            {
                // For reference only
                var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="contact">
                        <attribute name="fullname" />
                        <attribute name="telephone1" />
                        <attribute name="contactid" />
                        <attribute name="emailaddress1" />
                        <order attribute="fullname" descending="false" />
                        <filter type="and">
                          <condition attribute="ccof_username" operator="like" value="%OFM- CAS Contact%" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         contacts?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }

        public string RequestOpsSupervisor
        {
            get
            {
                // For reference only
                var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="systemuser">
                        <attribute name="systemuserid" />
                        <attribute name="internalemailaddress" />
                           <filter type="and">
                            <condition attribute="ofm_operational_supervisor" operator="eq" value="1" />
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         systemusers?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }

        public string RequestOpsandecUser
        {
            get
            {
                // For reference only
                var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                      <entity name="systemuser">
                        <attribute name="systemuserid" />
                        <attribute name="internalemailaddress" />
                           <filter type="or">
                            <condition attribute="ofm_operational_supervisor" operator="eq" value="1" />
                           <condition attribute="ofm_is_expense_authority" operator="eq" value="1" />                       
                        </filter>
                      </entity>
                    </fetch>
                    """;

                var requestUri = $"""
                         systemusers?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

                return requestUri;
            }
        }

        public async Task<ProcessData> GetRecipientAsync(string RequestRecipent)
        {
            HttpResponseMessage response;
            _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P500SendPaymentRequestProvider));
           
            response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, RequestRecipent, isProcess: true);

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

        public async Task<List<Guid>> GetOpssupervisorEmail(bool ecflag=false)
        {
            var opsuser = ecflag ? await GetRecipientAsync(RequestOpsandecUser) : await GetRecipientAsync(RequestOpsSupervisor);

            #region Get Ops user email and check if it is in email safelist
            var  opsuseremail = JsonSerializer.Deserialize<List<User>>(opsuser.Data.ToString());

            opsuseremail?.ForEach(user =>
            {
                userId = user.systemuserid;
                if (_notificationSettings.EmailSafeList.Enable &&
                    !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(user.internalemailaddress?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
                {
                    userId = new Guid(_notificationSettings.EmailSafeList.DefaultUserId);
                }
                opsuserID.Add(userId);
            });
            return await Task.FromResult(opsuserID);
            #endregion

        }

        public async Task<List<string>> GetccuserEmail(bool ecflag=false)
        {
            var ccuser = ecflag ? await GetRecipientAsync(Requestccuser) : null; 

            #region Get cc contact email and check if it is in email safelist
            var ccuseremail = JsonSerializer.Deserialize<List<Contact>>(ccuser.Data.ToString());
            List<string> ccList = [];
            string? contactId = null;
            // foreach contact, check if the email address is on the safe list configured on the appsettings, if yes then carry on, else replace the email with a default email address
            ccuseremail.ForEach(contact =>
            {
                contactId = contact.contactid.ToString();
                if (_notificationSettings.EmailSafeList.Enable &&
                    !_notificationSettings.EmailSafeList.Recipients.Any(x => x.Equals(contact.emailaddress1?.Trim(';'), StringComparison.CurrentCultureIgnoreCase)))
                {
                    contactId = _notificationSettings.EmailSafeList.DefaultContactId;
                }
                ccuserID.Add(contact.emailaddress1);
            });
            
            return await Task.FromResult(ccuserID);
            #endregion

        }

        public Task<Dictionary<string, List<ValidationResult>>> AreAllPropertiesValid(List<PaymentLine> paymentLines)
        {
            List<List<ValidationResult>> invalidLine = new List<List<ValidationResult>>();
            var nullFieldsByIndex = new Dictionary<string, List<ValidationResult>>();

            foreach (PaymentLine paymentLine in paymentLines)
            {

                var validationResults = new List<ValidationResult>();
                var validationContext = new ValidationContext(paymentLine);

                if (!Validator.TryValidateObject(paymentLine, validationContext, validationResults, true))
                {
                    foreach (var error in validationResults)
                    {
                        nullFieldsByIndex[paymentLine.ofm_invoice_number] = validationResults;


                    }
                }

               // return Task.FromResult(nullFieldsByIndex);




            }
            return Task.FromResult(nullFieldsByIndex);

        }

        public async Task<JsonObject> SendPaymentErrorEmail(Int16 processId,string templateNumber,  Guid senderId, Dictionary<string,List<ValidationResult>> invalidline=null)
        {
             
            var localDataTemplate = await _emailRepository.GetTemplateDataAsync(Int32.Parse(templateNumber));

            var serializedDataTemplate = JsonSerializer.Deserialize<List<D365Template>>(localDataTemplate.Data.ToString());
            _logger.LogInformation("Got the Template", serializedDataTemplate.Count);
            string? emailBody = string.Empty;

            var templateobj = serializedDataTemplate?.FirstOrDefault();
            string? subject = _emailRepository.StripHTML(templateobj?.subjectsafehtml);
            string? emaildescription = templateobj?.safehtml;
            StringBuilder paymentTable = new StringBuilder();
            IEnumerable<D365CommunicationType> _communicationType = await _emailRepository!.LoadCommunicationTypeAsync();
            var _informationCommunicationType = _communicationType.Where(c => c.ofm_communication_type_number == _notificationSettings.CommunicationTypes.ActionRequired).Select(s => s.ofm_communication_typeid).FirstOrDefault();

            var emailParties = new JsonArray()
           {
            new JsonObject
            {
             { "partyid_systemuser@odata.bind", $"/systemusers({senderId})" },
             { "participationtypemask", 1 } // From Email
            },

           };

          opsuserID.Distinct().ToList().ForEach(recipient =>
          {emailParties.Add(new JsonObject
            {
             { "partyid_systemuser@odata.bind",  $"/systemusers({recipient})"},
             { "participationtypemask", 2 } // To Email
           });
          });

            ccuserID.Distinct().ToList().ForEach(ccrecipient =>
            {
                emailParties.Add(new JsonObject
            {
             { "addressused",  $"{ccrecipient}"},
             { "participationtypemask", 3 } // To Email
           });
            });


            if (invalidline!=null)
            {
                paymentTable.Append("<div style=\" overflow: auto;width: 50%;\" role=\"region\" tabindex=\"0\"><table style=\"text-align: center;border: 1px solid #dededf;    height: 100%;    width: 100%;    table-layout: fixed;    border-collapse: collapse;    border-spacing: 1px;    text-align: left;\" border=\"1\" border-style=\"solid\" rules=\"all\" width=\"100%\"><thead bgcolor=\"#eceff1\"><tr><th width=\"40%\" style=\"text-align: center;border: 1px solid #dededf;background-color: #eceff1;color: #000000;padding: 5px;\">Invoice Number</th><th width=\"30%\" style=\"text-align: center;border: 1px solid #dededf;background-color: #eceff1;color: #000000;padding: 5px;\">Errors</th></thead><tbody>");

                                                            
                foreach (var error in invalidline)
                {
                    string errorMessages = string.Join(";\n", error.Value.Select(v => v.ErrorMessage));
                    paymentTable.Append($"<tr><td align=\"center\" style=\"text-align: center;border: 1px solid #dededf;background-color: #ffffff;   color: #000000;    padding: 5px;\">{error.Key}</td><td align=\"center\"  style=\"text-align: center;border: 1px solid #dededf;    background-color: #ffffff;    color: #000000;    padding: 5px;\"> {errorMessages}</td></tr>");
                }
                paymentTable.Append("</tbody></table></div>");
                emailBody = emaildescription?.Replace("[payment]", paymentTable.ToString());
                var regardinddata = string.Format("ofm_paymentlines");
            }
            else
            {

                emailBody = emaildescription?.Replace("[Date]", DateTime.UtcNow.ToLocalPST().Date.ToString("MM/dd/yyyy"));
            }
            await _emailRepository.CreateAndSendEmail(subject, emailBody.ToString(), emailParties, _informationCommunicationType, appUserService, _d365webapiservice, 530, "paymentlines");

            return ProcessResult.Completed(processId).SimpleProcessResult;
        }


    }
}
