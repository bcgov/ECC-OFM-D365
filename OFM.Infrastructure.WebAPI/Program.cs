
using Hellang.Middleware.ProblemDetails;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OFM.Infrastructure.WebAPI.Caching;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.Batches;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Documents;
using OFM.Infrastructure.WebAPI.Services.Processes;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using OFM.Infrastructure.WebAPI.Services.Processes.Fundings;
using OFM.Infrastructure.WebAPI.Services.Processes.Payments;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfile;
using OFM.Infrastructure.WebAPI.Services.Processes.ProviderProfiles;
using OFM.Infrastructure.WebAPI.Services.Processes.Reporting;
using OFM.Infrastructure.WebAPI.Services.Processes.Requests;
using OFM.Infrastructure.WebAPI.Services.Processes.FundingReports;
using OFM.Infrastructure.WebAPI.Services.Processes.DataImports;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.AddServerHeader = false);
//builder.Logging.AddFilter(LogCategory.ProviderProfile, LogLevel.Debug);

var services = builder.Services;

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(config =>
{
    config.AddSwaggerApiKeySecurity(builder.Configuration);
    var xmlCommentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlCommentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlCommentsFile);
    config.IncludeXmlComments(xmlCommentsFullPath);
});
services.AddCustomProblemDetails();

services.AddDistributedMemoryCache();
services.TryAddSingleton(typeof(IDistributedCache<>), typeof(DistributedCache<>));
services.TryAddSingleton(TimeProvider.System);

services.AddScoped<ID365TokenService, D365TokenService>();
services.AddScoped<ID365AppUserService, D365AppUserService>();
services.AddScoped<ID365WebApiService, D365WebAPIService>();
services.AddScoped<ID365AuthenticationService, D365AuthServiceMSAL>();
services.AddScoped<ID365DataService, D365DataService>();

services.AddScoped<ID365DocumentProvider, DocumentProvider>();
services.AddScoped<ID365DocumentProvider, ApplicationDocumentProvider>();
services.AddScoped<ID365DocumentService, D365DocumentService>();

services.AddScoped<ID365ScheduledProcessService, ProcessService>();
services.AddScoped<ID365ProcessProvider, P100InactiveRequestProvider>();
services.AddScoped<ID365ProcessProvider, P200EmailReminderProvider>();
services.AddScoped<ID365ProcessProvider, P205SendNotificationProvider>();
services.AddScoped<ID365ProcessProvider, P210CreateFundingNotificationProvider>();
services.AddScoped<ID365ProcessProvider, P215SendSupplementaryRemindersProvider>();
services.AddScoped<ID365ProcessProvider, P220CreateSuppletaryRemindersProvider>();
services.AddScoped<ID365ProcessProvider, P225SendCertificateNotificationProvider>();
services.AddScoped<ID365ProcessProvider, P230SendApplicationNotificationProvider>();
services.AddScoped<ID365ProcessProvider, P235AllowanceApprovalDenialNotificationProvider>();
services.AddScoped<ID365ProcessProvider, P300BaseFundingProvider>();
services.AddScoped<ID365ProcessProvider, P305SupplementaryFundingProvider>();
services.AddScoped<ID365ProcessProvider, P310CalculateDefaultAllocationProvider>();
services.AddScoped<ID365ProcessProvider, P400VerifyGoodStandingProvider>();
services.AddScoped<ID365ProcessProvider, P405VerifyGoodStandingBatchProvider>();
services.AddScoped<ID365ProcessProvider, P500SendPaymentRequestProvider>();
services.AddScoped<ID365ProcessProvider, P505GeneratePaymentLinesProvider>();
services.AddScoped<ID365ProcessProvider, P510ReadPaymentResponseProvider>();
services.AddScoped<ID365ProcessProvider, P515GeneratePaymentLinesForIrregularExpense>();
services.AddScoped<ID365ProcessProvider, P600CloneFundingReportResponse>();
services.AddScoped<ID365ProcessProvider, P605CloseDuedReportsProvider>();
services.AddScoped<ID365ProcessProvider, P610CreateQuestionProvider>();
services.AddScoped<ID365ProcessProvider, P615CreateMonthlyReportProvider>();
services.AddScoped<ID365ProcessProvider, P700ProviderCertificateProvider>();

services.AddScoped<D365Email>();
services.AddScoped<ID365BackgroundProcessHandler, D365BackgroundProcessHandler>();

services.AddScoped<ID365BatchService, D365BatchService>();
services.AddScoped<ID365BatchProvider, BatchProvider>();
services.AddScoped<ID365BatchProvider, ContactEditProvider>();
services.AddScoped<ID365BatchProvider, ProviderReportResetProvider>();
services.AddScoped<IFundingRepository, FundingRepository>();
services.AddScoped<IEmailRepository, EmailRepository>();

services.AddD365HttpClient(builder.Configuration);
services.AddMvcCore().AddApiExplorer();
services.AddAuthentication();
services.AddHealthChecks();

//======== Configuration >>>
services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));
services.Configure<AuthenticationSettings>(builder.Configuration.GetSection(nameof(AuthenticationSettings)));
services.Configure<D365AuthSettings>(builder.Configuration.GetSection(nameof(D365AuthSettings)));
services.Configure<DocumentSettings>(builder.Configuration.GetSection(nameof(DocumentSettings)));
services.Configure<NotificationSettings>(builder.Configuration.GetSection(nameof(NotificationSettings)));
services.Configure<ProcessSettings>(builder.Configuration.GetSection(nameof(ProcessSettings)));
services.Configure<ExternalServices>(builder.Configuration.GetSection(nameof(ExternalServices)));
//======== <<<

// Wait 30 seconds for graceful shutdown.
builder.Host.ConfigureHostOptions(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseApiKey();
app.UseProblemDetails();

app.MapFallback(() => Results.Redirect("/swagger"));
app.UseHttpsRedirection();
app.UseAuthentication();

if (app.Configuration.GetValue<bool>("Features:Environment:Enable"))
    app.RegisterEnvironmentEndpoints();
if (app.Configuration.GetValue<bool>("Features:DocumentUpload:Enable"))
    app.RegisterDocumentsEndpoints();
if (app.Configuration.GetValue<bool>("Features:Batch:Enable"))
    app.RegisterBatchOperationsEndpoints();
if (app.Configuration.GetValue<bool>("Features:Search:Enable"))
    app.RegisterSearchesEndpoints();

app.RegisterBatchProcessesEndpoints();
app.RegisterProviderProfileEndpoints();
app.RegisterOperationsEndpoints();

#region Api Health

app.MapGet("/api/health", (ILogger<string> logger) =>
{
    //logger.LogInformation("Health checked on {currentTime}", DateTime.Now);

    return TypedResults.Ok("I am healthy!");

}).WithTags("Portal Environment").Produces(200).ProducesProblem(404).AllowAnonymous();

app.MapHealthChecks("/api/health");

#endregion

app.Run();