namespace VirtualActors.Tests;

using Orleans.Hosting;
using Orleans.TestingHost;
using Xunit;

/// <summary>
/// Provides an in-process Orleans cluster for virtual actor tests.
/// </summary>
public sealed class OrleansClusterFixture : IAsyncLifetime {
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Gets the Orleans test cluster.
    /// </summary>
    public InProcessTestCluster Cluster { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync() {
        var builder = new InProcessTestClusterBuilder();

        builder.ConfigureSilo((_, siloBuilder) => {
            // Match the named provider used by the grains while keeping tests isolated.
            siloBuilder.AddMemoryGrainStorage(StorageProviderName);
        });

        Cluster = builder.Build();

        await Cluster
            .DeployAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() {
        await Cluster
            .DisposeAsync()
            .ConfigureAwait(false);
    }
}
