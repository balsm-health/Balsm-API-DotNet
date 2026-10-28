using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Entity module admin routes (<c>/api/v1/admin/{workspace,entity-types,entities,branches}</c>)
/// over real HTTP, on the host with the admin portal off so requests reach the
/// controllers. Each entity/branch test creates its own rows under a fresh
/// workspace id, so tests never depend on one another's order.
/// </summary>
[Collection(nameof(AdminPortalOffWebAppCollection))]
public sealed class EntityEndpointTests(AdminPortalOffWebAppFactory factory)
{
    [Fact]
    public async Task ListEntityTypes_Always_ReturnsTheSeededTypes()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/api/v1/admin/entity-types");

        var codes = (await ReadJsonAsync(response)).EnumerateArray().Select(t => t.GetProperty("code").GetString());
        codes.Should().Contain(["pharmacy", "clinic", "hospital"]);
    }

    [Fact]
    public async Task Workspace_CreateThenReadAndUpdate_ReturnsTheUpdatedWorkspace()
    {
        var client = AdminClient();
        await EnsureWorkspaceAsync(client);
        var id = (await ReadJsonAsync(await client.GetAsync("/api/v1/admin/workspace"))).GetProperty("id").GetGuid();

        var response = await client.PatchAsJsonAsync($"/api/v1/admin/workspace/{id}", new Dictionary<string, string?>
        {
            ["name"] = "Example Pharmacy Group",
            ["locale"] = null,
        });

        var data = await ReadJsonAsync(response);
        data.GetProperty("name").GetString().Should().Be("Example Pharmacy Group");
        data.GetProperty("locale_default").GetString().Should().Be("en");
    }

    [Fact]
    public async Task CreateWorkspace_WhenOneExists_Returns409()
    {
        var client = AdminClient();
        await EnsureWorkspaceAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/admin/workspace", WorkspaceBody());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateWorkspace_UnknownId_Returns404()
    {
        var client = AdminClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/workspace/{Guid.NewGuid()}", new Dictionary<string, string> { ["name"] = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEntity_KnownType_ReturnsTheEntity()
    {
        var client = AdminClient();
        var workspaceId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/v1/admin/entities", EntityBody(workspaceId, "pharmacy"));

        var data = await ReadJsonAsync(response);
        data.GetProperty("workspace_id").GetGuid().Should().Be(workspaceId);
        data.GetProperty("type_code").GetString().Should().Be("pharmacy");
    }

    [Fact]
    public async Task CreateEntity_UnknownType_Returns404()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/entities", EntityBody(Guid.NewGuid(), "spaceport"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEntity_Existing_ReturnsIt()
    {
        var client = AdminClient();
        var id = await CreateEntityAsync(client, Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/admin/entities/{id}");

        (await ReadJsonAsync(response)).GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task GetEntity_Unknown_Returns404()
    {
        var response = await AdminClient().GetAsync($"/api/v1/admin/entities/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEntity_Existing_ReturnsTheNewName()
    {
        var client = AdminClient();
        var id = await CreateEntityAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"/api/v1/admin/entities/{id}", new Dictionary<string, string>
        {
            ["name"] = "Renamed Example Clinic",
            ["registration_number"] = "REG-000002",
        });

        (await ReadJsonAsync(response)).GetProperty("name").GetString().Should().Be("Renamed Example Clinic");
    }

    [Fact]
    public async Task UpdateEntity_Unknown_Returns404()
    {
        var response = await AdminClient().PatchAsJsonAsync(
            $"/api/v1/admin/entities/{Guid.NewGuid()}", new Dictionary<string, string> { ["name"] = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateEntity_Existing_HidesItUnlessInactiveAreIncluded()
    {
        var client = AdminClient();
        var workspaceId = Guid.NewGuid();
        var id = await CreateEntityAsync(client, workspaceId);

        var deactivate = await client.PostAsync($"/api/v1/admin/entities/{id}/deactivate", content: null);

        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities?workspaceId={workspaceId}")))
            .GetArrayLength().Should().Be(0);
        (await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities?workspaceId={workspaceId}&includeInactive=true")))
            .EnumerateArray().Single().GetProperty("is_deleted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateEntity_Unknown_Returns404()
    {
        var response = await AdminClient().PostAsync($"/api/v1/admin/entities/{Guid.NewGuid()}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReactivateEntity_Deactivated_ListsItAgain()
    {
        var client = AdminClient();
        var workspaceId = Guid.NewGuid();
        var id = await CreateEntityAsync(client, workspaceId);
        await client.PostAsync($"/api/v1/admin/entities/{id}/deactivate", content: null);

        var response = await client.PostAsync($"/api/v1/admin/entities/{id}/reactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities?workspaceId={workspaceId}")))
            .GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ReactivateEntity_Unknown_Returns404()
    {
        var response = await AdminClient().PostAsync($"/api/v1/admin/entities/{Guid.NewGuid()}/reactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBranch_ForExistingEntity_ListsTheBranch()
    {
        var client = AdminClient();
        var entityId = await CreateEntityAsync(client, Guid.NewGuid());

        var branchId = await CreateBranchAsync(client, entityId);

        var branches = await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities/{entityId}/branches"));
        branches.EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(branchId);
    }

    [Fact]
    public async Task CreateBranch_ForUnknownEntity_Returns404()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/v1/admin/branches", BranchBody(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateBranch_Existing_ReturnsTheNewDetails()
    {
        var client = AdminClient();
        var branchId = await CreateBranchAsync(client, await CreateEntityAsync(client, Guid.NewGuid()));

        var response = await client.PatchAsJsonAsync($"/api/v1/admin/branches/{branchId}", new Dictionary<string, string>
        {
            ["name"] = "Example Branch North",
            ["city"] = "Example City",
        });

        var data = await ReadJsonAsync(response);
        data.GetProperty("name").GetString().Should().Be("Example Branch North");
        data.GetProperty("city").GetString().Should().Be("Example City");
    }

    [Fact]
    public async Task UpdateBranch_Unknown_Returns404()
    {
        var response = await AdminClient().PatchAsJsonAsync(
            $"/api/v1/admin/branches/{Guid.NewGuid()}", new Dictionary<string, string> { ["name"] = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateBranch_Existing_HidesItUnlessInactiveAreIncluded()
    {
        var client = AdminClient();
        var entityId = await CreateEntityAsync(client, Guid.NewGuid());
        var branchId = await CreateBranchAsync(client, entityId);

        var response = await client.PostAsync($"/api/v1/admin/branches/{branchId}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities/{entityId}/branches")))
            .GetArrayLength().Should().Be(0);
        (await ReadJsonAsync(await client.GetAsync($"/api/v1/admin/entities/{entityId}/branches?includeInactive=true")))
            .GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task DeactivateBranch_Unknown_Returns404()
    {
        var response = await AdminClient().PostAsync($"/api/v1/admin/branches/{Guid.NewGuid()}/deactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReactivateBranch_Deactivated_ReturnsOk()
    {
        var client = AdminClient();
        var branchId = await CreateBranchAsync(client, await CreateEntityAsync(client, Guid.NewGuid()));
        await client.PostAsync($"/api/v1/admin/branches/{branchId}/deactivate", content: null);

        var response = await client.PostAsync($"/api/v1/admin/branches/{branchId}/reactivate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteBranch_Always_Returns405()
    {
        var response = await AdminClient().DeleteAsync($"/api/v1/admin/branches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    private HttpClient AdminClient() =>
        TestAuth.CreateClient(factory, TestAuth.MintToken(new Claim("unique_name", "entity-admin@example.test")));

    private static async Task EnsureWorkspaceAsync(HttpClient client)
    {
        // The workspace is a singleton on this shared host: whichever test runs
        // first creates it, the other sees the 409 it is about to assert.
        var response = await client.PostAsJsonAsync("/api/v1/admin/workspace", WorkspaceBody());
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
    }

    private static async Task<Guid> CreateEntityAsync(HttpClient client, Guid workspaceId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/entities", EntityBody(workspaceId, "clinic"));
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBranchAsync(HttpClient client, Guid entityId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/branches", BranchBody(entityId));
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static Dictionary<string, string> WorkspaceBody() => new()
    {
        ["name"] = "Example Health Group",
        ["slug"] = "example-health",
        ["locale"] = "en",
    };

    private static Dictionary<string, object> EntityBody(Guid workspaceId, string typeCode) => new()
    {
        ["workspace_id"] = workspaceId,
        ["name"] = "Example Clinic",
        ["type_code"] = typeCode,
        ["registration_number"] = "REG-000001",
    };

    private static Dictionary<string, object> BranchBody(Guid entityId) => new()
    {
        ["entity_root_id"] = entityId,
        ["name"] = "Example Branch",
        ["address_line"] = "1 Example Street",
        ["city"] = "Example Town",
        ["governorate"] = "Example Governorate",
        ["phone"] = "+20 100 000 0000",
    };
}
