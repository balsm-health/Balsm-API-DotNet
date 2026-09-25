using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

/// Decides whether the fixed `Otp:DevCode` may stand in for a real one-time
/// code, so staging can be tested end to end without a mailbox.
///
/// This is an authentication bypass — with it, an email address is the only
/// thing standing between a caller and an account — so it is fenced on three
/// sides and every one of them must hold:
///
/// 1. Only where `Deployment:Environment` says "staging", or on a Development
///    host. Not the ASPNETCORE_ENVIRONMENT name: docker-compose sets that to
///    Production for every deployment, staging included, so it cannot tell the
///    two apart. Unset, misspelt or "production" all fail closed.
/// 2. Only for addresses in `Otp:DevCodeEmails`, a comma-separated list of
///    suffixes owned by testing. No list, no bypass — an unfenced fixed code is
///    worth everything, so it is made worth nothing instead. Development is the
///    one exemption: a laptop has no users on it.
/// 3. Only when a code was actually requested (the caller checks that a live
///    challenge exists). It short-circuits delivery, not the flow.
///
/// Every acceptance is logged at warning, because a bypass that leaves no
/// trace is one nobody notices being used.
public sealed class DevOtpCodePolicy(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevOtpCodePolicy> logger)
{
    /// The deployment this instance belongs to, which is not the same thing as
    /// the ASPNETCORE_ENVIRONMENT name — that is Production on the staging box
    /// too. Absent means production, because absent must be the safe answer.
    private string Deployment =>
        (configuration["Deployment:Environment"] ?? "production").Trim().ToLowerInvariant();

    public bool Accepts(string email, string presentedCode)
    {
        if (!environment.IsDevelopment() && Deployment != "staging") return false;

        var devCode = configuration["Otp:DevCode"];
        if (string.IsNullOrEmpty(devCode)) return false;
        if (!string.Equals(presentedCode, devCode, StringComparison.Ordinal)) return false;

        var suffixes = (configuration["Otp:DevCodeEmails"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (suffixes.Length == 0)
        {
            // A laptop has no users on it, and listing every address a
            // developer types while testing would be busywork. Anything
            // deployed — staging included — must name the addresses.
            if (environment.IsDevelopment()) return true;
            logger.LogWarning(
                "Otp:DevCode is set in {Deployment} with no Otp:DevCodeEmails allowlist — refusing it",
                Deployment);
            return false;
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (!suffixes.Any(s => normalized.EndsWith(s.ToLowerInvariant(), StringComparison.Ordinal))) return false;

        logger.LogWarning("Otp:DevCode accepted in {Deployment} for an allowlisted test address", Deployment);
        return true;
    }
}
