using Microsoft.Extensions.Hosting;

namespace Balsm.Infrastructure.Configuration;

public static class HostEnvironmentExtensions
{
    /// Any name other than Staging or Development — unset, misspelt, or a test
    /// host's "Testing" — reads as Production, the answer that fails closed:
    /// it is the one that never unlocks a staging-only bypass.
    public static DeploymentEnvironment GetDeploymentEnvironment(this IHostEnvironment environment) =>
        environment.IsStaging() ? DeploymentEnvironment.Staging
        : environment.IsDevelopment() ? DeploymentEnvironment.Development
        : DeploymentEnvironment.Production;

    /// Lowercase name for Sentry's environment and the `Environment` log
    /// property, so alerts filter on one spelling.
    public static string ToTelemetryName(this DeploymentEnvironment environment) =>
        environment.ToString().ToLowerInvariant();
}
