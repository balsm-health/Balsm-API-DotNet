using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Shares one <see cref="TestWebAppFactory"/> — and so one Postgres container
/// and one host boot — across every test class that joins this collection.
///
/// Without it each class starts its own container, which is the difference
/// between seconds and minutes as endpoint coverage grows.
/// </summary>
[CollectionDefinition(nameof(TestWebAppCollection))]
public sealed class TestWebAppCollection : ICollectionFixture<TestWebAppFactory>;
