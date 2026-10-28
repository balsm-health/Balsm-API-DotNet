using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// The container-free host with the admin portal switched off, as on a cloud
/// run. The admin-auth middleware is then not in the pipeline, so the Identity
/// module's <c>/api/v1/admin/users</c> routes are reached directly.
/// </summary>
public sealed class AdminPortalOffWebAppFactory : LiteWebAppFactory
{
    protected override IReadOnlyDictionary<string, string> AdditionalSettings { get; } =
        new Dictionary<string, string> { ["AdminPortal:Enabled"] = "false" };
}

/// <summary>Shares one admin-portal-off host across the tests that need it.</summary>
[CollectionDefinition(nameof(AdminPortalOffWebAppCollection))]
public sealed class AdminPortalOffWebAppCollection : ICollectionFixture<AdminPortalOffWebAppFactory>;

/// <summary>
/// A host of its own for the auth endpoints. <c>POST /auth/otp/request</c>
/// counts every call against one per-IP budget (10 an hour, and every test
/// request arrives from the same address), so the auth tests must not share
/// that budget with the rest of the suite.
/// </summary>
[CollectionDefinition(nameof(AuthWebAppCollection))]
public sealed class AuthWebAppCollection : ICollectionFixture<LiteWebAppFactory>;
