using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

public sealed class OtpService(IConfiguration configuration, ILogger<OtpService> logger)
{
    private const int OtpLength = 6;
    private const int ExpiryMinutes = 10;
    private const int LinkTokenBytes = 32;
    private readonly string _hmacSecret = configuration["Otp:HmacSecret"]
        ?? throw new InvalidOperationException("Otp:HmacSecret not configured");
    private readonly string _resendApiKey = configuration["Resend:ApiKey"] ?? string.Empty;
    private readonly string _fromAddress = configuration["Resend:From"] ?? "Balsm <noreply@balsm.health>";

    // Returns the 6-digit code and its HMAC hash, plus a URL-safe magic-link token
    // and its HMAC hash, and a shared expiry. Only the hashes are ever persisted;
    // the raw code and token are emailed and never stored.
    public (string code, string codeHash, string linkToken, string linkTokenHash, DateTime expiresAt) Generate()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = ComputeHash(code);

        // URL-safe random link token: 32 bytes -> Base64Url, no padding.
        var linkToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(LinkTokenBytes));
        var linkTokenHash = ComputeHash(linkToken);

        return (code, codeHash, linkToken, linkTokenHash, DateTime.UtcNow.AddMinutes(ExpiryMinutes));
    }

    // Constant-time comparison to prevent timing attacks
    public bool Verify(string submittedCode, string storedHash)
    {
        var submittedHash = ComputeHash(submittedCode);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    // Hashes a raw magic-link token so callers can compare it to the stored
    // LinkTokenHash. The raw token never touches the database.
    public string HashToken(string token) => ComputeHash(token);

    public async Task<OtpDelivery> SendAsync(
        string email, string code, string linkUrl, string language, OtpEmailKind kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_resendApiKey))
        {
            // Dev/test only: no email provider configured, so surface the code
            // in logs to keep the OTP flow usable end-to-end. In production the
            // Resend key is set and this branch never runs. Logs the code only —
            // never the email (PHI).
            logger.LogWarning("Resend not configured — dev OTP code: {Code}", code);
            return OtpDelivery.Sent;
        }

        var lang = OtpEmail.LanguageFor(language);
        var templates = Path.Combine(AppContext.BaseDirectory, "Templates", "auth-otp");
        var layoutPath = Path.Combine(templates, $"{lang}.html");
        var logoPath = Path.Combine(templates, "logo.png");

        OtpEmailMessage message;
        byte[]? logo = null;
        if (File.Exists(layoutPath))
        {
            var layout = await File.ReadAllTextAsync(layoutPath, ct);
            message = OtpEmail.Render(layout, kind, lang, code, linkUrl, ExpiryMinutes, DateTime.UtcNow.Year);
            if (File.Exists(logoPath)) logo = await File.ReadAllBytesAsync(logoPath, ct);
        }
        else
        {
            logger.LogError("OTP email layout missing for {Language}; sending the plain fallback", lang);
            message = OtpEmail.RenderPlain(kind, lang, code, linkUrl, ExpiryMinutes);
        }

        return await SendViaResendAsync(email, message.Subject, message.Html, logo, ct);
    }

    private async Task<OtpDelivery> SendViaResendAsync(string to, string subject, string html, byte[]? logo, CancellationToken ct)
    {
        // Callers guarantee a configured key (see SendAsync).
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_resendApiKey}");

        // The logo travels inside the email and is referenced by content id.
        // Mail clients block or strip the alternatives: a data: URI is dropped
        // by Gmail, and a remote image waits behind "display images".
        var attachments = logo is null
            ? []
            : new[]
            {
                new
                {
                    filename = "balsm.png",
                    content = Convert.ToBase64String(logo),
                    content_type = "image/png",
                    content_id = OtpEmail.LogoContentId,
                },
            };

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            from = _fromAddress,
            to = new[] { to },
            subject,
            html,
            attachments,
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("https://api.resend.com/emails", content, ct);

        if (response.IsSuccessStatusCode) return OtpDelivery.Sent;
        logger.LogError("Resend email delivery failed: {Status}", response.StatusCode);
        // Resend answers 429 for its rate limit and for a spent daily or monthly quota.
        return response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            ? OtpDelivery.OverQuota
            : OtpDelivery.Failed;
    }

    private string ComputeHash(string code)
    {
        var key = Encoding.UTF8.GetBytes(_hmacSecret);
        var data = Encoding.UTF8.GetBytes(code);
        return Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }

    // Base64Url without padding — safe to carry in an email link query string.
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public enum OtpDelivery
{
    Sent,
    Failed,

    /// The provider refused for its rate limit or quota.
    OverQuota,
}
