using ECC.Core.DataContext;
using Microsoft.Extensions.Hosting;
using Microsoft.Xrm.Sdk;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using CreateRequest = OFM.Infrastructure.WebAPI.Messages.CreateRequest;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Reporting;

public class P610CreateQuestionProvider : ID365ProcessProvider
{
    const string EntityNameSet = "ofm_question";
    private readonly ID365AppUserService _appUserService;
    private readonly ID365WebApiService _d365webapiservice;
    private readonly IOrganizationService _service;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private ProcessData? _data;
    private ProcessParameter? _processParams;
    private string _requestUri = string.Empty;
    private Guid? _sectionId;
    private string[] _questionIdentifier;
    private string[] _surveyIdentifier;

    public P610CreateQuestionProvider(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ILoggerFactory loggerFactory, TimeProvider timeProvider)
    {
        _appUserService = appUserService;
        _d365webapiservice = d365WebApiService;
        _logger = loggerFactory.CreateLogger(LogCategory.Process);
        _timeProvider = timeProvider;
    }

    public Int16 ProcessId => Setup.Process.Reporting.CreateUpdateQuestionId;
    public string ProcessName => Setup.Process.Reporting.CreateUpdateQuestionName;
    public string RequestUri
    {
        
        get
        {
            var projectId = _processParams?.Project?.ProjectId;
           
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" >
                        <entity name="msfp_project" alias="template" >
                            <attribute name="msfp_name" />
                            <attribute name="msfp_projectid" />
                            <filter type="and" >
                               <condition attribute="msfp_projectid" operator="eq" value="{projectId}" />
                            </filter>
                            <link-entity name="msfp_survey" from="msfp_project" to="msfp_projectid" alias="section" link-type="inner" >
                                <attribute name="msfp_name" />
                                <attribute name="msfp_project" />
                                <attribute name="msfp_sourcesurveyidentifier" />
                                <attribute name="msfp_surveyid" />
                                  <filter type="and" >
                                        <condition attribute="statecode" operator="eq" value="0" />
                                    </filter>
                                <link-entity name="msfp_question" from="msfp_survey" to="msfp_surveyid" alias="questions" link-type="inner" >
                                    <attribute name="msfp_questionid" />
                                    <attribute name="msfp_name" />
                                    <attribute name="createdon" />
                                    <attribute name="modifiedon" />
                                    <attribute name="msfp_survey" />
                                    <attribute name="msfp_subtitle" />
                                    <attribute name="msfp_sourcesurveyidentifier" />
                                    <attribute name="msfp_sourcequestionidentifier" />
                                    <attribute name="msfp_sequence" />
                                    <attribute name="msfp_responserequired" />
                                    <attribute name="msfp_questiontype" />
                                    <attribute name="msfp_questiontext" />
                                    <attribute name="msfp_questionchoices" />
                                    <attribute name="msfp_otherproperties" />
                                    <attribute name="msfp_imageproperties" />
                                    <attribute name="msfp_multiline" />
                                    <attribute name="msfp_choicetype" />
                                    <order attribute="modifiedon" descending="false" />
                                    <filter type="and" >
                                        <condition attribute="statecode" operator="eq" value="0" />
                                    </filter>
                                </link-entity>
                            </link-entity>
                        </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         msfp_projects?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    public string TableRequestUri
    {
        get
        {
            var fetchXml = $"""
                    <fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false">
                    <entity name="ofm_question">
                    <attribute name="ofm_questionid" />
                    <attribute name="ofm_name" />
                    <attribute name="createdon" />
                    <attribute name="ofm_subtitle" />
                    <order attribute="ofm_name" descending="false" />
                    <filter type="and">
                    <condition attribute="ofm_question_type" operator="eq" value="{(int)ofm_ReportingQuestionType.Table}" />
                    <condition attribute="ofm_section" operator="eq" value = "{_sectionId}"/>
                    </filter>
                    </entity>
                    </fetch>
                    """;

            var requestUri = $"""
                         ofm_questions?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }

    public string QuestionBusinessRuleRequestUri
    {
        get
        {
            var fetchXml =
                     @"<fetch>
                    <entity name='ofm_question_business_rule'>
                    <attribute name='ofm_name' />
                    <attribute name='ofm_false_child_question' />
                    <attribute name='ofm_true_child_question' />
                    <attribute name='ofm_parent_has_response' />
                    <attribute name='ofm_business_rule_type' />
                    <attribute name='ofm_condition' />
                    <attribute name='ofm_section' />
                    <attribute name='ofm_child_question' />

                    <link-entity name='ofm_question' from='ofm_questionid' to='ofm_parentquestionid' link-type='inner' alias='que' intersect='true' >
                    <attribute name='ofm_source_question_id' />
                    <order attribute='createdon' descending='true' /> 
                    <filter>
                    <condition attribute='ofm_source_question_id' operator='in'>";
            for (var i = 0; i < _questionIdentifier.Length; i++)
            {
                fetchXml += "<value>" + _questionIdentifier[i] + "</value>";
            }
            fetchXml += @"</condition></filter>

                    </link-entity>
                    <link-entity name='ofm_question' from='ofm_questionid' to='ofm_true_child_question' link-type='outer' alias='true'>
                    <attribute name='ofm_source_question_id' />
                    </link-entity>
                    <link-entity name='ofm_question' from='ofm_questionid' to='ofm_false_child_question' link-type='outer' alias='false'>
                    <attribute name='ofm_source_question_id' />
                    </link-entity>
                    <link-entity name='ofm_question' from='ofm_questionid' to='ofm_child_question' link-type='outer' alias='child'>
                    <attribute name='ofm_source_question_id' />
                    </link-entity>
                    </entity>
                    </fetch>";


            var requestUri = $"""
                         ofm_question_business_rules?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    public string QuestionRequestUri
    {
        get
        {
              var fetchXml =
                    @"<fetch>
                      <entity name='ofm_question'>
                        <attribute name='ofm_questionid' />
                        <attribute name='ofm_question_id' />
                        <attribute name='ofm_default_rows' />
                        <attribute name='ofm_occurence' />
                        <attribute name='ofm_maximum_rows' />
                        <attribute name='ofm_fixed_response' />
                        <attribute name='ofm_source_question_id' />
                        <filter>
                          <condition attribute='ofm_source_question_id' operator='in'>";
            for (var i = 0; i < _questionIdentifier.Length; i++)
            {
                fetchXml += "<value>" + _questionIdentifier[i] + "</value>";
            }
            fetchXml += @"</condition></filter>
                       <link-entity name='ofm_section' from='ofm_sectionid' to='ofm_section' alias='section'>
                          <link-entity name='ofm_survey' from='ofm_surveyid' to='ofm_survey' alias='survey' >
                            <attribute name='ofm_is_published' />
                            <attribute name='createdon' />
                            <attribute name='statecode' />
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>";




            var requestUri = $"""
                         ofm_questions?fetchXml={WebUtility.UrlEncode(fetchXml)}
                         """;

            return requestUri;
        }
    }
    

    public async Task<ProcessData> GetDataAsync()
    {

        _logger.LogDebug(CustomLogEvent.Process, "Calling GetData of {nameof}", nameof(P610CreateQuestionProvider));

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
    private async Task<ProcessData> GetTableDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetTableDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, TableRequestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Question with Table Type records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Question with Table Type records found with query {requestUri}", TableRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }
    private async Task<ProcessData> GetBRDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetBRDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, QuestionBusinessRuleRequestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Business Rule records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Business Rule  records found with query {requestUri}", QuestionBusinessRuleRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }
    private async Task<ProcessData> GetQuestionDataAsync()
    {
        _logger.LogDebug(CustomLogEvent.Process, "GetQuestionDataAsync");

        var response = await _d365webapiservice.SendRetrieveRequestAsync(_appUserService.AZSystemAppUser, QuestionRequestUri, isProcess: true);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(CustomLogEvent.Process, "Failed to query Question records with the server error {responseBody}", responseBody.CleanLog());

            return await Task.FromResult(new ProcessData(string.Empty));
        }

        var jsonObject = await response.Content.ReadFromJsonAsync<JsonObject>();

        JsonNode d365Result = string.Empty;
        if (jsonObject?.TryGetPropertyValue("value", out var currentValue) == true)
        {
            if (currentValue?.AsArray().Count == 0)
            {
                _logger.LogInformation(CustomLogEvent.Process, "No Question  records found with query {requestUri}", QuestionRequestUri.CleanLog());
            }
            d365Result = currentValue!;
        }

        _logger.LogDebug(CustomLogEvent.Process, "Query Result {queryResult}", d365Result.ToString().CleanLog());

        return await Task.FromResult(new ProcessData(d365Result));
    }
   

    public async Task<JsonObject> RunProcessAsync(ID365AppUserService appUserService, ID365WebApiService d365WebApiService, ProcessParameter processParams)
    {
        _processParams = processParams;
        var startTime = _timeProvider.GetTimestamp();
        try
        {
            var localData = await GetDataAsync();
            var deserializedData = JsonSerializer.Deserialize<List<D365Reporting>>(localData.Data.ToString());
            var questionIdentifiers = deserializedData.Select(q => q.QuestionSourcequestionIdentifier).ToList();


            _questionIdentifier = questionIdentifiers.ToArray();


            #region Create Customer Voice Project as Report Template in CE Custom
            if (deserializedData.Count > 0)
            {
                var payloadReportTemplate = new JsonObject
            {
                {ofm_survey.Fields.ofm_customervoiceprojectid,deserializedData.First().msfp_projectid },
                {ofm_survey.Fields.ofm_name,deserializedData.First().msfp_name }

            };

                var requestBodyReportTemplate = JsonSerializer.Serialize(payloadReportTemplate);
                var CreateResponserequestBodyReportTemplate = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_survey.EntitySetName, requestBodyReportTemplate);
                if (!CreateResponserequestBodyReportTemplate.IsSuccessStatusCode)
                {
                    var responseBody = await CreateResponserequestBodyReportTemplate.Content.ReadAsStringAsync();
                    _logger.LogError(CustomLogEvent.Process, "Failed to create the Report Template record with the server error {responseBody}", responseBody.CleanLog());

                    return ProcessResult.Failure(ProcessId, new string[] { responseBody }, 0, 0).SimpleProcessResult;
                }

                var newReportTemplate = await CreateResponserequestBodyReportTemplate.Content.ReadFromJsonAsync<JsonObject>();
                var newReportTemplateId = newReportTemplate?["ofm_surveyid"];

                #endregion Create Customer Voice Project as Report Template in CE Custom

                #region Create Customer Voice Survey as Section in CE Custom

                var sections = _processParams?.ReportSections;
                var deserializedsectionOrder = JsonSerializer.Deserialize<List<D365Reporting>>(sections.ToString());

                var surveyDataGroup = deserializedData.GroupBy(s => s.SectionSurveyId);
                var reportSectionOrder = deserializedsectionOrder?.OrderBy(x => x.SectionName);

                List<HttpRequestMessage> requestsQuestionCreation = new() { };
                List<HttpRequestMessage> requestsQuestionCreationTable = new() { };
                List<HttpRequestMessage> requestsQuestionCreationColumn = new() { };

                foreach (var survey in surveyDataGroup)
                {

                    var payloadSurvey = new JsonObject
                        {
                            {ofm_section.Fields.ofm_name,survey.First().CVSectionName },
                             {ofm_section.Fields.ofm_section_order,reportSectionOrder.Where(x => x.SectionName == survey.First().CVSectionName).Select(x => x.OrderNumber).First() },
                            {ofm_section.Fields.ofm_section_title,survey.First().CVSectionName },
                            {ofm_section.Fields.ofm_source_section_id,survey.First().SectionSourceSurveyIdentifier },
                            {ofm_section.Fields.ofm_customer_voice_survey_id,survey.First().SectionSurveyId },
                            {ofm_section.Fields.ofm_survey+"@odata.bind",$"/{ofm_survey.EntitySetName}({newReportTemplateId})" }
                        };
                    var requestBodySection = JsonSerializer.Serialize(payloadSurvey);
                    var CreateResponseSection = await d365WebApiService.SendCreateRequestAsync(appUserService.AZSystemAppUser, ofm_section.EntitySetName, requestBodySection);
                    if (!CreateResponseSection.IsSuccessStatusCode)
                    {
                        var responseBody = await CreateResponseSection.Content.ReadAsStringAsync();
                        _logger.LogError(CustomLogEvent.Process, "Failed to create the Section record with the server error {responseBody}", responseBody.CleanLog());
                        _logger.LogError(CustomLogEvent.Process, "Report Template is Created with {Report Template ID}", newReportTemplateId);
                        return ProcessResult.Failure(ProcessId, new string[] { responseBody }, 0, 0).SimpleProcessResult;
                    }

                    var newSection = await CreateResponseSection.Content.ReadFromJsonAsync<JsonObject>();
                    var newSectionId = newSection?["ofm_sectionid"];
                    _sectionId = (Guid?)(newSectionId);

                    #endregion Create Customer Voice Survey as Section in CE Customm

                    #region Create Questions

                    // var surveyQuestions = survey.OrderByDescending(x => x.QuestionSubtitle).GroupBy(x => x.QuestionSubtitle).SelectMany(g => g);

                    var surveyQuestionTable = survey.Where(g => g.QuestionSubtitle != null && g.QuestionSubtitle.ToLower().Contains("table")).Select(g => g).ToList();
                    var surveyQuestioColumn = survey.Where(g => g.QuestionSubtitle != null && g.QuestionSubtitle.ToLower().Contains("column")).Select(g => g).ToList();
                    var surveyQuestions = survey.Where(g => (g.QuestionSubtitle == null) || (!g.QuestionSubtitle.ToLower().Contains("table") && !g.QuestionSubtitle.ToLower().Contains("column"))).Select(g => g).ToList();


                    var entitySetNameQuestion = ofm_question.EntitySetName;
                    List<ofm_question> deserializedDataQuestion = null;

                    foreach (var question in surveyQuestions)
                    {

                        requestsQuestionCreation.Add(new CreateRequest($"{entitySetNameQuestion}",
                            CreateJsonObject(question)));

                    }

                    if (requestsQuestionCreation.Count > 0)
                    {

                        var createQuestionBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requestsQuestionCreation, null);

                        if (createQuestionBatchResult.Errors.Any())
                        {
                            var createQuestionError = ProcessResult.Failure(ProcessId, createQuestionBatchResult.Errors, createQuestionBatchResult.TotalProcessed, createQuestionBatchResult.TotalRecords);
                            _logger.LogError(CustomLogEvent.Process, "Failed to create Question with an error: {error}", JsonValue.Create(createQuestionError)!.ToString());
                            _logger.LogError(CustomLogEvent.Process, "Report Template is Created with {Report Template ID}", newReportTemplateId);
                            _logger.LogError(CustomLogEvent.Process, "Report Section is Created with {Report Section ID}", _sectionId);
                            return createQuestionError.SimpleProcessResult;

                        }
                        requestsQuestionCreation.Clear();
                    }


                    foreach (var table in surveyQuestionTable)
                    {
                        requestsQuestionCreationTable.Add(new CreateRequest($"{entitySetNameQuestion}",
                               CreateJsonObject(table)));

                    }
                    if (requestsQuestionCreationTable.Count > 0)
                    {

                        var createTableQuestionBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requestsQuestionCreationTable, null);

                        if (createTableQuestionBatchResult.Errors.Any())
                        {
                            var createQuestionTableError = ProcessResult.Failure(ProcessId, createTableQuestionBatchResult.Errors, createTableQuestionBatchResult.TotalProcessed, createTableQuestionBatchResult.TotalRecords);
                            _logger.LogError(CustomLogEvent.Process, "Failed to create Table type Question with an error: {error}", JsonValue.Create(createQuestionTableError)!.ToString());
                            _logger.LogError(CustomLogEvent.Process, "Report Template is Created with {Report Template ID}", newReportTemplateId);
                            _logger.LogError(CustomLogEvent.Process, "Report Section is Created with {Report Section ID}", _sectionId);
                            return createQuestionTableError.SimpleProcessResult;


                        }
                        requestsQuestionCreationTable.Clear();
                        var localdataTable = await GetTableDataAsync();
                        deserializedDataQuestion = JsonSerializer.Deserialize<List<ofm_question>>(localdataTable.Data.ToString());
                    }
                    

                    foreach (var column in surveyQuestioColumn)
                    {
                        var parentQuestion = deserializedDataQuestion.Where(q => q.ofm_subtitle.Trim() == column.QuestionText.Split('#')?[0].Trim()).Select(c => c.ofm_questionid ?? Guid.Empty).FirstOrDefault();

                        requestsQuestionCreationColumn.Add(new CreateRequest($"{entitySetNameQuestion}",
                                new JsonObject(){
                                 {ofm_question.Fields.ofm_name, column.QuestionName.Split('#')[1] },
                       { ofm_question.Fields.ofm_subtitle, column.QuestionSubtitle },
                            { ofm_question.Fields.ofm_source_question_id, column.QuestionSourcequestionIdentifier},
                           { ofm_question.Fields.ofm_choice_type,column.QuestionChoiceType},
                            { ofm_question.Fields.ofm_multiple_line, column.QuestionMultiline},
                        {ofm_question.Fields.ofm_question_choice,column.QuestionChoices },
                         {ofm_question.Fields.ofm_question_text,column.QuestionText.Split('#')[1] },
                          {ofm_question.Fields.ofm_response_required,column.QuestionresponseRequired },
                           {ofm_question.Fields.ofm_sequence,column.QuestionSequence },
                           {ofm_question.Fields.ofm_question_type,GetQuestionType(column) },
                            {ofm_question.Fields.ofm_section+"@odata.bind",$"/{ofm_section.EntitySetName}({newSectionId})" },
                                     {ofm_question.Fields.ofm_header+"@odata.bind",$"/{ofm_question.EntitySetName}({parentQuestion})"}
                                }));
                    }
                    if (requestsQuestionCreationColumn.Count > 0)
                    {
                        var createQuestionColumnBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requestsQuestionCreationColumn, null);

                        if (createQuestionColumnBatchResult.Errors.Any())
                        {
                            var createQuestionColumnError = ProcessResult.Failure(ProcessId, createQuestionColumnBatchResult.Errors, createQuestionColumnBatchResult.TotalProcessed, createQuestionColumnBatchResult.TotalRecords);
                            _logger.LogError(CustomLogEvent.Process, "Failed to create Question with an error: {error}", JsonValue.Create(createQuestionColumnError)!.ToString());
                            _logger.LogError(CustomLogEvent.Process, "Report Template is Created with {Report Template ID}", newReportTemplateId);
                            _logger.LogError(CustomLogEvent.Process, "Report Section is Created with {Report Section ID}", _sectionId);
                           // return createQuestionColumnError.SimpleProcessResult;


                        }
                        requestsQuestionCreationColumn.Clear();
                    }
                   

                    #endregion Create Questions
                }

                UpdateQuestionwithManualData(questionIdentifiers, _sectionId, appUserService, d365WebApiService);

            }
        }

        catch (ValidationException exp)
        {
            _logger.LogError(CustomLogEvent.Process, "Failed under catch block RunProcessAsync", new[] { exp.Message, exp.StackTrace ?? string.Empty });

        }

        return ProcessResult.Completed(ProcessId).SimpleProcessResult;

    }
    public JsonObject CreateJsonObject(D365Reporting question)
    {
        var newRequest = new JsonObject(){
                                 {ofm_question.Fields.ofm_name, question.QuestionName },
                       { ofm_question.Fields.ofm_subtitle, question.QuestionSubtitle },
                            { ofm_question.Fields.ofm_source_question_id, question.QuestionSourcequestionIdentifier},
                           { ofm_question.Fields.ofm_choice_type,question.QuestionChoiceType},
                            { ofm_question.Fields.ofm_multiple_line, question.QuestionMultiline},
                        {ofm_question.Fields.ofm_question_choice,question.QuestionChoices },
                         {ofm_question.Fields.ofm_question_text,question.QuestionText },
                          {ofm_question.Fields.ofm_response_required,question.QuestionresponseRequired },
                           {ofm_question.Fields.ofm_sequence,question.QuestionSequence },
                           {ofm_question.Fields.ofm_question_type,GetQuestionType(question) },
                            {ofm_question.Fields.ofm_section+"@odata.bind",$"/{ofm_section.EntitySetName}({_sectionId})" },

                            };
        return newRequest;

    }
    public int GetQuestionType(D365Reporting question)
    {
         if (question.QuestionSubtitle != null && question.QuestionSubtitle.ToLower().Contains("table"))
            return (int)ofm_ReportingQuestionType.Table;
       else if ((bool)question.QuestionMultiline)
            return (int)ofm_ReportingQuestionType.TextArea;
        else if (question.QuestionChoiceType == (int)msfp_question_msfp_choicetype.Multichoice)
            return (int)ofm_ReportingQuestionType.MultipleChoice;
        else if (question.QuestionType == (int)msfp_question_msfp_questiontype.Number)
            return (int)ofm_ReportingQuestionType.Number;

        else if (question.QuestionChoices != null && question.QuestionChoices.Replace("\"", "") == "Yes,No")
        {
            return (int)ofm_ReportingQuestionType.TwoOption;
        }
        


        else
            return (int)(ofm_ReportingQuestionType)question.QuestionType;

    }
   
    public async Task<bool> UpdateQuestionwithManualData(IEnumerable<string?> questionIdentifiers, Guid? _sectionId, ID365AppUserService appUserService, ID365WebApiService d365WebApiService)
   {
        try
        {
           
            var questionData = await GetQuestionDataAsync();
            var deserializedQuestion = JsonSerializer.Deserialize<List<Question>>(questionData.Data.ToString());
            List<HttpRequestMessage> requestsQuestionUpdate = new() { };

            foreach (var identifier in questionIdentifiers)
            {

                var getLatestPublishedVersion = deserializedQuestion?.FirstOrDefault(x => x.surveyIsPublished == true && x.surveyStatecode == (int)ofm_survey_statecode.Active && x.ofm_source_question_id == identifier);
                var getQuestionToUpdate = deserializedQuestion?.FirstOrDefault(x => x.surveyIsPublished == false && x.surveyStatecode == (int)ofm_survey_statecode.Active && x.ofm_source_question_id == identifier);
                if (getLatestPublishedVersion != null && getQuestionToUpdate != null)
                {
                    requestsQuestionUpdate.Add(new D365UpdateRequest(new Messages.EntityReference(ofm_question.EntitySetName, getQuestionToUpdate.ofm_questionid),
                                       new JsonObject()
                                       {
                                            {ofm_question.Fields.ofm_default_rows, getLatestPublishedVersion.ofm_default_rows ?? null},
                                            {ofm_question.Fields.ofm_maximum_rows, getLatestPublishedVersion.ofm_maximum_rows ?? null},
                                            {ofm_question.Fields.ofm_occurence,(int)((getLatestPublishedVersion.ofm_occurence == null) ? ofm_question_ofm_occurence.Monthly:getLatestPublishedVersion.ofm_occurence) },
                                            {ofm_question.Fields.ofm_fixed_response, getLatestPublishedVersion.ofm_fixed_response }
                                       }));
                }




            }

            if (requestsQuestionUpdate.Count > 0)
            {
                var UpdateQuestionBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requestsQuestionUpdate, null);

                if (UpdateQuestionBatchResult.Errors.Any())
                {
                    var createQuestionBRError = ProcessResult.Failure(ProcessId, UpdateQuestionBatchResult.Errors, UpdateQuestionBatchResult.TotalProcessed, UpdateQuestionBatchResult.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to Update manual values on Question with an error: {error}", JsonValue.Create(createQuestionBRError)!.ToString());
                    _logger.LogError(CustomLogEvent.Process, "Report Section is Created with {Report Section ID}", _sectionId);


                }
                requestsQuestionUpdate.Clear();
                await CreateBusinessRule(_sectionId, appUserService, d365WebApiService, deserializedQuestion);
            }
        }
        catch (ValidationException exp)
        {
            _logger.LogError(CustomLogEvent.Process, "Failed under catch block UpdateQuestionwithManualData", new[] { exp.Message, exp.StackTrace ?? string.Empty });
            ProcessResult.Failure(ProcessId, new[] { exp.Message, exp.StackTrace ?? string.Empty }, 0, 0);
            return await Task.FromResult(false);
        }
        return await Task.FromResult(true);

    }
    public async Task<bool> CreateBusinessRule(Guid? _sectionId, ID365AppUserService appUserService, ID365WebApiService d365WebApiService,List<Question> deserializedQuestiondata)
    {
        try
        {
            List<HttpRequestMessage> requestsQuestionBRCreation = new() { };
            var entitySetNameQuestionBR = ofm_question_business_rule.EntitySetName;
            var localdataBusinessRule = await GetBRDataAsync();
            var deserializedDataBR = JsonSerializer.Deserialize<List<BRQuestion>>(localdataBusinessRule.Data.ToString());
           
            deserializedDataBR?.ForEach(br =>
            {
                var parentQuestionId = deserializedQuestiondata.FirstOrDefault(q => q.ofm_source_question_id == br.brSourceQuestion && q.surveyIsPublished == false && q.surveyStatecode == (int)ofm_survey_statecode.Active)?.ofm_questionid;
                var trueQuestionId = deserializedQuestiondata.FirstOrDefault(q => q.ofm_source_question_id == br.TrueSourcequestionIdentifier && q.surveyIsPublished == false && q.surveyStatecode == (int)ofm_survey_statecode.Active)?.ofm_questionid;
                var falseQuestionId = deserializedQuestiondata.FirstOrDefault(q => q.ofm_source_question_id == br.FalseSourcequestionIdentifier && q.surveyIsPublished == false && q.surveyStatecode == (int)ofm_survey_statecode.Active)?.ofm_questionid;
                var hasResponseQuestionId = deserializedQuestiondata.FirstOrDefault(q => q.ofm_source_question_id == br.childSourcequestionIdentifier && q.surveyIsPublished == false && q.surveyStatecode == (int)ofm_survey_statecode.Active)?.ofm_questionid;
                requestsQuestionBRCreation.Add(new CreateRequest($"{entitySetNameQuestionBR}",
      new JsonObject()
      {
         
                                            {ofm_question_business_rule.Fields.ofm_name, br.ofm_name },
                                            {ofm_question_business_rule.Fields.ofm_business_rule_type,(int)br.ofm_business_rule_type },
                                            {br._ofm_true_child_question_value != Guid.Empty ?ofm_question_business_rule.Fields.ofm_true_child_question + "@odata.bind":"_"+ofm_question_business_rule.Fields.ofm_true_child_question + "_value" ,br._ofm_true_child_question_value != Guid.Empty ? $"/{ofm_question.EntitySetName}({trueQuestionId})" : null},
                                            {br._ofm_false_child_question_value != Guid.Empty ?ofm_question_business_rule.Fields.ofm_false_child_question + "@odata.bind":"_"+ofm_question_business_rule.Fields.ofm_false_child_question + "_value" ,br._ofm_false_child_question_value != Guid.Empty ? $"/{ofm_question.EntitySetName}({falseQuestionId})" : null},
                                            {ofm_question_business_rule.Fields.ofm_parent_has_response, br.ofm_parent_has_response },
                                            {ofm_question_business_rule.Fields.ofm_condition, br.ofm_condition },
                                            {ofm_question_business_rule.Fields.ofm_section+"@odata.bind", $"/{ofm_section.EntitySetName}({_sectionId})" },
                                            {br.ofm_parent_has_response == true ?ofm_question_business_rule.Fields.ofm_child_question + "@odata.bind":"_"+ofm_question_business_rule.Fields.ofm_child_question + "_value", br.ofm_parent_has_response == true ? $"/{ofm_question.EntitySetName}({hasResponseQuestionId})": null},
                                            {ofm_question_business_rule.Fields.ofm_parentquestionid+"@odata.bind",$"/{ofm_question.EntitySetName}({parentQuestionId})" }


      }));


            });

            if (requestsQuestionBRCreation.Count > 0)
            {
                var createQuestionBRBatchResult = await d365WebApiService.SendBatchMessageAsync(appUserService.AZSystemAppUser, requestsQuestionBRCreation, null);

                if (createQuestionBRBatchResult.Errors.Any())
                {
                    var createQuestionBRError = ProcessResult.Failure(ProcessId, createQuestionBRBatchResult.Errors, createQuestionBRBatchResult.TotalProcessed, createQuestionBRBatchResult.TotalRecords);
                    _logger.LogError(CustomLogEvent.Process, "Failed to create Question Business rules with an error: {error}", JsonValue.Create(createQuestionBRError)!.ToString());
                    _logger.LogError(CustomLogEvent.Process, "Report Section is Created with {Report Section ID}", _sectionId);


                }
                requestsQuestionBRCreation.Clear();
            }


        }
        catch (ValidationException exp)
        {
            _logger.LogError(CustomLogEvent.Process, "Failed under catch block CreateBusinessRule", new[] { exp.Message, exp.StackTrace ?? string.Empty });
            ProcessResult.Failure(ProcessId, new[] { exp.Message, exp.StackTrace ?? string.Empty }, 0, 0);
            return await Task.FromResult(false);
        }
        return await Task.FromResult(true);

    }
   
    
}





