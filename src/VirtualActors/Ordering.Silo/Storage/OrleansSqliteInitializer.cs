namespace Ordering.Silo.Storage;

using System.Globalization;
using Microsoft.Data.Sqlite;

/// <summary>
/// Initializes the SQLite schema required by Orleans grain persistence.
/// </summary>
internal static class OrleansSqliteInitializer {
    private const string OrleansQueryTableName = "OrleansQuery";
    private const string OrleansStorageTableName = "OrleansStorage";

    private static readonly string[] RequiredQueryKeys = [
        "WriteToStorageKey",
        "ReadFromStorageKey",
        "ClearStorageKey",
    ];

    /// <summary>
    /// Creates the database directory and initializes the Orleans persistence schema.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public static async Task InitializeSchemaAsync(
        string connectionString,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        EnsureDatabaseDirectoryExists(connectionString);

        using var connection = new SqliteConnection(connectionString);

        await connection
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        bool schemaExists = await CheckSchemaAsync(
            connection,
            cancellationToken).ConfigureAwait(false);

        if (schemaExists) {
            return;
        }

        await CreateSchemaAsync(
            connection,
            cancellationToken).ConfigureAwait(false);

        await ValidateOperationalQueriesAsync(
            connection,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> CheckSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) {
        bool queryTableExists = await TableExistsAsync(
            connection,
            OrleansQueryTableName,
            cancellationToken).ConfigureAwait(false);

        bool storageTableExists = await TableExistsAsync(
            connection,
            OrleansStorageTableName,
            cancellationToken).ConfigureAwait(false);

        if (queryTableExists && storageTableExists) {
            await ValidateOperationalQueriesAsync(
                connection,
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (queryTableExists || storageTableExists) {
            throw new InvalidOperationException(
                "The Orleans SQLite persistence schema is only partially initialized. "
                + "Remove or repair the database before starting the silo.");
        }

        return false;
    }

    private static async Task CreateSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) {
        using SqliteTransaction transaction = connection.BeginTransaction();

        try {
            await ExecuteScriptAsync(
                connection,
                transaction,
                OrleansSqliteSchema.Main,
                cancellationToken).ConfigureAwait(false);

            await ExecuteScriptAsync(
                connection,
                transaction,
                OrleansSqliteSchema.Persistence,
                cancellationToken).ConfigureAwait(false);

            await transaction
                .CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch {
            await transaction
                .RollbackAsync(cancellationToken)
                .ConfigureAwait(false);

            throw;
        }
    }

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string script,
        CancellationToken cancellationToken) {
        using SqliteCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = script;

        await command
            .ExecuteNonQueryAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken) {
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;

        command.Parameters.AddWithValue("$tableName", tableName);

        object? result = await command
            .ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        return Convert.ToInt64(result, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task ValidateOperationalQueriesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) {
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*)
            FROM OrleansQuery
            WHERE QueryKey IN (
                $writeKey,
                $readKey,
                $clearKey
            );
            """;

        command.Parameters.AddWithValue("$writeKey", RequiredQueryKeys[0]);
        command.Parameters.AddWithValue("$readKey", RequiredQueryKeys[1]);
        command.Parameters.AddWithValue("$clearKey", RequiredQueryKeys[2]);

        object? result = await command
            .ExecuteScalarAsync(cancellationToken)
            .ConfigureAwait(false);

        long queryCount = Convert.ToInt64(
            result,
            CultureInfo.InvariantCulture);

        if (queryCount != RequiredQueryKeys.Length) {
            throw new InvalidOperationException(
                "The Orleans SQLite persistence schema does not contain "
                + "all required operational queries.");
        }
    }

    private static void EnsureDatabaseDirectoryExists(
        string connectionString) {
        var connectionStringBuilder =
            new SqliteConnectionStringBuilder(connectionString);

        string dataSource = connectionStringBuilder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource)
            || dataSource.Equals(
                ":memory:",
                StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        string databasePath = Path.GetFullPath(dataSource);
        string? directoryPath = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
