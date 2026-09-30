namespace Balsm.Auth.Application.Commands;

/// The caller's intent behind an OTP-request. OTP emails are spent only for
/// these two flows — email-OTP login was removed to conserve email quota.
public enum OtpPurpose
{
    /// One entry for everyone: the code that follows a failed email+password
    /// attempt. Sent whether or not the address has an account — the client is
    /// not told which, because that is what the merged flow hides. Verify signs
    /// the account in when it exists and creates it when it does not.
    Continue,

    /// An existing user recovering a forgotten password. Silently accepted
    /// (no email) for an unknown email to avoid enumeration.
    Reset,
}
