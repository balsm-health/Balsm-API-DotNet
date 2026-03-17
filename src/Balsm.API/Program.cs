using Balsm.Customer.Api;
using Balsm.Customer.Infrastructure;
using Balsm.Entity.Api;
using Balsm.Entity.Infrastructure;
using Balsm.Identity.Api;
using Balsm.Identity.Infrastructure;
using Balsm.Infrastructure;
using Balsm.Infrastructure.Middleware;
using Balsm.Inventory.Api;
using Balsm.Inventory.Infrastructure;
using Balsm.POS.Api;
using Balsm.POS.Infrastructure;
using Balsm.Prescription.Api;
using Balsm.Prescription.Infrastructure;
using Balsm.Supervisor;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Security;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Settings.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure service hosting for platform-native daemons
builder.Host.UseWindowsService();
builder.Host.UseSystemd();

// Determine deployment mode
var deploymentMode = builder.Configuration["DeploymentMode"] ?? "Standalone";
var isStandalone = deploymentMode.Equals("Standalone", StringComparison.OrdinalIgnoreCase);

// Bind to URL from configuration (supports Server:Urls in appsettings)
var serverUrls = builder.Configuration["Server:Urls"];

if (isStandalone)
{
    // Generate/load self-signed certificate for HTTPS admin panel
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var certLogger = loggerFactory.CreateLogger("Balsm.CertificateService");
    var cert = CertificateService.EnsureCertificate(certLogger);

    if (cert is not null)
    {
        var configPort = 5050;
        if (!string.IsNullOrEmpty(serverUrls) && Uri.TryCreate(
                serverUrls.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
        {
            configPort = uri.Port;
        }

        // Determine binding scope from Server:Mode config
        var serverMode = builder.Configuration["Server:Mode"];
        if (string.IsNullOrEmpty(serverMode))
        {
            serverMode = serverUrls?.Contains("0.0.0.0") == true ? "network" : "local";
        }

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // HTTP: local binds to localhost only, network/public binds to all interfaces
            if (serverMode is "network" or "public")
                kestrel.ListenAnyIP(configPort);
            else
                kestrel.ListenLocalhost(configPort);

            // HTTPS for admin panel (localhost only)
            kestrel.ListenLocalhost(configPort + 1, o => o.UseHttps(cert));
        });
    }
}
else if (!string.IsNullOrEmpty(serverUrls))
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
if (isStandalone)
{
    builder.Services.AddSupervisorModule(builder.Configuration);
}

// Add API services
var mvcBuilder = builder.Services.AddControllers()
    .AddApplicationPart(typeof(Balsm.Identity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Entity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Inventory.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.POS.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Customer.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Prescription.Api.ModuleRegistration).Assembly);

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
    app.UseMiddleware<AdminSetupRedirectMiddleware>();

    // Serve Vite build output — must be before UseRouting so that static files
    // take priority over the MapFallbackToFile SPA catch-all endpoint.
    var adminPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(adminPath),
        RequestPath = "/admin"
    });
}

// Explicit UseRouting after static files so fallback endpoints don't pre-match asset requests
app.UseRouting();

if (isStandalone)
{
    app.UseMiddleware<AdminAuthMiddleware>();
    app.UseMiddleware<FederationAuthMiddleware>();
}

app.UseAuthorization();
app.MapControllers();

if (isStandalone)
{
    // Serve admin panel at /admin (fallback to index.html for SPA routing)
    app.MapFallbackToFile("/admin/{**slug}", "admin/index.html");
    app.MapFallbackToFile("/admin", "admin/index.html");
}

app.Run();
