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
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

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

// Add API services
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Balsam.Identity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Entity.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Inventory.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.POS.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Customer.Api.ModuleRegistration).Assembly)
    .AddApplicationPart(typeof(Balsam.Prescription.Api.ModuleRegistration).Assembly);

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

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseAuthorization();
app.MapControllers();

app.Run();
