using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Balsm.API.OpenApi;

/// <summary>
/// Registers per-module OpenAPI documents plus an aggregate "all" document.
/// One document is emitted per call to <c>AddOpenApi(documentName, ...)</c>; the
/// build-time <c>Microsoft.Extensions.ApiDescription.Server</c> target writes each
/// to <c>docs/api/openapi/v1/{documentName}.json</c>.
/// </summary>
internal static class OpenApiServiceCollectionExtensions
{
    public static readonly IReadOnlyList<ModuleDocument> Documents =
    [
        new("identity",     "Balsm Identity API",     "Users, accounts, sessions, permissions, authentication."),
        new("entity",       "Balsm Entity API",       "Entities, branches, departments, rooms, beds."),
        new("inventory",    "Balsm Inventory API",    "Items, purchase orders, vendors, stock levels."),
        new("pos",          "Balsm POS API",          "Point-of-sale transactions and tills."),
        new("customer",     "Balsm Customer API",     "Customer profiles and contact details."),
        new("prescription", "Balsm Prescription API", "Prescriptions, medications, validity, drug interactions."),
        new("supervisor",   "Balsm Supervisor API",   "Standalone-mode admin panel: federation, certificates, self-update."),
    ];

    public static IServiceCollection AddBalsmOpenApi(this IServiceCollection services)
    {
        services.AddControllers(options => options.Conventions.Add(new ModuleGroupConvention()));

        foreach (var doc in Documents)
        {
            services.AddOpenApi(doc.Name, options =>
            {
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info = new OpenApiInfo
                    {
                        Title = doc.Title,
                        Version = "v1",
                        Description = doc.Description,
                        Contact = new OpenApiContact
                        {
                            Name = "Balsm Platform",
                            Url = new Uri("https://github.com/balsm-io/Balsm-API-DotNet"),
                        },
                    };
                    return Task.CompletedTask;
                });

                options.ShouldInclude = api =>
                    string.Equals(api.GroupName, doc.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        services.AddOpenApi("all", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Balsm API (aggregate)",
                    Version = "v1",
                    Description = "Aggregate of all Balsm modules — generated. Do not hand-edit.",
                };
                return Task.CompletedTask;
            });
            options.ShouldInclude = _ => true;
        });

        return services;
    }

    public static IEndpointRouteBuilder MapBalsmOpenApi(this IEndpointRouteBuilder endpoints)
    {
        // Single templated route resolves any registered document name (identity, entity, ..., all)
        // from the URL and dispatches to the matching OpenApi document service.
        endpoints.MapOpenApi("/openapi/v1/{documentName}.json")
            .WithName("OpenApi_Document");

        return endpoints;
    }

    public sealed record ModuleDocument(string Name, string Title, string Description);
}
