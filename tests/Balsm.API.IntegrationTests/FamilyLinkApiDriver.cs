using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.SharedKernel.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Drives the family-link routes over real HTTP as distinct, synthetic patient
/// accounts (provisioned through the Account module, signed in with real JWTs).
/// </summary>
public sealed class FamilyLinkApiDriver(WebApplicationFactory<Program> factory)
{
    public const string Base = "/api/v1/family-links";

    /// <summary>A provisioned account with its own authenticated client.</summary>
    public sealed record Patient(Guid UserId, HttpClient Client);

    public async Task<Patient> NewPatientAsync(string? firstName = null, string? handle = null)
    {
        using var scope = factory.Services.CreateScope();
        var userId = await scope.ServiceProvider.GetRequiredService<IUserAccountProvisioner>().ProvisionAsync("EG", "en");
        if (firstName is not null || handle is not null)
        {
            var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var account = await db.UserAccounts.SingleAsync(a => a.Id == userId);
            if (firstName is not null) account.UpdateProfile(firstName: firstName, lastName: "Doe");
            if (handle is not null) account.ClaimHandle(handle);
            await db.SaveChangesAsync();
        }

        var token = scope.ServiceProvider.GetRequiredService<JwtService>().IssueAccessToken(userId, $"{userId:N}@example.test");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Patient(userId, client);
    }

    /// <summary>Mints the patient's profile QR (client-minted jti, ciphertext opaque to the server).</summary>
    public static async Task<Guid> MintProfileQrAsync(Patient patient)
    {
        var jti = Guid.NewGuid();
        var response = await patient.Client.PostAsJsonAsync("/emergency-qr/mint", new
        {
            ciphertext = Convert.ToBase64String(new byte[48]),
            profile_etag = "etag-1",
            ttl_seconds = 0,
            token_id = jti,
        });
        response.EnsureSuccessStatusCode();
        return jti;
    }

    public static Task<HttpResponseMessage> CreateAsync(Patient requester, Guid id, Guid jti, string relation = "child", byte[]? key = null) =>
        requester.Client.PostAsJsonAsync(Base, new
        {
            id,
            jti,
            relation,
            public_key = key is null ? null : Convert.ToBase64String(key),
        });

    public static Task<HttpResponseMessage> PutCardAsync(Patient owner, Guid id, int version, int ciphertextBytes = 64) =>
        owner.Client.PutAsJsonAsync($"{Base}/{id}/card", new
        {
            version,
            ephemeral_public_key = Convert.ToBase64String(Key(4)),
            nonce = Convert.ToBase64String(new byte[12]),
            ciphertext = Convert.ToBase64String(Enumerable.Repeat((byte)version, ciphertextBytes).ToArray()),
        });

    /// <summary>A pending link from <paramref name="requester"/> to <paramref name="recipient"/>; returns its id.</summary>
    public async Task<Guid> RequestAsync(Patient requester, Patient recipient)
    {
        var id = Guid.NewGuid();
        var response = await CreateAsync(requester, id, await MintProfileQrAsync(recipient), key: Key(1));
        response.EnsureSuccessStatusCode();
        return id;
    }

    public async Task<Guid> LinkAsync(Patient requester, Patient recipient)
    {
        var id = await RequestAsync(requester, recipient);
        (await recipient.Client.PostAsJsonAsync($"{Base}/{id}/approve", new { public_key = Convert.ToBase64String(Key(2)) }))
            .EnsureSuccessStatusCode();
        return id;
    }

    public static byte[] Key(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

    public static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
