# Ordering.Silo

Ordering.Silo hosts the Orleans virtual-actor implementation used by the **Microservices vs Virtual Actors** architecture workbench. It configures local Orleans clustering, activity propagation, the Orleans Dashboard, SQLite-backed grain-state persistence, database connectivity health reporting, and the shared readiness and liveness endpoints.

This project is the runtime host for ordering grains. Persistence infrastructure is delegated to `Ordering.Persistence.Sqlite`, while shared HTTP health endpoints and observability defaults come from `Hosting.ServiceDefaults`.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Ordering.Silo is the execution host for the virtual actor path. The companion Ordering API exposes the actor-backed workflow, while the Silo hosts the grains and their persistence provider.

The repository is an architecture case study, not a benchmark or production deployment blueprint. See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The Silo performs six main tasks:

- Creates the ASP.NET Core web host
- Applies shared service defaults and observability configuration
- Starts an Orleans silo with localhost clustering
- Enables Orleans activity propagation and the Orleans Dashboard
- Registers named SQLite grain-state persistence and its connectivity health check
- Maps the shared readiness and liveness endpoints

## Startup flow

The application starts in this order:

1. Create the `WebApplicationBuilder`.
2. Apply shared service defaults.
3. Resolve the required `Default` SQLite connection string.
4. Configure the Orleans silo.
5. Register SQLite grain storage.
6. Register the SQLite connectivity health check.
7. Build the web application.
8. Map the Orleans Dashboard.
9. Map shared readiness and liveness endpoints.
10. Run until shutdown.

## Orleans configuration

The Silo uses:

```csharp
siloBuilder
    .UseLocalhostClustering()
    .AddActivityPropagation()
    .AddDashboard();
```

`UseLocalhostClustering` is appropriate for the local architecture workbench. It is not a production clustering configuration.

Activity propagation allows distributed traces to retain context across calls into Orleans grains.

The Orleans Dashboard is mapped under:

```text
/dashboard
```

## Persistence configuration

The Silo resolves the connection string named:

```text
Default
```

Startup fails with `InvalidOperationException` when that connection string is not configured. This prevents the host from starting with an unusable grain-state persistence configuration.

The named Orleans storage provider is:

```text
OrderingStorage
```

The provider is registered through the persistence-project extension:

```csharp
siloBuilder.AddSqliteGrainStorage(
    StorageProviderName,
    connectionString);
```

Persistent grain-state declarations must use the same storage-provider name.

Connection strings may contain file paths or credentials and must not be logged or exposed through diagnostics.

## Database health check

The Silo registers the persistence-owned connectivity check after storage registration:

```csharp
siloBuilder.AddSqliteGrainStorageHealthCheck(
    DatabaseHealthCheckName);
```

The registered health-check name is:

```text
ordering-database
```

The check uses the `GrainStateDbContext` factory registered by `AddSqliteGrainStorage`, so registration order is intentional.

The health check verifies connectivity only. It does not validate schema freshness or guarantee that every future persistence operation will succeed. Database migrations remain the responsibility of the SQLite storage provider's silo lifecycle initialization.

The persistence extension supplies a two-second default timeout. The Silo can override it by passing a `TimeSpan` to `AddSqliteGrainStorageHealthCheck` if the showcase configuration changes.

## Health endpoints

Shared service defaults map:

```text
/health
/alive
```

`/health` is the readiness endpoint and evaluates registered dependency checks, including the SQLite connectivity check.

`/alive` is the liveness endpoint and evaluates only checks tagged for liveness by the shared defaults. Database availability should not determine whether the process itself is alive.

## Dashboard

The Orleans Dashboard is co-hosted by the Silo and mapped at `/dashboard`.

It is intended for local inspection of Orleans runtime behavior. It should not be exposed as an unauthenticated production management endpoint.

## Observability

`AddServiceDefaults` applies the repository's shared logging, metrics, tracing, health-check, and exporter configuration.

`AddActivityPropagation` preserves tracing context through Orleans calls so activity initiated by the Ordering API or Workbench scenario can be correlated with grain execution.

The Silo does not log the SQLite connection string or serialized grain-state payloads.

## Configuration contract

The current host relies on these stable values:

```text
Connection string: Default
Storage provider:  OrderingStorage
Health check:      ordering-database
Dashboard route:   /dashboard
```

Changing `OrderingStorage` requires updating persistent-state registrations that reference the provider name.

Changing the connection-string name requires updating application configuration and the Silo constant together.

Changing the health-check name can affect topology health-source mappings or Workbench presentation when they reference the named health entry.

## Run locally

From the Ordering.Silo project directory:

```console
dotnet run
```

Alternatively, run the project from the repository root:

```console
dotnet run --project <path-to-Ordering.Silo.csproj>
```

The application requires a configured `Default` SQLite connection string. The shared AppHost normally supplies the local orchestration environment.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Silo-hosting changes should verify:

- startup with a valid connection string
- startup failure when the connection string is missing
- Orleans localhost clustering
- grain activation and persistence
- activity propagation
- Dashboard mapping
- readiness behavior when SQLite is available or unavailable
- liveness behavior independent of SQLite availability
- graceful cancellation and shutdown

## Adding or changing Silo behavior

When modifying this project:

- Keep host composition in `Program.cs` concise
- Delegate persistence implementation and health-check details to `Ordering.Persistence.Sqlite`
- Register grain storage before its health check
- Keep the storage-provider name synchronized with persistent-state declarations
- Keep dependency checks on readiness rather than liveness
- Use shared service defaults for health endpoints and observability
- Preserve activity propagation for distributed tracing
- Avoid logging connection strings or serialized state
- Treat the Dashboard as a local development surface
- Replace localhost clustering before treating the host as a production deployment
- Update this README when startup, configuration, persistence, monitoring, or endpoint contracts change

## Naming conventions

- Configuration constants use PascalCase names and stable string values
- Orleans storage-provider names are configuration contracts
- Health-check names are stable observability identifiers
- Route prefixes begin with `/`
- Async entry points return `Task` and propagate host shutdown cancellation through framework APIs
- Silo-specific composition remains in the `Ordering.Silo` namespace

## Scope

Ordering.Silo is the local Orleans host for the virtual actor side of the architecture workbench. It does not define a production clustering strategy, distributed database, multi-region deployment, Dashboard security model, autoscaling policy, backup strategy, or disaster-recovery plan.

Production deployments must replace local clustering and evaluate persistence, security, availability, monitoring, and operational requirements independently.
