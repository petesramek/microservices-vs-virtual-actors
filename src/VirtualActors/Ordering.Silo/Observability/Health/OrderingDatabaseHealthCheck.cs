namespace Ordering.Silo.Observability.Health;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Verifies that the Ordering grain-state database is accessible.
/// </summary>
internal sealed class OrderingDatabaseHealthCheck(
    IConfiguration configuration)
    : IHealthCheck {
    private const string ConnectionName = "Default";
    private const string ValidationCommandText = "SELECT 1;";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        string? connectionString = configuration.GetConnectionString(
            ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString)) {
            return HealthCheckResult.Unhealthy(
                "The Ordering database connection string is not configured.");
        }

        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(Timeout);
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

        try {
            await using var connection = new SqliteConnection(connectionString);

            await connection
                .OpenAsync(linkedCancellationTokenSource.Token)
                .ConfigureAwait(false);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = ValidationCommandText;

            await command
                .ExecuteScalarAsync(linkedCancellationTokenSource.Token)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                "The Ordering database is available.");
        } catch (OperationCanceledException)
              when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException exception) {
            return HealthCheckResult.Unhealthy(
                "The Ordering database health check timed out.",
                exception);
        } catch (Exception exception) {
            return HealthCheckResult.Unhealthy(
                "The Ordering database health check failed.",
                exception);
        }
    }
}
