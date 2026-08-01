namespace Balsm.Auth.Application.Commands;

/// The caller's intent behind an OTP-request. OTP emails are spent only for
/// these two flows — email-OTP login was removed to conserve email quota.
public enum OtpPurpose
{
    /// A brand-new email verifying ownership at signup. Rejected if the email
    /// already has an identity.
    Register,

    /// An existing user recovering a forgotten password. Silently accepted
    /// (no email) for an unknown email to avoid enumeration.
    Reset,
}
