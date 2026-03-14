using Balsam.Customer.Api;
using Balsam.Customer.Infrastructure;
using Balsam.Entity.Api;
using Balsam.Entity.Infrastructure;
using Balsam.Identity.Api;
using Balsam.Identity.Infrastructure;
using Balsam.Infrastructure;
using Balsam.Infrastructure.Middleware;
using Balsam.Inventory.Api;
using Balsam.Inventory.Infrastructure;
using Balsam.POS.Api;
using Balsam.POS.Infrastructure;
using Balsam.Prescription.Api;
using Balsam.Prescription.Infrastructure;
using Balsam.Supervisor;
using Serilog;
using Serilog.Settings.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure service hosting for platform-native daemons
builder.Host.UseWindowsService();
builder.Host.UseSystemd();

// Bind to URL from configuration (supports Server:Urls in appsettings)
var serverUrls = builder.Configuration["Server:Urls"];
if (!string.IsNullOrEmpty(serverUrls))
{
    builder.WebHost.UseUrls(serverUrls);
}

// Configure Serilog (explicit assembly list for single-file publish compatibility)
var readerOptions = new ConfigurationReaderOptions(
    typeof(Serilog.ConsoleLoggerConfigurationExtensions).Assembly);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration, readerOptions));

// Add shared infrastructure
builder.Services.AddSharedInfrastructure(builder.Configuration);

// Register modules (Application + Api layer)
builder.Services.AddIdentityModule();
builder.Services.AddEntityModule();
builder.Services.AddInventoryModule();
builder.Services.AddPOSModule();
builder.Services.AddCustomerModule();
builder.Services.AddPrescriptionModule();

// Register module infrastructure (DbContexts, repositories)
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddEntityInfrastructure(builder.Configuration);
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddPOSInfrastructure(builder.Configuration);
builder.Services.AddCustomerInfrastructure(builder.Configuration);
builder.Services.AddPrescriptionInfrastructure(builder.Configuration);

// Register Supervisor module (admin panel, mDNS, self-update) for Standalone mode
var deploymentMode = builder.Configuration["DeploymentMode"] ?? "Standalone";
var isStandalone = deploymentMode.Equals("Standalone", StringComparison.OrdinalIgnoreCase);

if (isStandalone)
{
    builder.Services.AddSupervisorModule(builder.Configuration);
}

// Add API services
var mvcBuilder = builder.Services.AddControllers()
    .AddApplicationPart(typeof(Balsam.Identity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Entity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Inventory.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.POS.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Customer.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Prescription.Api.ModuleRegistration).Assembly);

if (isStandalone)
{
    mvcBuilder.AddApplicationPart(typeof(SupervisorRegistration).Assembly);
}

builder.Services.AddOpenApi();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!isStandalone)
{
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();

if (isStandalone)
{
    app.UseStaticFiles();
}

app.UseAuthorization();
app.MapControllers();

if (isStandalone)
{
    // Serve admin panel at /admin (fallback to index.html for SPA-like behavior)
    app.MapFallbackToFile("/admin/{**slug}", "admin/index.html");
}

app.Run();
