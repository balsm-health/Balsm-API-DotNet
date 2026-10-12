namespace Balsm.Auth.Application.Commands;

/// The caller's intent behind an OTP-request. It decides whether an email is
/// sent at all, which words it carries, and where its link lands in the app.
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

    /// Proving it is them before deleting their account, from the public
    /// delete-account page. Verifies exactly like <see cref="Continue"/>; what
    /// differs is the email, which says an account deletion was asked for, and
    /// that nothing is sent for an address with no account — there is nothing
    /// to delete, and a code would create one.
    DeleteAccount,
}

public static class OtpPurposeWire
{
    /// The purpose for a wire value, or null when it names none. `register` is
    /// what clients in the field send for <see cref="OtpPurpose.Continue"/>.
    public static OtpPurpose? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "continue" or "register" => OtpPurpose.Continue,
        "reset" => OtpPurpose.Reset,
        "delete" or "delete_account" or "deleteaccount" => OtpPurpose.DeleteAccount,
        _ => null,
    };

    /// The short name the magic link carries (`p=`) so the app knows which
    /// screen the link belongs to. Null for a plain sign-in, which carries none.
    public static string? LinkCode(OtpPurpose purpose) => purpose switch
    {
        OtpPurpose.Reset => "reset",
        OtpPurpose.DeleteAccount => "delete",
        _ => null,
    };

    /// A `p=` value read back from a link, or null when it is not one of ours.
    public static string? NormalizeLinkCode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "reset" => "reset",
        "delete" => "delete",
        _ => null,
    };
}
