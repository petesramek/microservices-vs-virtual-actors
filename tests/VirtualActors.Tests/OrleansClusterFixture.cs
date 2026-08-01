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
        await Cluster.DeployAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() {
        await Cluster.DisposeAsync().ConfigureAwait(false);
    }
}
