using Balsm.Account.Api;
using Balsm.Account.Infrastructure;
using Balsm.API.OpenApi;
using Microsoft.AspNetCore.HttpOverrides;
using Balsm.Auth.Api;
using Balsm.Auth.Infrastructure;
using Balsm.Customer.Api;
using Balsm.Customer.Infrastructure;
using Balsm.Deletion.Api;
using Balsm.Deletion.Infrastructure;
using Balsm.Disclosure.Api;
using Balsm.Disclosure.Infrastructure;
using Balsm.EmergencyQr.Api;
using Balsm.EmergencyQr.Infrastructure;
using Balsm.Entity.Api;
using Balsm.Entity.Infrastructure;
using Balsm.Geofence.Infrastructure;
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
using Balsm.Sessions.Api;
using Balsm.Sessions.Infrastructure;
using Balsm.Supervisor;
using Balsm.Supervisor.Cli;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Security;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.FileProviders;
using Sentry.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Settings.Configuration;
using MigrationGateMiddleware = Balsm.Infrastructure.Middleware.MigrationGateMiddleware;
using AuditEnricherMiddleware = Balsm.Infrastructure.Middleware.AuditEnricherMiddleware;

// ── CLI dispatch ─────────────────────────────────────────────────────────────
// If the first argument matches a known CLI command, run it and exit immediately
// without starting the ASP.NET host.
if (CliRouter.IsCliInvocation(args))
    return await CliRouter.RunAsync(args);

var builder = WebApplication.CreateBuilder(args);

// Config layering (later wins):
//   appsettings.json            committed non-secret defaults
//   appsettings.Build.json      baked from the repo-root .env at build (gitignored)
//   appsettings.Local.json      optional per-box overrides (gitignored)
//   environment variables       runtime overrides, e.g. Sentry__DSN  (always last)
builder.Configuration
    .AddJsonFile("appsettings.Build.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Configure service hosting for platform-native daemons
builder.Host.UseWindowsService();
builder.Host.UseSystemd();

// Determine deployment mode
var deploymentMode = builder.Configuration["DeploymentMode"] ?? "Standalone";
var isStandalone = deploymentMode.Equals("Standalone", StringComparison.OrdinalIgnoreCase);

// Admin portal kill switch. The admin portal — the Supervisor module, the
// self-signed HTTPS listener on :5051, the admin SPA, and the admin-auth
// middleware — is a self-hosted-only surface that must NEVER be exposed on a
// cloud run. It defaults to on in Standalone and off otherwise, but
// AdminPortal:Enabled is an explicit override that disables the entire surface
// regardless of DeploymentMode — so a misconfigured mode string can never leak
// the admin portal in the cloud.
var adminPortalEnabled =
    builder.Configuration.GetValue<bool?>("AdminPortal:Enabled") ?? isStandalone;

// OpenAPI spec generation mode: serve /openapi/* without running migrations,
// backups, audit retention, or other DB-dependent hosted services. Triggered by
// the BALSM_GENERATE_OPENAPI env var so scripts/generate-openapi.sh can boot a
// stripped host against a throwaway DB.
var isOpenApiGenerationMode = builder.Configuration.GetValue<bool>("BALSM_GENERATE_OPENAPI");

// Bind to URL from configuration (supports Server:Urls in appsettings)
var serverUrls = builder.Configuration["Server:Urls"];

if (adminPortalEnabled)
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

// Release identifier shared by Sentry release-health and event tagging.
var assemblyVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
var sentryRelease = $"balsm-api@{assemblyVersion}";

// Resolve the on-disk log directory once (relative to the binary, matching the
// var/ convention used by the CLI token + connection-info files). The File sink
// below and LogsController both read from this absolute path.
var logDirectory = Path.GetFullPath(
    builder.Configuration["Logs:Directory"] ?? "var/logs",
    AppContext.BaseDirectory);
Directory.CreateDirectory(logDirectory);
var retainedLogFiles = builder.Configuration.GetValue<int?>("Logs:RetainedFileCountLimit") ?? 14;

// Sentry: full ASP.NET Core integration (automatic request data, performance
// transactions, EF Core query spans via Sentry.DiagnosticSource, release health
// sessions, and CPU profiling). Initialized here so it owns the SDK hub; the
// Serilog sink below reuses this hub instead of re-initializing.
//
// HEALTHCARE/PHI SAFETY: SendDefaultPii stays false and request bodies are never
// captured (MaxRequestBodySize = None). Do not enable either — request payloads
// and headers may carry patient data.
var sentryDsn = builder.Configuration["Sentry:DSN"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(options =>
    {
        options.Dsn = sentryDsn;
        options.Environment = deploymentMode;
        options.Release = sentryRelease;
        options.AttachStacktrace = true;
        options.MaxBreadcrumbs = 200;
        options.AutoSessionTracking = true;             // release health
        options.SendDefaultPii = false;                 // never send PHI/PII
        options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        options.TracesSampleRate = builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0.1;
        // CPU profiling rides on top of sampled transactions. Default 0 (off) so
        // a self-hosted box pays no overhead unless an operator opts in.
        options.ProfilesSampleRate = builder.Configuration.GetValue<double?>("Sentry:ProfilesSampleRate") ?? 0.0;
        // Profiler startup timeout (TimeSpan ctor is the 4.3.0 API; the
        // AddProfilingIntegration extension landed in a later SDK release).
        options.AddIntegration(new Sentry.Profiling.ProfilingIntegration(TimeSpan.FromMilliseconds(500)));
    });
}

// Configure Serilog (explicit assembly list for single-file publish compatibility)
var readerOptions = new ConfigurationReaderOptions(
    typeof(Serilog.ConsoleLoggerConfigurationExtensions).Assembly);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration, readerOptions)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("DeploymentMode", deploymentMode)
        // Rolling daily file under var/logs — surfaced by the admin portal Logs
        // page and the `balsm logs` CLI command.
        .WriteTo.File(
            Path.Combine(logDirectory, "balsm-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retainedLogFiles,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
        // Reuse the hub initialized by UseSentry above (no DSN here). Errors+ are
        // sent as events; lower levels become breadcrumbs for context.
        .WriteTo.Sentry(options =>
        {
            // Reuse the hub from UseSentry. Without this the sink calls
            // SentrySdk.Init itself and throws on the missing DSN.
            options.InitializeSdk = false;
            options.MinimumBreadcrumbLevel = LogEventLevel.Debug;
            options.MinimumEventLevel = LogEventLevel.Error;
        }));

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

// JWT Bearer authentication + patient-app authorization policies
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "balsm",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "balsm-app",
            ValidateLifetime = true,
            ClockSkew = System.TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy(Balsm.API.Authorization.PolicyNames.SelfOnly,
        p => p.AddRequirements(new Balsm.API.Authorization.SelfOnlyRequirement()));
    opts.AddPolicy(Balsm.API.Authorization.PolicyNames.ActiveAccount,
        p => p.AddRequirements(new Balsm.API.Authorization.ActiveAccountRequirement()));
    opts.AddPolicy(Balsm.API.Authorization.PolicyNames.NotLockedOut,
        p => p.AddRequirements(new Balsm.API.Authorization.NotLockedOutRequirement()));
    opts.AddPolicy(Balsm.API.Authorization.PolicyNames.AgeGate,
        p => p.AddRequirements(new Balsm.API.Authorization.AgeGateRequirement()));
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Balsm.API.Authorization.SelfOnlyHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Balsm.API.Authorization.ActiveAccountHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Balsm.API.Authorization.NotLockedOutHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Balsm.API.Authorization.AgeGateHandler>();

// Register modules (Application + Api layer)
builder.Services.AddAuthModule();
builder.Services.AddAccountModule();
builder.Services.AddEmergencyQrModule();
builder.Services.AddSessionsModule();
builder.Services.AddDeletionModule();
builder.Services.AddDisclosureModule();
builder.Services.AddIdentityModule();
builder.Services.AddEntityModule();
builder.Services.AddInventoryModule();
builder.Services.AddPOSModule();
builder.Services.AddCustomerModule();
builder.Services.AddPrescriptionModule();

// Register module infrastructure (DbContexts, repositories)
builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddAccountInfrastructure(builder.Configuration);
builder.Services.AddEmergencyQrInfrastructure(builder.Configuration);
builder.Services.AddSessionsInfrastructure(builder.Configuration);
builder.Services.AddDeletionInfrastructure(builder.Configuration);
builder.Services.AddDisclosureInfrastructure(builder.Configuration);
builder.Services.AddGeofenceInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddEntityInfrastructure(builder.Configuration);
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddPOSInfrastructure(builder.Configuration);
builder.Services.AddCustomerInfrastructure(builder.Configuration);
builder.Services.AddPrescriptionInfrastructure(builder.Configuration);

// Register Supervisor module (admin panel, mDNS, self-update) — admin portal only.
if (adminPortalEnabled)
{
    builder.Services.AddSupervisorModule(builder.Configuration);
}

// Register composition-root services
builder.Services.AddScoped<Balsm.SharedKernel.Contracts.IFirstRunOrchestrator, Balsm.API.Services.FirstRunOrchestrator>();

// Add API services
var mvcBuilder = builder.Services.AddControllers()
    .AddApplicationPart(typeof(Balsm.Auth.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Account.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.EmergencyQr.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Sessions.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Deletion.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Disclosure.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Identity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Entity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Inventory.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.POS.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Customer.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsm.Prescription.Api.ModuleRegistration).Assembly);

// Balsm.API references Balsm.Supervisor, so MVC auto-discovers its controllers
// (admin-auth, admin-status, connect, federation, …) even without an explicit
// AddApplicationPart. When the admin portal is disabled we physically REMOVE
// that application part, so those endpoints do not exist at all — otherwise
// they would still route to controllers whose services were never registered
// (AddSupervisorModule is skipped) and fail at activation.
if (!adminPortalEnabled)
{
    var supervisorAssembly = typeof(SupervisorRegistration).Assembly;
    mvcBuilder.ConfigureApplicationPartManager(apm =>
    {
        var part = apm.ApplicationParts
            .FirstOrDefault(p => p is AssemblyPart ap && ap.Assembly == supervisorAssembly);
        if (part is not null) apm.ApplicationParts.Remove(part);
    });
}

// snake_case on the wire (request binding + response serialization) so the
// Flutter client's snake_case DTOs (country_code, device_id, id_token, …) bind
// correctly. STJ applies the naming policy to BOTH read and write, so requests
// deserialize and responses serialize as snake_case.
// Enums serialize as their string names (e.g. "active") rather than integer
// ordinals so string-typed DTOs match the wire format.
mvcBuilder.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddBalsmOpenApi();

// Configure JSON serialization (minimal-API endpoints)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();

// Debug-mode request/response tracing. Gated on Debug:LogRequests AND a
// non-Production environment: request/response bodies can carry PHI, so this
// tracer is hard-blocked in Production even if the flag is mistakenly set. Runs
// right after CorrelationId so every trace line carries the correlation id, and
// before ExceptionHandling so it observes the final (formatted) response.
if (builder.Configuration.GetValue<bool>("Debug:LogRequests") && !app.Environment.IsProduction())
{
    app.UseMiddleware<Balsm.Infrastructure.Middleware.DebugRequestLoggingMiddleware>();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<MigrationGateMiddleware>();

// Always expose per-module + aggregate OpenAPI documents.
// Spec is generated from controller attributes — safe to publish in non-Dev too.
app.MapBalsmOpenApi();

if (!isStandalone)
{
    // Cloud/hosted runs behind a TLS-terminating reverse proxy (Railway, etc.)
    // that forwards plain HTTP with X-Forwarded-Proto: https. Honor it so the
    // app sees the real scheme; otherwise UseHttpsRedirection 307s every request
    // into a redirect loop. KnownNetworks/KnownProxies are cleared because the
    // proxy IP is dynamic and not knowable at deploy time.
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
    };
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedOptions);

    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();

if (adminPortalEnabled)
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

// Sentry performance: open a transaction per request (no-op unless a DSN is set
// and TracesSampleRate > 0). After UseRouting so the route template names the
// transaction; static-asset requests already short-circuited above.
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    app.UseSentryTracing();
}

// CORS before auth/audit so cross-origin preflight (OPTIONS) is answered and not
// rejected by the admin-auth middleware.
app.UseCors(AdminCorsPolicy);

app.UseMiddleware<AuditEnricherMiddleware>();

if (adminPortalEnabled)
{
    app.UseMiddleware<LocalOsTrustMiddleware>();
    app.UseMiddleware<AdminAuthMiddleware>();
    app.UseMiddleware<FederationAuthMiddleware>();
    app.UseWhen(
        ctx => ctx.Request.Path.StartsWithSegments("/api/v1/admin/auth/login"),
        branch => branch.UseMiddleware<Balsm.Supervisor.Middleware.RateLimitMiddleware>());
}

app.UseAuthorization();
app.MapControllers();

if (adminPortalEnabled)
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

await app.RunAsync();
return 0;
