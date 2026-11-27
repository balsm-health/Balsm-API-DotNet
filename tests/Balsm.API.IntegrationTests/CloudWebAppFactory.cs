using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The container-free host configured as a cloud run: not Standalone, so the
/// admin portal is off, forwarded headers are honoured and HTTPS redirection
/// is on. Debug request tracing is switched on too, as an operator would to
/// diagnose a non-production deployment.
/// </summary>
public sealed class CloudWebAppFactory : LiteWebAppFactory
{
    protected override IReadOnlyDictionary<string, string> AdditionalSettings { get; } =
        new Dictionary<string, string>
        {
            ["DeploymentMode"] = "Cloud",
            ["Server:Urls"] = "http://localhost:5099",
            ["Debug:LogRequests"] = "true",
        };
}

/// <summary>Shares one cloud-mode host across the tests that need it.</summary>
[CollectionDefinition(nameof(CloudWebAppCollection))]
public sealed class CloudWebAppCollection : ICollectionFixture<CloudWebAppFactory>;

/// <summary>
/// The stripped host scripts/generate-openapi.sh boots (BALSM_GENERATE_OPENAPI):
/// no Balsm hosted services, so no migrations, backups or retention jobs.
/// </summary>
public sealed class OpenApiGenerationWebAppFactory : LiteWebAppFactory
{
    protected override IReadOnlyDictionary<string, string> AdditionalSettings { get; } =
        new Dictionary<string, string> { ["BALSM_GENERATE_OPENAPI"] = "true" };
}

/// <summary>Shares one spec-generation host across the tests that need it.</summary>
[CollectionDefinition(nameof(OpenApiGenerationWebAppCollection))]
public sealed class OpenApiGenerationWebAppCollection : ICollectionFixture<OpenApiGenerationWebAppFactory>;
