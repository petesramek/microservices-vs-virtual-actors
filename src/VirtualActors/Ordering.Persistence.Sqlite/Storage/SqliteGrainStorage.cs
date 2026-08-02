namespace Ordering.Persistence.Sqlite.Storage;

using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;

/// <summary>
/// Stores serialized Orleans grain state in SQLite through Entity Framework Core.
/// </summary>
internal sealed class SqliteGrainStorage :
    IGrainStorage,
    ILifecycleParticipant<ISiloLifecycle> {
    private static readonly SemaphoreSlim WriteLock = new(
        initialCount: 1,
        maxCount: 1);

    private readonly string _storageName;
    private readonly string _serviceId;
    private readonly IDbContextFactory<GrainStateDbContext> _dbContextFactory;
    private readonly IGrainStorageSerializer _serializer;
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

        using GrainStateDbContext context = await _dbContextFactory
            .CreateDbContextAsync()
            .ConfigureAwait(false);

        GrainStateEntity? entity = await FindStateAsync(
            context,
            stateName,
            grainId).ConfigureAwait(false);

        if (entity is null) {
            grainState.ETag = null!;
            grainState.RecordExists = false;
            return;
        }

        grainState.State = _serializer.Deserialize<T>(
            new BinaryData(entity.Payload));
        grainState.ETag = FormatVersion(entity.Version);
        grainState.RecordExists = true;
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
            using GrainStateDbContext context = await _dbContextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

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
            EnsureETagMatches(grainState.ETag, storedETag, grainId, "write");

            entity.Payload = payload;
            entity.Version++;
            entity.ModifiedUtc = DateTimeOffset.UtcNow;

            try {
                await context
                    .SaveChangesAsync()
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException exception) {
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
        finally {
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
            using GrainStateDbContext context = await _dbContextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

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
            EnsureETagMatches(grainState.ETag, storedETag, grainId, "clear");

            context.GrainStates.Remove(entity);

            try {
                await context
                    .SaveChangesAsync()
                    .ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException exception) {
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
        finally {
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
                    "PRAGMA journal_mode=WAL;",
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        } finally {
            WriteLock.Release();
        }

        _logger.LogInformation(
            "SQLite grain storage provider {StorageProviderName} initialized for service {ServiceId}.",
            _storageName,
            _serviceId);
    }

    private async Task InsertStateAsync<T>(
        GrainStateDbContext context,
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState,
        byte[] payload) {
        if (!string.IsNullOrEmpty(grainState.ETag)) {
            throw CreateInconsistentStateException(
                grainId,
                storedETag: null,
                grainState.ETag,
                "write");
        }

        var entity = new GrainStateEntity {
            ServiceId = _serviceId,
            ProviderName = _storageName,
            StateName = stateName,
            GrainType = grainId.Type.ToString(),
            GrainId = grainId.Key.ToString(),
            Payload = payload,
            Version = 1,
            ModifiedUtc = DateTimeOffset.UtcNow,
        };

        context.GrainStates.Add(entity);

        try {
            await context
                .SaveChangesAsync()
                .ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException {
                SqliteErrorCode: 19,
            }) {
            throw CreateInconsistentStateException(
                grainId,
                storedETag: null,
                grainState.ETag,
                "write",
                exception);
        }

        grainState.ETag = FormatVersion(entity.Version);
        grainState.RecordExists = true;
    }

    private Task<GrainStateEntity?> FindStateAsync(
        GrainStateDbContext context,
        string stateName,
        GrainId grainId) {
        string grainType = grainId.Type.ToString();
        string grainKey = grainId.Key.ToString();

        return context.GrainStates.SingleOrDefaultAsync(entity =>
            entity.ServiceId == _serviceId
            && entity.ProviderName == _storageName
            && entity.StateName == stateName
            && entity.GrainType == grainType
            && entity.GrainId == grainKey);
    }

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

    private static InconsistentStateException CreateInconsistentStateException(
        GrainId grainId,
        string? storedETag,
        string? currentETag,
        string operation,
        Exception? innerException = null) {
        string message =
            $"Grain state {operation} failed because its ETag is inconsistent. "
            + $"GrainId={grainId}.";

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

    private static string FormatVersion(int version) {
        return version.ToString(CultureInfo.InvariantCulture);
    }
}
