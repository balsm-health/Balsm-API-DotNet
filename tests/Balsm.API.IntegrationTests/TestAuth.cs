using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.API.IntegrationTests;

/// <summary>A session issued by the API itself, for calling authenticated routes.</summary>
internal sealed record SignedInUser(Guid UserId, Guid DeviceId, string AccessToken, string RefreshToken);

/// <summary>
/// Gets real sessions out of the running host without a mailbox.
///
/// An OTP challenge stores only HMAC hashes, so a test cannot learn the code
/// that <c>/auth/otp/request</c> generated. It writes the challenge itself
/// instead, hashed by the host's own <see cref="OtpService"/>, and then signs
/// in through <c>POST /auth/otp/verify</c> exactly as the app does. That also
/// keeps sign-in off the per-IP OTP request budget.
/// </summary>
internal static class TestAuth
{
    public const string Code = "424242";
    public const string JwtSecret = "test-secret-at-least-32-bytes-long!!";
    public static readonly Guid DeviceId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    public static async Task SeedChallengeAsync(
        WebApplicationFactory<Program> factory, string email, string code = Code, string? linkToken = null)
    {
        using var scope = factory.Services.CreateScope();
        var otp = scope.ServiceProvider.GetRequiredService<OtpService>();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.OtpChallenges.Add(OtpChallenge.Create(
            email,
            otp.HashToken(code),
            DateTime.UtcNow.AddMinutes(10),
            linkToken is null ? null : otp.HashToken(linkToken)));
        await db.SaveChangesAsync();
    }

    public static async Task LockAsync(WebApplicationFactory<Program> factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var lockout = AccountLockout.Create(email, "email");
        for (var i = 0; i < 5; i++) lockout.RecordFailure();
        db.AccountLockouts.Add(lockout);
        await db.SaveChangesAsync();
    }

    public static async Task<SignedInUser> SignInAsync(WebApplicationFactory<Program> factory, string email)
    {
        await SeedChallengeAsync(factory, email);
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/otp/verify", new Dictionary<string, object>
        {
            ["email"] = email,
            ["code"] = Code,
            ["device_id"] = DeviceId,
            ["device_label"] = "Test device",
        });
        response.EnsureSuccessStatusCode();
        var data = await ReadDataAsync(response);
        return new SignedInUser(
            data.GetProperty("user_id").GetGuid(),
            DeviceId,
            data.GetProperty("access_token").GetString()!,
            data.GetProperty("refresh_token").GetString()!);
    }

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// A token signed with the test host's secret, for callers the auth flow
    /// does not mint — the admin mirror is keyed by the <c>name</c> claim.
    /// </summary>
    public static string MintToken(params Claim[] claims)
    {
        var token = new JwtSecurityToken(
            issuer: "balsm-test",
            audience: "balsm-app",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
