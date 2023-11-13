
using Hellang.Middleware.ProblemDetails;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OFM.Infrastructure.WebAPI.Caching;
using OFM.Infrastructure.WebAPI.Extensions;
using OFM.Infrastructure.WebAPI.Models;
using OFM.Infrastructure.WebAPI.Services.AppUsers;
using OFM.Infrastructure.WebAPI.Services.D365WebApi;
using OFM.Infrastructure.WebAPI.Services.Documents;
using OFM.Infrastructure.WebAPI.Services.Processes;
using OFM.Infrastructure.WebAPI.Services.Processes.Emails;
using OFM.Infrastructure.WebAPI.Services.Processes.Requests;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(LogCategory.ProviderProfile, LogLevel.Debug);

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

services.TryAddSingleton<ID365TokenService, D365TokenService>();
services.TryAddSingleton<ID365AppUserService, D365AppUserService>();
services.TryAddSingleton<ID365WebApiService, D365WebAPIService>();
services.TryAddSingleton<ID365AuthenticationService, D365AuthServiceMSAL>();
services.TryAddSingleton<ID365DocumentProvider, DocumentProvider>();
services.TryAddSingleton<ID365DocumentProvider, ApplicationDocumentProvider>();
services.TryAddSingleton<ID365DocumentService, D365DocumentService>();

services.AddScoped<ID365ProcessService, ProcessService>();
services.AddScoped<ID365ProcessProvider, P100InactiveRequestProvider>();
services.AddScoped<ID365ProcessProvider, P200EmailReminderProvider>();
services.AddScoped<D365Email>();

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
    logger.LogInformation("Health checked on {currentTime}(PST)", DateTime.Now);

    return TypedResults.Ok("I am healthy!");

}).WithTags("Environment").Produces(200).ProducesProblem(404).AllowAnonymous();

app.MapHealthChecks("/api/health");

#endregion

app.Run();
