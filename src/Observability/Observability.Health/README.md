## Observability.Health

Observability.Health is the shared health-status contract and evaluation library for the **Microservices vs Virtual Actors** architecture workbench. It defines the health states exchanged by participating components, provides an instance-based abstraction for aggregating multiple health states, and supplies dependency-injection registration for the default evaluator.

This project does not collect health checks or expose HTTP health endpoints. Its responsibility is to provide the neutral health model and evaluation behavior used by projects that collect, transform, or display health information.

### Repository context

The repository compares the same order workflow implemented with two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments.
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity.

The Workbench presents health information from both implementations through a common model. Observability.Health keeps that model independent of ASP.NET Core health-check types, Aspire resources, transport details, and presentation concerns.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

### Responsibilities

The project performs four main tasks:

- Defines the shared health-status values used by observability components.
- Defines `IHealthStatusEvaluator` as the instance-based aggregation contract.
- Provides the default `HealthStatusEvaluator` implementation.
- Registers the default evaluator with the dependency-injection container.

### Project structure

```text
Abstractions/
  IHealthStatusEvaluator.cs

Extensions/
  HealthServiceCollectionExtensions.cs
```

The project also contains the default evaluator implementation and the health contracts consumed by other observability projects.

### Public API

#### IHealthStatusEvaluator

`IHealthStatusEvaluator` defines the health aggregation contract:

```csharp
HealthStatus Evaluate(
    IReadOnlyCollection<HealthStatus> statuses);
```

Consumers should depend on this abstraction and call the injected instance. They should not invoke static evaluation helpers or reproduce aggregation rules locally.

The concrete implementation owns status precedence, empty-collection behavior, and handling of unsupported values. Changes to those rules are behavioral compatibility changes.

#### HealthStatusEvaluator

`HealthStatusEvaluator` is the default implementation of `IHealthStatusEvaluator`.

The implementation is registered as a singleton and is therefore expected to remain stateless. Do not add scoped state, request-specific state, mutable shared state, or scoped dependencies without revisiting the service lifetime.

#### AddHealthStatusEvaluator

`AddHealthStatusEvaluator` registers the default service mapping:

```csharp
IHealthStatusEvaluator -> HealthStatusEvaluator
```

The extension returns the supplied `IServiceCollection` so registration can participate in fluent application composition.

### Dependency-injection registration

Register the evaluator during application startup:

```csharp
using Observability.Health.Extensions;

services.AddHealthStatusEvaluator();
```

The extension registers `HealthStatusEvaluator` for `IHealthStatusEvaluator` with singleton lifetime.

Call the extension once during application composition. The registration uses `AddSingleton`, so repeated calls add repeated service descriptors.

### Usage

Inject `IHealthStatusEvaluator` into the consuming application service:

```csharp
using Observability.Health;

public sealed class HealthReportService
{
    private readonly IHealthStatusEvaluator _healthStatusEvaluator;

    public HealthReportService(
        IHealthStatusEvaluator healthStatusEvaluator)
    {
        ArgumentNullException.ThrowIfNull(healthStatusEvaluator);

        _healthStatusEvaluator = healthStatusEvaluator;
    }

    public HealthStatus Evaluate(
        IReadOnlyCollection<HealthStatus> statuses)
    {
        return _healthStatusEvaluator.Evaluate(statuses);
    }
}
```

Depending on the abstraction keeps consumers independent of the default implementation and allows tests to provide a controlled substitute.

### Evaluation behavior

Health aggregation is a domain contract. Consumers pass the statuses to `IHealthStatusEvaluator.Evaluate` and use the returned aggregate status.

Consumers should not:

- duplicate status-precedence rules;
- infer aggregate health independently;
- depend directly on implementation details;
- convert framework-specific health states outside the designated mapping boundary;
- treat an undocumented enum value as equivalent to a known state.

Document the exact precedence and empty-collection behavior on `HealthStatusEvaluator` and protect those rules with focused tests.

### Testing

Tests for `HealthStatusEvaluator` should cover:

- an empty status collection;
- a collection containing one status;
- repeated statuses;
- mixed statuses;
- precedence between healthy, degraded, unhealthy, and unknown states;
- unsupported enum values;
- a null collection when null is rejected by the implementation.

Consumer tests should substitute `IHealthStatusEvaluator` and verify how the consumer handles the returned status. They should not repeat the evaluator’s precedence test matrix.

### Prerequisites

Use the .NET SDK required by the repository. Restore dependencies from the repository root before building:

```console
dotnet restore
```

### Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

When aggregation behavior changes, update the evaluator tests and this README together. Also validate downstream components that serialize, display, or react to the aggregate status.

### Adding or changing health behavior

When modifying the project:

- Keep `IHealthStatusEvaluator` focused on health-status aggregation.
- Preserve the instance-based API and dependency-injection model.
- Keep the default evaluator stateless while it uses singleton lifetime.
- Treat precedence changes as behavioral compatibility changes.
- Define and test empty-collection behavior explicitly.
- Define and test unsupported enum-value behavior explicitly.
- Add public abstractions to the `Abstractions` folder.
- Add dependency-injection registrations to focused extensions in the `Extensions` folder.
- Document every declared type and member.
- Update this README when the public contract, service lifetime, or project structure changes.

### Naming conventions

- Interfaces use the `I` prefix.
- Public types and members use PascalCase.
- Private fields use `_camelCase`.
- Dependency-injection extensions use the `Add...` naming pattern.
- Abstractions describe capabilities rather than implementation details.
- Health-status names are stable contract values and should not be renamed casually.

### Scope

Observability.Health provides a neutral health model, aggregation abstraction, default evaluator, and dependency-injection registration. It does not register ASP.NET Core health checks, map readiness or liveness endpoints, query remote services, persist health history, publish telemetry, authorize health access, or define production monitoring policy.

Those responsibilities belong to the hosting, service-defaults, topology, or Workbench projects that consume this library.
