using System.Text.Json.Serialization;

namespace Balsm.Infrastructure.Configuration;

/// Which deployment this instance is, read from ASPNETCORE_ENVIRONMENT.
/// Independent of DeploymentMode, which says how it is hosted (Cloud /
/// Standalone): staging and production both run as Cloud.
///
/// Serialised lowercase on the wire, matching Sentry's environment names.
public enum DeploymentEnvironment
{
    [JsonStringEnumMemberName("production")]
    Production,

    [JsonStringEnumMemberName("staging")]
    Staging,

    /// Local laptops.
    [JsonStringEnumMemberName("development")]
    Development
}
