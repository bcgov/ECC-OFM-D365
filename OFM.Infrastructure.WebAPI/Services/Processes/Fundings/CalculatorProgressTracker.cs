using ECC.Core.DataContext;
using HandlebarsDotNet;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Messages;
using OFM.Infrastructure.WebAPI.Models.Fundings;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace OFM.Infrastructure.WebAPI.Services.Processes.Fundings;

public interface IProgressTracker
{
    Task SaveProgressAsync(Funding funding, IFundingResult fundingResult, List<NonHRStepAction>? nonHRMessages);
}

public enum TrackerCategory
{
    [Description("No Category")]
    None = 0,
    [Description("Calculator Category")]
    Calculator = 1,
    [Description("Funding Agreement Category")]
    FundingAgreement = 2,
    [Description("Application Category")]
    Application = 3,
    [Description("Integration Category")]
    Integration = 4,
    [Description("Calculator Error")]
    Error = 5,
    [Description("System Error")]
    SystemError = 6
};

public class CalculatorProgressTracker(ID365AppUserService appUserService, ID365WebApiService service, IFundingResult fundingResult, ILogger logger) : IProgressTracker
{
    private const int maxLength = 100000;
    private Guid? _regardingId;
    private Funding? _funding;
    private List<NonHRStepAction> _nonHRactions;
    private readonly IFundingResult _fundingResult = fundingResult;
    private FundingCalculator? _calculator;
    private readonly ILogger _logger = logger;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;
    private readonly List<dynamic> _rawFTEs;

    public string Title => $"Calculation Breakdowns at {TimeExtensions.GetCurrentPSTDateTime()} (PST)";
    public string Category => TrackerCategory.Calculator.ToString();
    public string TrackingDetails { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;

    public JsonObject TrackerMessage
    {
        get
        {
            string details = TrackingDetails;
            if (TrackingDetails.Length > maxLength)
                details = TrackingDetails[..(maxLength - 1)];

            return new JsonObject() {
                { "ofm_title", Title },
                { "ofm_tracking_details", details }, // Ensure the tracking details does not exceed the characters limit (4000).
                { "ofm_regardingid_ofm_funding@odata.bind", $"/ofm_fundings({_regardingId})"},
                { "ofm_category", Category }
            };
        }
    }
    public IEnumerable<JsonObject> TrackerMessages
    {
        get
        {
            return [TrackerMessage];
        }
    }

    private dynamic RawFTEsData
    {
        get
        {
            var actionsLog = _fundingResult.ActionsLog;
            //var first = actionsLog.FirstOrDefault();
            //var licenceDetails = first["LicenceDetails"];
            //var deserilized = JsonSerializer.Deserialize<List<LicenceDetail>>(licenceDetails.ToJsonString());

            var rawFTEs = actionsLog.GroupBy(act => act.LicenceType).Select(sdd => new
            {
                //sdd.First().Step,
                sdd.First().LicenceType,
                Spaces = sdd.Max(licenceDetail => licenceDetail.Spaces),
                RawITE = sdd.Max(licenceDetail => licenceDetail.RawITE),
                RawECE = sdd.Max(licenceDetail => licenceDetail.RawECE),
                RawECEA = sdd.Max(licenceDetail => licenceDetail.RawECEA),
                RawRA = sdd.Max(licenceDetail => licenceDetail.RawRA),
            });

            var value = new
            {
                header = new
                {
                    Total = "Total",
                    LicenceType = "Licence Category", // Values are Type A, B or C
                    Spaces = "Facility Spaces",
                    ITE = "RawITE",
                    ECE = "RawECE",
                    ECEA = "RawECEA",
                    RA = "RawRA",
                },
                steps = rawFTEs,
                footer = new
                {
                    Total = "Total",
                    Spaces = rawFTEs.Sum(ld => ld.Spaces),
                    ITE = rawFTEs.Sum(ld => ld.RawITE),
                    ECE = rawFTEs.Sum(ld => ld.RawECE),
                    ECEA = rawFTEs.Sum(ld => ld.RawECEA),
                    RA = rawFTEs.Sum(ld => ld.RawRA)
                }
            };

            return value;
        }
    }
    private dynamic NonHRRatesData
    {
        get
        {
            var facilityRate = _nonHRactions.First(act => act.Envelope == "Facility").Rate;
            var transposedRate = _nonHRactions.GroupBy(act => act.Step).Select(act => new
            {
                act.First().Step,
                act.First().MinSpaces,
                act.First().MaxSpaces,
                act.First().Ownership,
                Programming = act.First(stepAction => stepAction.Envelope == "Programming").Rate,
                Administrative = act.First(stepAction => stepAction.Envelope == "Administration").Rate,
                Operational = act.First(stepAction => stepAction.Envelope == "Operational").Rate,
                Facility = (act.Key == 1) ? facilityRate : 0m,
                Total = act.Sum(stepAction => stepAction.Rate)
            });

            return new
            {
                steps = transposedRate
            };
        }
    }
    private dynamic NonHRAmountsData
    {
        get
        {
            var facilityCost = _nonHRactions.First(c => c.Envelope == "Facility").Cost;
            var transposedCost = _nonHRactions.GroupBy(m => m.Step).Select(m => new
            {
                m.First().Step,
                m.First().AllocatedSpaces,
                Programming = m.First(c => c.Envelope == "Programming").Cost,
                Administrative = m.First(c => c.Envelope == "Administration").Cost,
                Operational = m.First(c => c.Envelope == "Operational").Cost,
                Facility = (m.Key == 1) ? facilityCost : 0m,
                Total = m.Sum(m => m.Cost)
            });

            var value = new
            {
                steps = transposedCost,
                footer = new
                {
                    step = "All",
                    spaces = transposedCost.Sum(m => m.AllocatedSpaces),
                    programming = transposedCost.Sum(m => m.Programming),
                    admin = transposedCost.Sum(m => m.Administrative),
                    operational = transposedCost.Sum(m => m.Operational),
                    facility = transposedCost.Sum(m => m.Facility),
                    totalCost = transposedCost.Sum(m => m.Total),
                }
            };

            return value;
        }
    }

    private string HRRawFTEsSource => $$$"""
                                        <div style="background-color:white;padding:20px; align:center">
                                        		<table border="1px solid black" cellspacing="0" cellpadding="3px">
                                        		<caption style="background-color:lightgrey; font-weight: bold;">Raw FTEs (Minimum Required Staffing)</caption>
                                        		<thead>
                                        			<tr style="text-align: center; vertical-align: middle;">
                                        				<th scope="col">Group Size</th>
                                        				<th scope="col">{{{RawFTEsData.header.Spaces}}}</th>
                                        				<th scope="col">{{{RawFTEsData.header.ITE}}}</th>
                                        				<th scope="col">{{{RawFTEsData.header.ECE}}}</th>
                                        				<th scope="col">{{{RawFTEsData.header.ECEA}}}</th>
                                        				<th scope="col">{{{RawFTEsData.header.RA}}}</th>
                                        			</tr>
                                        		</thead>
                                        		<tbody>                             
                                        			{{#each steps}}
                                        				{{>rawFTEStep rawFTEStep=.}}
                                        			{{/each}}                      
                                        		</tbody>
                                        		<tfoot>
                                        			<tr style="text-align: center; vertical-align: middle;">
                                        				<th scope="row">Total</th>
                                        	            <td>{{{RawFTEsData.footer.Spaces}}}</td>
                                        				<td>{{{RawFTEsData.footer.ITE}}}</td>
                                        				<td>{{{RawFTEsData.footer.ECE}}}</td>
                                        				<td>{{{RawFTEsData.footer.ECEA}}}</td>
                                        				<td>{{{RawFTEsData.footer.RA}}}</td>
                                        			</tr>
                                        		</tfoot>
                                        		</table>
                                        </div>
                                        """;
    private string NonHRRatesSource => $$$"""
                                            <div style="background-color:white;padding:20px;align:center">
                                                <table border="1px solid black" cellspacing="0" cellpadding="3px">
                                                <caption style="background-color:lightgrey;font-weight:bold;">Non-HR Funding Rates</caption>
                                                <thead>
                                                    <tr style="text-align:center;vertical-align:middle;">
                                                        <th scope="col">Steps</th>
                                                        <th scope="col">Min Spaces</th>
                                                        <th scope="col">Max Spaces</th>
                                                        <th scope="col">Ownership</th>
                                                        <th scope="col">Programming<br/>($/space)</th>
                                                        <th scope="col">Administrative<br/>($/space)</th>
                                                        <th scope="col">Operational<br/>($/space)</th>
                                                        <th scope="col">Facility<br/>($/space)</th>
                                                        <th scope="col">Total Cost<br/>($/space)</th>
                                                    </tr>
                                                </thead>
                                                <tbody>                             
                                                    {{#each steps}}
                                                        {{>nonHRRateStep nonHRRateStep=.}}
                                                    {{/each}}                      
                                                </tbody>
                                                </table>
                                            </div>
                                            """;
    private string NonHRAmountsSource => $$$"""
                                        <div style="background-color:white;padding:20px; align:center">
                                            <table border="1px solid black" cellspacing="0" cellpadding="3px">
                                            <caption style="background-color:lightgrey;font-weight: bold;">{{{_funding?.ofm_facility?.name}}}</caption>
                                            <thead>
                                                <tr style="text-align:center;vertical-align:middle;">
                                                    <th scope="col">Steps</th>
                                                    <th scope="col">Adjusted Spaces</th>
                                                    <th scope="col">Programming</th>
                                                    <th scope="col">Administrative</th>
                                                    <th scope="col">Operational</th>
                                                    <th scope="col">Facility</th>
                                                    <th scope="col">Total Cost</th>
                                                </tr>
                                            </thead>
                                            <tbody>                             
                                                {{#each steps}}
                                                    {{>nonHRStep nonHRStep=.}}
                                                {{/each}}                      
                                            </tbody>
                                            <tfoot>
                                                <tr style="text-align:center;vertical-align:middle;">
                                                    <th scope="row">All</th>
                                                    <td>{{{NonHRAmountsData.footer.spaces}}}</td>
                                                    <td>{{{NonHRAmountsData.footer.programming}}}</td>
                                                    <td>{{{NonHRAmountsData.footer.admin}}}</td>
                                                    <td>{{{NonHRAmountsData.footer.operational}}}</td>
                                                    <td>{{{NonHRAmountsData.footer.facility}}}</td>
                                                    <td>{{{NonHRAmountsData.footer.totalCost}}}</td>
                                                </tr>
                                            </tfoot>
                                            </table>
                                        </div>
                                        """;

    private string RawFTEsPartialSource => """
                                    <tr style="text-align:center;vertical-align:middle;"><td>{{LicenceType}}</td><td>{{Spaces}}</td><td>{{RawITE}}</td><td>{{RawECE}}</td><td>{{RawECEA}}</td><td>{{RawRA}}</td></tr>
                                    """;
    private string NonHRRatesPartialSource => """
                                    <tr style="text-align:center;vertical-align:middle;"><td>{{Step}}</td><td>{{MinSpaces}}</td><td>{{MaxSpaces}}</td><td>{{Ownership}}</td><td>{{Programming}}</td><td>{{Administrative}}</td><td>{{Operational}}</td><td>{{Facility}}</td><td>{{Total}}</td></tr>
                                    """;
    private string NonHRAmountsPartialSource => """
                                    <tr style="text-align:center;vertical-align:middle;"><td>{{Step}}</td><td>@{{AllocatedSpaces}}</td><td>{{Programming}}</td><td>{{Administrative}}</td><td>{{Operational}}</td><td>{{Facility}}</td><td>{{Total}}</td></tr>
                                    """;

    public async Task SaveProgressAsync(Funding funding, IFundingResult fundingResult, List<NonHRStepAction>? nonHRMessages)
    {
        ArgumentNullException.ThrowIfNull(funding);
        ArgumentNullException.ThrowIfNull(fundingResult);
        ArgumentNullException.ThrowIfNull(nonHRMessages);

        _regardingId = funding.ofm_fundingid;
        _funding = funding;
        _nonHRactions = [.. nonHRMessages.OrderBy(m => m.Step)];

        Handlebars.RegisterTemplate("rawFTEStep", RawFTEsPartialSource);
        Handlebars.RegisterTemplate("nonHRRateStep", NonHRRatesPartialSource);
        Handlebars.RegisterTemplate("nonHRStep", NonHRAmountsPartialSource);

        var rawFTEStepsTemplate = Handlebars.Compile(HRRawFTEsSource);
        var nonHRRatesTemplate = Handlebars.Compile(NonHRRatesSource);
        var nonHRAmountsTemplate = Handlebars.Compile(NonHRAmountsSource);

        var rawFTEsResult = rawFTEStepsTemplate(RawFTEsData);
        var nonHRRatesResult = nonHRRatesTemplate(NonHRRatesData);
        var nonHRAmountsResult = nonHRAmountsTemplate(NonHRAmountsData);

        TrackingDetails = string.Concat(rawFTEsResult, nonHRRatesResult, nonHRAmountsResult);

        List<HttpRequestMessage> createRequests = [];

        foreach (var message in TrackerMessages)
        {
            createRequests.Add(new CreateRequest(ofm_progress_tracker.EntitySetName, message));
        }

        var batchResult = await _d365webapiservice.SendBatchMessageAsync(_appUserService.AZSystemAppUser, createRequests, null);

        if (batchResult.Errors.Any())
        {
            _logger.LogError("Failed to create the progress tracker record for nonHR action messages with the server error: {errors}", batchResult.Errors);
        }
    }
}

public class CalculatorErrorTracker(ID365AppUserService appUserService, ID365WebApiService service, ILogger logger) : IProgressTracker
{
    private readonly ILogger _logger = logger;
    private readonly ID365AppUserService _appUserService = appUserService;
    private readonly ID365WebApiService _d365webapiservice = service;

    public string Title => $"*** ERROR *** - Calculator error at {TimeExtensions.GetCurrentPSTDateTime()} (PST)";
    public string Category => TrackerCategory.Error.ToString();
    public string TrackingDetails { get; set; } = string.Empty;
    public int Status { get; set; } = (int)ofm_progress_tracker_StatusCode.Active;
    public string StatusMessage { get; set; } = string.Empty;

    public JsonObject TrackerMessage
    {
        get
        {
            return [];
        }
    }
    public IEnumerable<JsonObject> TrackerMessages
    {
        get
        {
            return [TrackerMessage];
        }
    }

    public async Task SaveProgressAsync(Funding funding, IFundingResult _fundingResult, List<NonHRStepAction>? nonHRMessages)
    {
        List<HttpRequestMessage> errorMessageRequests = [];

        foreach (var errorMessage in _fundingResult.Errors)
        {
            errorMessageRequests.Add(new CreateRequest(ofm_progress_tracker.EntitySetName,
                    new JsonObject(){
                            { "ofm_title",Title },
                            { "ofm_category",Category },
                            { "ofm_tracking_details",errorMessage },
                            { "ofm_regardingid_ofm_funding@odata.bind", $"/ofm_fundings({funding.Id})"}
            }));
        }

        var d365Result = await _d365webapiservice.SendBatchMessageAsync(appUserService.AZSystemAppUser, errorMessageRequests, Guid.Empty);

        if (d365Result.Errors.Any())
        {
            _logger.LogError(CustomLogEvent.Process, "Failed to create the progress tracker record for the calculator errors with a server error: {error}", JsonValue.Create(d365Result.Errors)!.ToString());
        }
    }
}