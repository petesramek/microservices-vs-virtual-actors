namespace VirtualActors.Tests;

using Microsoft.Data.Sqlite;
using Ordering.Grains.Interfaces;
using Ordering.Persistence.Sqlite.Extensions;
using Orleans.TestingHost;
using Shouldly;
using Xunit;

/// <summary>
/// Verifies that grain state survives an Orleans cluster restart.
/// </summary>
public sealed class SqliteGrainPersistenceTests {
    private const string StorageProviderName = "OrderingStorage";

    /// <summary>
    /// Verifies that persisted inventory state is restored after restarting the cluster.
    /// </summary>
    [Fact]
    public async Task InventoryStateSurvivesClusterRestart() {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"ordering-grain-state-{Guid.NewGuid():N}.db");
        string connectionString = $"Data Source={databasePath}";
        string productId = $"product-{Guid.NewGuid():N}";
        string serviceId = $"ordering-persistence-{Guid.NewGuid():N}";
        string clusterId = $"ordering-persistence-{Guid.NewGuid():N}";

        try {
            InProcessTestCluster firstCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId).ConfigureAwait(false);

            try {
                IInventoryItemGrain inventory =
                    firstCluster.Client.GetGrain<IInventoryItemGrain>(productId);

                await inventory
                    .ResetAsync(10)
                    .ConfigureAwait(false);

                await inventory
                    .ReserveAsync(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        quantity: 3)
                    .ConfigureAwait(false);
            }
            finally {
                await firstCluster
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }

            InProcessTestCluster secondCluster = await StartClusterAsync(
                connectionString,
                serviceId,
                clusterId).ConfigureAwait(false);

            try {
                IInventoryItemGrain inventory =
                    secondCluster.Client.GetGrain<IInventoryItemGrain>(productId);

                int availableQuantity = (await inventory
                    .GetAsync()
                    .ConfigureAwait(false))
                    .AvailableQuantity;

                availableQuantity.ShouldBe(7);
            }
            finally {
                await secondCluster
                    .DisposeAsync()
                    .ConfigureAwait(false);
            }
        }
        finally {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static async Task<InProcessTestCluster> StartClusterAsync(
        string connectionString,
        string serviceId,
        string clusterId) {
        var builder = new InProcessTestClusterBuilder();

        builder.Options.ServiceId = serviceId;
        builder.Options.ClusterId = clusterId;

        builder.ConfigureSilo((_, siloBuilder) => {
            siloBuilder.AddSqliteGrainStorage(
                StorageProviderName,
                connectionString);
        });

        InProcessTestCluster cluster = builder.Build();

        await cluster
            .DeployAsync()
            .ConfigureAwait(false);

        return cluster;
    }

    private static void DeleteDatabaseFiles(string databasePath) {
        // EF Core uses pooled SQLite connections, which can retain the file handle
        // after the test clusters and their service providers have been disposed.
        SqliteConnection.ClearAllPools();

        foreach (string path in new[] {
            databasePath,
            $"{databasePath}-shm",
            $"{databasePath}-wal",
        }) {
            DeleteFileIfExists(path);
        }
    }

    private static void DeleteFileIfExists(string path) {
        const int MaxAttempts = 5;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException) when (attempt < MaxAttempts) {
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }
}
