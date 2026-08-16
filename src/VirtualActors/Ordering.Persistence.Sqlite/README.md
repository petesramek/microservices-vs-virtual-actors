## Ordering.Persistence.Sqlite

Ordering.Persistence.Sqlite provides a named SQLite-backed `IGrainStorage` implementation for the **Microservices vs Virtual Actors** architecture workbench. It persists serialized Orleans grain state through Entity Framework Core, maps provider-managed row versions to Orleans ETags, applies the database schema during silo startup, and exposes registration extensions for the storage provider and its connectivity health check.

This project does not implement grain behavior or application workflow rules. Its responsibility is to adapt Orleans grain persistence to the repository's local SQLite storage model.

### Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments.
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity.

Ordering.Persistence.Sqlite supports the virtual actor path by storing named Orleans persistent state for the ordering silo. The project is intended for this repository's local architecture workbench and should not be interpreted as a general production persistence recommendation.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

### Responsibilities

The project performs seven main tasks:

- Registers a named SQLite-backed Orleans grain storage provider.
- Registers an optional connectivity-only health check for the grain-state database.
- Serializes and deserializes grain state through Orleans' configured storage serializer.
- Stores grain-state identity, payload, provider-managed version, and modification time.
- Maps the persisted version to the Orleans ETag used for optimistic concurrency.
- Applies Entity Framework Core migrations and enables SQLite write-ahead logging during silo startup.
- Emits a structured informational event after successful provider initialization.

### Project structure

Generated `bin` and `obj` directories are intentionally omitted.

```text
Ordering.Persistence.Sqlite.csproj

Extensions/
  SiloBuilderExtensions.cs

Internal/
  Infrastructure/
    GrainStateDbContext.cs
    GrainStateEntity.cs
    GrainStateEntityConfiguration.cs
    SqliteGrainStorage.cs

  Observability/
    Health/
      SqliteGrainStorageHealthCheck.cs
    Logging/
      LogInformation.cs

Migrations/
  20260802202451_InitialGrainStateSchema.cs
  20260802202451_InitialGrainStateSchema.Designer.cs
  GrainStateDbContextModelSnapshot.cs
```

### Registration

Register storage while configuring the Orleans silo:

```csharp
siloBuilder.AddSqliteGrainStorage(
    storageProviderName,
    connectionString);
```

The extension registers:

- a pooled `IDbContextFactory<GrainStateDbContext>` configured with SQLite;
- a named `SqliteGrainStorage` implementation for Orleans;
- lifecycle participation through the provider's `ILifecycleParticipant<ISiloLifecycle>` implementation.

Persistent state must reference the same provider name used during registration.

Register the connectivity health check separately, after storage registration:

```csharp
siloBuilder.AddSqliteGrainStorageHealthCheck(
    healthCheckName,
    timeout);
```

The health-check name and timeout are optional. Their defaults are:

```text
sqlite-grain-storage
00:00:02
```

A typical fluent registration is:

```csharp
siloBuilder
    .AddSqliteGrainStorage(
        storageProviderName,
        connectionString)
    .AddSqliteGrainStorageHealthCheck(
        healthCheckName);
```

#### Registration boundary

`AddSqliteGrainStorage` registers an unkeyed context factory and a named Orleans storage provider. Call it once for `GrainStateDbContext` in a silo service collection.

Registering the method multiple times with different connection strings would not create independently keyed context factories. Supporting multiple SQLite databases in one silo would require a provider-specific or keyed context-factory design.

`AddSqliteGrainStorageHealthCheck` depends on the context factory registered by `AddSqliteGrainStorage`, so storage must be registered first.

A SQLite connection string can disclose file-system or credential information. Do not write it to logs, telemetry, exception messages, or health-check descriptions.

### Connectivity health check

`SqliteGrainStorageHealthCheck` creates an independent `GrainStateDbContext` and calls `Database.CanConnectAsync` with the health-check cancellation token.

The check intentionally verifies connectivity only. It does not claim that:

- migrations are current;
- the expected tables exist;
- every persistence operation will succeed;
- the database is suitable for production workloads.

Schema creation and updates remain the responsibility of the migration lifecycle in `SqliteGrainStorage`.

The registration timeout is operational policy and is applied by the health-check framework. Caller or framework cancellation is propagated. Other failures produce an unhealthy result with a non-sensitive description.

### Persistence identity

Each stored state record is identified by a composite primary key:

```text
ServiceId
ProviderName
StateName
GrainType
GrainId
```

This identity separates:

- Orleans services sharing a database;
- named storage providers;
- multiple named state objects on one grain;
- grain types;
- individual grain keys.

`Payload` contains the serialized state produced by `IGrainStorageSerializer`. The persistence layer treats it as opaque binary data. It may contain sensitive application state and must not be logged.

### Read behavior

`ReadStateAsync` looks up a record by the complete persistence identity.

When no record exists, the provider sets:

```csharp
grainState.ETag = null!;
grainState.RecordExists = false;
```

When a record exists, the provider:

- deserializes the payload into `grainState.State`;
- formats the stored integer version as an invariant ETag string;
- sets `RecordExists` to `true`.

### Write behavior

A new record:

- requires an empty Orleans ETag;
- starts with provider version `1`;
- records the current UTC modification time;
- returns version `1` as the Orleans ETag;
- sets `RecordExists` to `true`.

An existing record:

1. derives the stored ETag from the persisted version;
2. compares it with the ETag supplied by Orleans;
3. replaces the serialized payload;
4. increments the provider-managed version;
5. updates the UTC modification time;
6. saves the entity;
7. returns the new version as the Orleans ETag.

A competing insert for the same composite primary key is translated to `InconsistentStateException`. Other SQLite constraint failures remain database errors instead of being misreported as stale grain state.

### Clear behavior

`ClearStateAsync` reads the current record and verifies the supplied ETag before deleting it.

After a successful delete, the provider sets:

```csharp
grainState.ETag = null!;
grainState.RecordExists = false;
```

Clearing a state that no longer exists also returns the same non-existent state markers.

### ETag and concurrency

Orleans exposes `IGrainState<T>.ETag` so storage providers can implement optimistic concurrency. This provider stores the ETag as an integer `Version` column and converts it using invariant formatting.

Concurrency is enforced at two levels:

- The provider compares the ETag supplied by Orleans with the version read from SQLite before a write or clear.
- Entity Framework Core treats `Version` as a concurrency token, protecting the interval between loading a tracked entity and saving or deleting it.

If the database row changes during that interval, Entity Framework Core raises `DbUpdateConcurrencyException`. The provider translates it to Orleans `InconsistentStateException` with the stored and supplied ETag values.

A process-local `SemaphoreSlim` serializes schema initialization, writes, and clears within the current process. It does not coordinate other silo processes or external database writers, so it does not replace ETag and database-level concurrency checks.

### Database schema

`GrainStateEntityConfiguration` maps the entity to the `GrainStates` table.

The mapping defines:

- the five-column composite primary key;
- required string identity columns;
- maximum lengths for names and grain identifiers;
- a required binary payload;
- a provider-managed integer version configured as an EF Core concurrency token;
- a required UTC modification timestamp.

The `Version` value is not database-generated. `SqliteGrainStorage` initializes and increments it as part of the provider's ETag contract.

### Migrations and startup

The provider participates in the silo lifecycle at `ApplicationServices` stage. During initialization it:

1. creates a `GrainStateDbContext`;
2. applies pending Entity Framework Core migrations;
3. enables SQLite write-ahead logging;
4. releases the process-local write lock;
5. writes a structured provider-initialized log event.

The initial migration and model snapshot are committed under `Migrations/`. Schema changes should be represented by new migrations rather than edits to an already applied migration.

### SQLite write-ahead logging

Initialization executes:

```sql
PRAGMA journal_mode=WAL;
```

WAL mode allows readers and a writer to make progress concurrently and is suited to a local database file. All processes using the WAL database must run on the same host and have access to the database, WAL, and shared-memory files. Do not place this database on a network filesystem as a shared multi-host store.

### Observability

The provider emits one source-generated informational event after successful initialization:

```text
SQLite grain storage provider {StorageName} initialized for service {ServiceId}.
```

`StorageName` and `ServiceId` are structured properties. The connection string, serialized payload, and ETag values are not logged.

The logging method uses `LoggerMessage` source generation to preserve a stable event ID and avoid runtime message-template parsing.

### Error handling

The provider reports stale or competing state through Orleans `InconsistentStateException`.

This includes:

- inserting when the supplied grain state already has an ETag;
- inserting after another writer created the same composite-key record;
- writing with a stale ETag;
- clearing with a stale ETag;
- an EF Core update or delete concurrency conflict.

Unexpected serialization, migration, connection, command-timeout, locking, or non-primary-key constraint failures are not converted to stale-state errors. They propagate so the caller can distinguish infrastructure failures from optimistic concurrency conflicts.

### Prerequisites

Use the .NET SDK required by the repository. The current project targets `net10.0`.

Restore dependencies from the repository root:

```console
dotnet restore
```

The consuming silo must provide:

- Orleans silo hosting;
- an `IGrainStorageSerializer`;
- `ClusterOptions` with a stable `ServiceId`;
- a writable SQLite connection string location;
- ASP.NET Core health-check services when the optional health check is registered.

### Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Persistence changes should cover at least:

- write, read, update, and clear operations;
- missing records;
- named state identity;
- ETag advancement after successful writes;
- stale write and stale clear rejection;
- duplicate insert conflicts;
- parallel operations;
- non-ASCII grain identifiers;
- serializer round trips;
- migration startup;
- connectivity health-check success, failure, and cancellation;
- SQLite primary-key and non-primary-key constraint behavior.

The Orleans persistence test kit is a useful baseline for validating custom `IGrainStorage` providers, particularly ETag consistency and concurrent operations.

### Adding or changing persistence behavior

When modifying this project:

- Preserve the complete composite storage identity.
- Keep storage-provider names stable once referenced by persistent state.
- Treat serialized payloads and connection strings as sensitive.
- Keep ETag formatting culture-independent.
- Advance the provider-managed version exactly once per successful replacement.
- Return the new ETag and record-existence state after successful operations.
- Translate only genuine stale-state and competing-insert conflicts to `InconsistentStateException`.
- Keep EF Core concurrency-token configuration aligned with the provider's update behavior.
- Keep the health check connectivity-only unless its contract is explicitly changed.
- Keep health-check timeout policy at registration.
- Add a new migration when the entity mapping changes.
- Do not edit an applied migration to represent a new schema revision.
- Keep SQLite deployment assumptions explicit.
- Update this README when registration, schema, ETag behavior, initialization, health checks, or deployment constraints change.

### Naming conventions

- Public extension methods use PascalCase.
- Internal infrastructure types remain under `Internal.Infrastructure`.
- Health checks remain under `Internal.Observability.Health`.
- Structured logging helpers remain under `Internal.Observability.Logging`.
- Orleans storage-provider names are stable configuration identifiers.
- Entity identity property names follow Orleans storage concepts.
- `Version` means the provider-managed value exposed as the Orleans ETag.
- UTC timestamps use the `Utc` suffix.
- Migration names describe the schema change they introduce.

### Scope

Ordering.Persistence.Sqlite is a local SQLite grain-state provider for the ordering silo in this architecture workbench. It is not a distributed database, multi-host shared storage service, backup system, disaster-recovery strategy, encryption solution, general-purpose ORM repository, or production scaling recommendation.

Production deployments should evaluate an Orleans storage provider and database technology that match their availability, durability, backup, security, throughput, and multi-instance requirements.
