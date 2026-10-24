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
/// 1. Only on a Staging or Development host (ASPNETCORE_ENVIRONMENT). Unset,
///    misspelt or Production all fail closed: ASP.NET reads an unset name as
///    Production.
/// 2. Only for addresses in `Otp:DevCodeEmails`, a comma-separated list of
///    domains owned by testing (`@balsm.health`). Entries are matched as
///    "@domain", so a bare domain cannot also match a lookalike, and a listed
///    domain does not imply its subdomains. No list, no bypass — an unfenced fixed code is
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
    public bool Accepts(string email, string presentedCode)
    {
        if (!environment.IsDevelopment() && !environment.IsStaging()) return false;

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
                "Otp:DevCode is set in {Environment} with no Otp:DevCodeEmails allowlist — refusing it",
                environment.EnvironmentName);
            return false;
        }

        var normalized = email.Trim().ToLowerInvariant();
        // Entries are normalised to "@domain" before matching. A bare
        // "balsm.health" as a plain suffix would also match an attacker's
        // evilbalsm.health, and a list meant to name one organisation would
        // quietly name every domain ending in those characters.
        if (!suffixes.Any(entry =>
            {
                var suffix = entry.ToLowerInvariant();
                if (!suffix.StartsWith('@')) suffix = '@' + suffix;
                return normalized.EndsWith(suffix, StringComparison.Ordinal);
            }))
        {
            return false;
        }

        logger.LogWarning("Otp:DevCode accepted in {Environment} for an allowlisted test address", environment.EnvironmentName);
        return true;
    }
}
