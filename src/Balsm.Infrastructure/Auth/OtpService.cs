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

    public async Task SendAsync(string email, string code, string linkUrl, string language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_resendApiKey))
        {
            // Dev/test only: no email provider configured, so surface the code
            // in logs to keep the OTP flow usable end-to-end. In production the
            // Resend key is set and this branch never runs. Logs the code only —
            // never the email (PHI).
            logger.LogWarning("Resend not configured — dev OTP code: {Code}", code);
            return;
        }

        // Template resolution: ar-EG / ar-SA / ar-AE / en
        var lang = language is "ar-EG" or "ar-SA" or "ar-AE" ? language : "en";
        var templatePath = Path.Combine(
            AppContext.BaseDirectory, "Templates", "auth-otp", $"{lang}.html");

        string body;
        if (File.Exists(templatePath))
        {
            var template = await File.ReadAllTextAsync(templatePath, ct);
            body = template
                .Replace("{{OTP}}", code)
                .Replace("{{EXPIRY_MINUTES}}", ExpiryMinutes.ToString())
                .Replace("{{LINK_URL}}", linkUrl);
        }
        else
        {
            body = $"Your Balsm verification code is: <strong>{code}</strong>. Expires in {ExpiryMinutes} minutes."
                 + $"\nOr sign in with this link: {linkUrl}";
        }

        await SendViaResendAsync(email, "Your Balsm code", body, ct);
    }

    private async Task SendViaResendAsync(string to, string subject, string html, CancellationToken ct)
    {
        // Callers guarantee a configured key (see SendAsync).
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_resendApiKey}");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            from = _fromAddress,
            to = new[] { to },
            subject,
            html
        });

        var response = await http.PostAsync(
            "https://api.resend.com/emails",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            ct);

        if (!response.IsSuccessStatusCode)
            logger.LogError("Resend email delivery failed: {Status}", response.StatusCode);
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
