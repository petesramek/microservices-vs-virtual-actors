namespace Ordering.Persistence.Sqlite.Internal.Infrastructure;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ordering.Persistence.Sqlite.Internal.Observability.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;
using System.Globalization;

/// <summary>
/// Stores serialized Orleans grain state in SQLite through Entity Framework Core.
/// </summary>
/// <remarks>
/// The provider stores an integer version and exposes its invariant string form
/// as the Orleans ETag. Writes and clears compare the supplied ETag before the
/// database operation, while Entity Framework Core concurrency handling protects
/// the interval between reading and saving a tracked entity.
/// </remarks>
internal sealed class SqliteGrainStorage :
    IGrainStorage,
    ILifecycleParticipant<ISiloLifecycle> {
    /// <summary>
    /// Identifies a grain-state write operation in conflict diagnostics.
    /// </summary>
    private const string WriteOperation = "write";

    /// <summary>
    /// Identifies a grain-state clear operation in conflict diagnostics.
    /// </summary>
    private const string ClearOperation = "clear";

    /// <summary>
    /// Enables SQLite write-ahead logging for the grain-state database.
    /// </summary>
    private const string EnableWriteAheadLoggingCommand = "PRAGMA journal_mode=WAL;";

    /// <summary>
    /// Defines the first provider-managed version exposed as an Orleans ETag.
    /// </summary>
    private const int InitialVersion = 1;

    /// <summary>
    /// Identifies SQLite's extended primary-key constraint result code.
    /// </summary>
    /// <remarks>
    /// A primary-key conflict during insert means that another writer created
    /// the same grain-state record first. Other constraint failures are allowed
    /// to propagate as database errors instead of being reported as stale state.
    /// </remarks>
    private const int SqlitePrimaryKeyConstraintErrorCode = 1555;

    /// <summary>
    /// Serializes schema initialization and write operations performed by this
    /// provider within the current process.
    /// </summary>
    /// <remarks>
    /// Database-level ETag and concurrency checks remain necessary because this
    /// lock does not coordinate writers in other processes.
    /// </remarks>
    private static readonly SemaphoreSlim WriteLock = new(
        initialCount: 1,
        maxCount: 1);

    /// <summary>
    /// Identifies the registered Orleans storage provider.
    /// </summary>
    private readonly string _storageName;

    /// <summary>
    /// Identifies the Orleans service whose grain state is stored.
    /// </summary>
    private readonly string _serviceId;

    /// <summary>
    /// Creates database contexts for independent persistence operations.
    /// </summary>
    private readonly IDbContextFactory<GrainStateDbContext> _dbContextFactory;

    /// <summary>
    /// Serializes and deserializes Orleans grain-state payloads.
    /// </summary>
    private readonly IGrainStorageSerializer _serializer;

    /// <summary>
    /// Writes storage-provider lifecycle events.
    /// </summary>
    private readonly ILogger<SqliteGrainStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteGrainStorage"/> class.
    /// </summary>
    /// <param name="storageName">The registered storage provider name.</param>
    /// <param name="clusterOptions">The Orleans cluster options.</param>
    /// <param name="dbContextFactory">The grain state database context factory.</param>
    /// <param name="serializer">The Orleans grain storage serializer.</param>
    /// <param name="logger">The storage provider logger.</param>
    public SqliteGrainStorage(
        string storageName,
        IOptions<ClusterOptions> clusterOptions,
        IDbContextFactory<GrainStateDbContext> dbContextFactory,
        IGrainStorageSerializer serializer,
        ILogger<SqliteGrainStorage> logger) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageName);
        ArgumentNullException.ThrowIfNull(clusterOptions);
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(logger);

        _storageName = storageName;
        _serviceId = clusterOptions.Value.ServiceId;
        _dbContextFactory = dbContextFactory;
        _serializer = serializer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReadStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(grainState);

        GrainStateDbContext context = await _dbContextFactory
            .CreateDbContextAsync()
            .ConfigureAwait(false);

        await using (context.ConfigureAwait(false)) {
            GrainStateEntity? entity = await FindStateAsync(
                context,
                stateName,
                grainId)
                .ConfigureAwait(false);

            if (entity is null) {
                grainState.ETag = null!;
                grainState.RecordExists = false;
                return;
            }

            grainState.State = _serializer.Deserialize<T>(new BinaryData(entity.Payload));
            grainState.ETag = FormatVersion(entity.Version);
            grainState.RecordExists = true;
        }
    }

    /// <inheritdoc />
    public async Task WriteStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(grainState);

        await WriteLock
            .WaitAsync()
            .ConfigureAwait(false);

        try {
            GrainStateDbContext context = await _dbContextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

            await using (context.ConfigureAwait(false)) {
                GrainStateEntity? entity = await FindStateAsync(
                context,
                stateName,
                grainId).ConfigureAwait(false);

                byte[] payload = _serializer
                    .Serialize(grainState.State)
                    .ToArray();

                if (entity is null) {
                    await InsertStateAsync(
                        context,
                        stateName,
                        grainId,
                        grainState,
                        payload).ConfigureAwait(false);

                    return;
                }

                string storedETag = FormatVersion(entity.Version);
                EnsureETagMatches(
                    grainState.ETag,
                    storedETag,
                    grainId,
                    WriteOperation);

                entity.Payload = payload;
                entity.Version++;
                entity.ModifiedUtc = DateTimeOffset.UtcNow;

                try {
                    await context
                        .SaveChangesAsync()
                        .ConfigureAwait(false);
                } catch (DbUpdateConcurrencyException exception) {
                    throw CreateInconsistentStateException(
                        grainId,
                        storedETag,
                        grainState.ETag,
                        "write",
                        exception);
                }

                grainState.ETag = FormatVersion(entity.Version);
                grainState.RecordExists = true;
            }
        } finally {
            WriteLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(grainState);

        await WriteLock
            .WaitAsync()
            .ConfigureAwait(false);

        try {
            GrainStateDbContext context = await _dbContextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

            await using (context.ConfigureAwait(false)) {
                GrainStateEntity? entity = await FindStateAsync(
                context,
                stateName,
                grainId).ConfigureAwait(false);

                if (entity is null) {
                    grainState.ETag = null!;
                    grainState.RecordExists = false;
                    return;
                }

                string storedETag = FormatVersion(entity.Version);
                EnsureETagMatches(
                    grainState.ETag,
                    storedETag,
                    grainId,
                    ClearOperation);

                context.GrainStates.Remove(entity);

                try {
                    await context
                        .SaveChangesAsync()
                        .ConfigureAwait(false);
                } catch (DbUpdateConcurrencyException exception) {
                    throw CreateInconsistentStateException(
                        grainId,
                        storedETag,
                        grainState.ETag,
                        "clear",
                        exception);
                }

                grainState.ETag = null!;
                grainState.RecordExists = false;
            }
        } finally {
            WriteLock.Release();
        }
    }

    /// <inheritdoc />
    public void Participate(ISiloLifecycle lifecycle) {
        ArgumentNullException.ThrowIfNull(lifecycle);

        lifecycle.Subscribe(
            $"{nameof(SqliteGrainStorage)}-{_storageName}",
            ServiceLifecycleStage.ApplicationServices,
            InitializeAsync);
    }

    /// <summary>
    /// Applies pending database migrations, enables SQLite write-ahead logging,
    /// and records successful provider initialization.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token that cancels silo lifecycle initialization.
    /// </param>
    /// <returns>A task that represents the initialization operation.</returns>
    private async Task InitializeAsync(CancellationToken cancellationToken) {
        await WriteLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try {
            GrainStateDbContext context = await _dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (context.ConfigureAwait(false)) {
                await context.Database
                    .MigrateAsync(cancellationToken)
                    .ConfigureAwait(false);

                await context.Database
                    .ExecuteSqlRawAsync(
                    EnableWriteAheadLoggingCommand,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        } finally {
            WriteLock.Release();
        }

        _logger.StorageProviderInitializedForService(_storageName, _serviceId);
    }

    /// <summary>
    /// Inserts a new serialized grain-state record.
    /// </summary>
    /// <typeparam name="T">The grain-state type.</typeparam>
    /// <param name="context">The database context used for the insert.</param>
    /// <param name="stateName">The persistent-state name.</param>
    /// <param name="grainId">The Orleans grain identifier.</param>
    /// <param name="grainState">
    /// The Orleans state wrapper that receives the new ETag and record status.
    /// </param>
    /// <param name="payload">The serialized grain-state payload.</param>
    /// <returns>A task that represents the insert operation.</returns>
    /// <exception cref="InconsistentStateException">
    /// The supplied state already has an ETag, or another writer inserts the
    /// same grain-state record first.
    /// </exception>
    private async Task InsertStateAsync<T>(
        GrainStateDbContext context,
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState,
        byte[] payload) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(stateName);
        ArgumentNullException.ThrowIfNull(grainState);
        ArgumentNullException.ThrowIfNull(payload);

        if (!string.IsNullOrEmpty(grainState.ETag)) {
            throw CreateInconsistentStateException(
                grainId,
                storedETag: null,
                grainState.ETag,
                WriteOperation);
        }

        var entity = new GrainStateEntity {
            ServiceId = _serviceId,
            ProviderName = _storageName,
            StateName = stateName,
            GrainType = grainId.Type.ToString(),
            GrainId = grainId.Key.ToString(),
            Payload = payload,
            Version = InitialVersion,
            ModifiedUtc = DateTimeOffset.UtcNow,
        };

        context.GrainStates.Add(entity);

        try {
            await context
                .SaveChangesAsync()
                .ConfigureAwait(false);
        } catch (DbUpdateException exception)
              when (exception.InnerException is SqliteException {
                  SqliteExtendedErrorCode:
                      SqlitePrimaryKeyConstraintErrorCode,
              }) {
            throw CreateInconsistentStateException(
                grainId,
                storedETag: null,
                grainState.ETag,
                WriteOperation,
                exception);
        }

        grainState.ETag = FormatVersion(entity.Version);
        grainState.RecordExists = true;
    }

    /// <summary>
    /// Finds one grain-state entity by its complete storage identity.
    /// </summary>
    /// <param name="context">The database context used for the query.</param>
    /// <param name="stateName">The persistent-state name.</param>
    /// <param name="grainId">The Orleans grain identifier.</param>
    /// <returns>
    /// A task whose result is the matching entity, or <see langword="null"/>
    /// when no record exists.
    /// </returns>
    private Task<GrainStateEntity?> FindStateAsync(
        GrainStateDbContext context,
        string stateName,
        GrainId grainId) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(stateName);

        string grainType = grainId.Type.ToString();
        string grainKey = grainId.Key.ToString();

        return context.GrainStates.SingleOrDefaultAsync(entity =>
            entity.ServiceId == _serviceId
            && entity.ProviderName == _storageName
            && entity.StateName == stateName
            && entity.GrainType == grainType
            && entity.GrainId == grainKey);
    }

    /// <summary>
    /// Verifies that the ETag supplied by Orleans matches the persisted version.
    /// </summary>
    /// <param name="currentETag">The ETag supplied by Orleans.</param>
    /// <param name="storedETag">The ETag derived from the stored version.</param>
    /// <param name="grainId">The Orleans grain identifier.</param>
    /// <param name="operation">The persistence operation being performed.</param>
    /// <exception cref="InconsistentStateException">
    /// <paramref name="currentETag"/> does not match
    /// <paramref name="storedETag"/>.
    /// </exception>
    private static void EnsureETagMatches(
        string? currentETag,
        string storedETag,
        GrainId grainId,
        string operation) {
        if (!string.Equals(
            currentETag,
            storedETag,
            StringComparison.Ordinal)) {
            throw CreateInconsistentStateException(
                grainId,
                storedETag,
                currentETag,
                operation);
        }
    }

    /// <summary>
    /// Creates the Orleans exception used to report an ETag conflict.
    /// </summary>
    /// <param name="grainId">The Orleans grain identifier.</param>
    /// <param name="storedETag">The ETag currently stored, when available.</param>
    /// <param name="currentETag">The ETag supplied by Orleans.</param>
    /// <param name="operation">The persistence operation that failed.</param>
    /// <param name="innerException">
    /// The optional database exception that detected the conflict.
    /// </param>
    /// <returns>An exception containing the conflicting ETag values.</returns>
    private static InconsistentStateException CreateInconsistentStateException(
        GrainId grainId,
        string? storedETag,
        string? currentETag,
        string operation,
        Exception? innerException = null) {
        string message =
            $"Grain state {operation} failed because its ETag is inconsistent. "
            + string.Create(CultureInfo.InvariantCulture, $"GrainId={grainId}.");

        return innerException is null
            ? new InconsistentStateException(
                message,
                storedETag,
                currentETag)
            : new InconsistentStateException(
                message,
                storedETag,
                currentETag,
                innerException);
    }

    /// <summary>
    /// Formats a provider-managed version as an Orleans ETag.
    /// </summary>
    /// <param name="version">The persisted provider version.</param>
    /// <returns>The invariant string representation of the version.</returns>
    private static string FormatVersion(int version) {
        return version.ToString(CultureInfo.InvariantCulture);
    }
}
