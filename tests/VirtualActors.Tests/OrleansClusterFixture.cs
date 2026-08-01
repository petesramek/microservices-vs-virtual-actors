namespace VirtualActors.Tests;

using Orleans.TestingHost;
using Xunit;

/// <summary>
/// Provides an in-process Orleans cluster for virtual actor tests.
/// </summary>
public sealed class OrleansClusterFixture : IAsyncLifetime {
    /// <summary>
    /// Gets the Orleans test cluster.
    /// </summary>
    public InProcessTestCluster Cluster { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync() {
        var builder = new InProcessTestClusterBuilder();
        Cluster = builder.Build();
        await Cluster.DeployAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() {
        await Cluster.DisposeAsync();
    }
}

/// <summary>
/// Collection fixture definition for Orleans cluster tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OrleansClusterFixtureDefinition : ICollectionFixture<OrleansClusterFixture> {
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = "OrleansCluster";
}
