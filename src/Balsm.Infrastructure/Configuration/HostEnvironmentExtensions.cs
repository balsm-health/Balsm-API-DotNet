using Microsoft.Extensions.Hosting;

namespace Balsm.Infrastructure.Configuration;

public static class HostEnvironmentExtensions
{
    /// The names a host may run under: the three deployments, plus the
    /// integration-test hosts' "Testing" (which reads as Production).
    private static readonly string[] KnownNames =
        [.. Enum.GetNames<DeploymentEnvironment>(), "Testing"];

    /// Stops the boot on an ASPNETCORE_ENVIRONMENT that names no deployment.
    /// A typo would otherwise run as Production with no appsettings layer of
    /// its own, and so would "production": Linux matches
    /// appsettings.{Name}.json case-sensitively, hence the exact comparison.
    public static void EnsureKnownName(this IHostEnvironment environment)
    {
        if (KnownNames.Contains(environment.EnvironmentName, StringComparer.Ordinal)) return;
        throw new InvalidOperationException(
            $"ASPNETCORE_ENVIRONMENT='{environment.EnvironmentName}' is not a known environment. " +
            "Use Production, Staging or Development, capitalised as written.");
    }

    /// Any name other than Staging or Development — unset, or a test host's
    /// "Testing" — reads as Production, the answer that fails closed: it is
    /// the one that never unlocks a staging-only bypass.
    public static DeploymentEnvironment GetDeploymentEnvironment(this IHostEnvironment environment) =>
        environment.IsStaging() ? DeploymentEnvironment.Staging
        : environment.IsDevelopment() ? DeploymentEnvironment.Development
        : DeploymentEnvironment.Production;

    /// Lowercase name for Sentry's environment and the `Environment` log
    /// property, so alerts filter on one spelling.
    public static string ToTelemetryName(this DeploymentEnvironment environment) =>
        environment.ToString().ToLowerInvariant();
}
