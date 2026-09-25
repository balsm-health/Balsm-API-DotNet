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
/// 1. Never in production. Configuration discipline is not a control.
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
    public bool Accepts(string email, string presentedCode)
    {
        if (environment.IsProduction()) return false;

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
        if (!suffixes.Any(s => normalized.EndsWith(s.ToLowerInvariant(), StringComparison.Ordinal))) return false;

        logger.LogWarning("Otp:DevCode accepted in {Environment} for an allowlisted test address",
            environment.EnvironmentName);
        return true;
    }
}
