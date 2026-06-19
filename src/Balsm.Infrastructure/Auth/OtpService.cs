using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

public sealed class OtpService(IConfiguration configuration, ILogger<OtpService> logger)
{
    private const int OtpLength = 6;
    private const int ExpiryMinutes = 10;
    private readonly string _hmacSecret = configuration["Otp:HmacSecret"]
        ?? throw new InvalidOperationException("Otp:HmacSecret not configured");
    private readonly string _resendApiKey = configuration["Resend:ApiKey"] ?? string.Empty;

    // Returns the 6-digit code and its HMAC hash for storage
    public (string code, string hash, DateTime expiresAt) Generate()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var hash = ComputeHash(code);
        return (code, hash, DateTime.UtcNow.AddMinutes(ExpiryMinutes));
    }

    // Constant-time comparison to prevent timing attacks
    public bool Verify(string submittedCode, string storedHash)
    {
        var submittedHash = ComputeHash(submittedCode);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    public async Task SendAsync(string email, string code, string language, CancellationToken ct)
    {
        // Template resolution: ar-EG / ar-SA / ar-AE / en
        var lang = language is "ar-EG" or "ar-SA" or "ar-AE" ? language : "en";
        var templatePath = Path.Combine(
            AppContext.BaseDirectory, "Templates", "auth-otp", $"{lang}.html");

        string body;
        if (File.Exists(templatePath))
        {
            var template = await File.ReadAllTextAsync(templatePath, ct);
            body = template.Replace("{{OTP}}", code).Replace("{{EXPIRY_MINUTES}}", ExpiryMinutes.ToString());
        }
        else
        {
            body = $"Your Balsm verification code is: <strong>{code}</strong>. Expires in {ExpiryMinutes} minutes.";
        }

        await SendViaResendAsync(email, "Your Balsm code", body, ct);
    }

    private async Task SendViaResendAsync(string to, string subject, string html, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_resendApiKey))
        {
            logger.LogWarning("Resend API key not configured — OTP email skipped for {Email}", "[redacted]");
            return;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_resendApiKey}");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            from = "Balsm <noreply@balsm.health>",
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
}
