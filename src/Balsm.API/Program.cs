using Balsm.API.OpenApi;
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
using MigrationGateMiddleware = Balsm.Infrastructure.Middleware.MigrationGateMiddleware;
using AuditEnricherMiddleware = Balsm.Infrastructure.Middleware.AuditEnricherMiddleware;

var builder = WebApplication.CreateBuilder(args);

// Configure service hosting for platform-native daemons
builder.Host.UseWindowsService();
builder.Host.UseSystemd();

// Determine deployment mode
var deploymentMode = builder.Configuration["DeploymentMode"] ?? "Standalone";
var isStandalone = deploymentMode.Equals("Standalone", StringComparison.OrdinalIgnoreCase);

// OpenAPI spec generation mode: serve /openapi/* without running migrations,
// backups, audit retention, or other DB-dependent hosted services. Triggered by
// the BALSM_GENERATE_OPENAPI env var so scripts/generate-openapi.sh can boot a
// stripped host against a throwaway DB.
var isOpenApiGenerationMode = builder.Configuration.GetValue<bool>("BALSM_GENERATE_OPENAPI");

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
        var httpPort = 5050;
        if (!string.IsNullOrEmpty(serverUrls) && Uri.TryCreate(
                serverUrls.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
        {
            httpPort = uri.Port;
        }

        // HTTPS port is configurable (Server:HttpsPort). Default keeps the dev
        // convention (HTTP+1, e.g. 5050→5051); set 443 for a port-less URL when
        // the service runs privileged. 80→443 is special-cased so a packaged
        // service only needs Server:Urls=http://0.0.0.0:80.
        var httpsPort = builder.Configuration.GetValue<int?>("Server:HttpsPort")
            ?? (httpPort == 80 ? 443 : httpPort + 1);

        // Determine binding scope from Server:Mode config
        var serverMode = builder.Configuration["Server:Mode"];
        if (string.IsNullOrEmpty(serverMode))
        {
            serverMode = serverUrls?.Contains("0.0.0.0") == true ? "network" : "local";
        }

        var bindAll = serverMode is "network" or "public";

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // HTTP + HTTPS: localhost only in local mode; all interfaces in
            // network/public so LAN devices can reach the admin panel by name.
            if (bindAll)
            {
                kestrel.ListenAnyIP(httpPort);
                kestrel.ListenAnyIP(httpsPort, o => o.UseHttps(cert));
            }
            else
            {
                kestrel.ListenLocalhost(httpPort);
                kestrel.ListenLocalhost(httpsPort, o => o.UseHttps(cert));
            }
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

// Required by Supervisor's RateLimitMiddleware and any cache-dependent module code.
builder.Services.AddMemoryCache();

// CORS: the admin panel is served from balsm.local / balsm-<slug>.local while the
// API is reachable at api.<host>. Those are cross-origin, so allow the local
// admin origins with credentials (the session cookie rides along).
const string AdminCorsPolicy = "admin-local";
builder.Services.AddCors(options => options.AddPolicy(AdminCorsPolicy, policy => policy
    .SetIsOriginAllowed(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var u)
        && (u.Host is "localhost" or "127.0.0.1" || u.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

if (isOpenApiGenerationMode)
{
    // Strip hosted services that touch the database — spec generation only needs DI graph + endpoints.
    var hostedServiceDescriptors = builder.Services
        .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    && d.ImplementationType?.Namespace?.StartsWith("Balsm.", StringComparison.Ordinal) == true)
        .ToList();
    foreach (var descriptor in hostedServiceDescriptors)
    {
        builder.Services.Remove(descriptor);
    }
}

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

// Register composition-root services
builder.Services.AddScoped<Balsm.SharedKernel.Contracts.IFirstRunOrchestrator, Balsm.API.Services.FirstRunOrchestrator>();

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

builder.Services.AddBalsmOpenApi();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<MigrationGateMiddleware>();

// Always expose per-module + aggregate OpenAPI documents.
// Spec is generated from controller attributes — safe to publish in non-Dev too.
app.MapBalsmOpenApi();

if (!isStandalone)
{
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();

if (isStandalone)
{
    app.UseMiddleware<AdminSetupRedirectMiddleware>();

    // Cache policy for the SPA (served at the host root). index.html must never be
    // cached so a new build's hashed asset URLs are always picked up; the hashed
    // assets are immutable forever. Reserved API paths are left untouched.
    app.Use(async (ctx, next) =>
    {
        var p = ctx.Request.Path;
        var reserved = p.StartsWithSegments("/api")
            || p.StartsWithSegments("/openapi")
            || p.StartsWithSegments("/connect");
        if (!reserved)
        {
            ctx.Response.Headers.CacheControl = p.StartsWithSegments("/assets")
                ? "public, max-age=31536000, immutable"
                : "no-cache, no-store, must-revalidate";
        }
        await next();
    });

    // Serve the Vite build output at the host root (panel = balsm.local/, no
    // /admin prefix). Must be before UseRouting so static files win over the
    // SPA fallback.
    var adminPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin");
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(adminPath),
        RequestPath = ""
    });
}

// Explicit UseRouting after static files so fallback endpoints don't pre-match asset requests
app.UseRouting();

// CORS before auth/audit so cross-origin preflight (OPTIONS) is answered and not
// rejected by the admin-auth middleware.
app.UseCors(AdminCorsPolicy);

app.UseMiddleware<AuditEnricherMiddleware>();

if (isStandalone)
{
    app.UseMiddleware<AdminAuthMiddleware>();
    app.UseMiddleware<FederationAuthMiddleware>();
    app.UseWhen(
        ctx => ctx.Request.Path.StartsWithSegments("/api/v1/admin/auth/login"),
        branch => branch.UseMiddleware<Balsm.Supervisor.Middleware.RateLimitMiddleware>());
}

app.UseAuthorization();
app.MapControllers();

if (isStandalone)
{
    // SPA fallback: any unmatched non-API path serves index.html (client-side
    // routing). Reserved API/doc paths fall through to a real 404 instead.
    var indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin", "index.html");
    app.MapFallback(async ctx =>
    {
        var p = ctx.Request.Path;
        if (p.StartsWithSegments("/api")
            || p.StartsWithSegments("/openapi")
            || p.StartsWithSegments("/connect"))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(indexPath);
    });
}

app.Run();
