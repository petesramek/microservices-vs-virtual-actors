## Observability.Topology

Observability.Topology provides the shared topology contracts, runtime snapshots, health evaluation, and validation used by the **Microservices vs Virtual Actors** architecture workbench. It describes services, storage resources, directed dependencies, visual groups, and their evaluated health without depending on Aspire hosting or Workbench presentation code.

This project does not discover resources, execute health checks, or render the topology. Its responsibility is to define the neutral model and policies used by projects that construct, observe, validate, and display the application topology.

### Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments.
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity.

The Workbench presents both implementations through a common observability model so their runtime structure and health can be examined side by side. Observability.Topology keeps that model independent of the hosting technology and UI.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

### Responsibilities

The project performs four main tasks:

- Defines the static topology of nodes, directed dependency edges, health sources, and visual groups.
- Represents point-in-time node, edge, group, and complete-topology snapshots.
- Evaluates aggregate dependency and group health through shared health abstractions.
- Validates identifiers, references, enum values, and graph invariants before the topology is consumed.

### Project structure

```text
Definitions/
  HealthSourceDefinition.cs
  TopologyDefinition.cs
  TopologyDependencyRequirement.cs
  TopologyEdgeDefinition.cs
  TopologyGroupDefinition.cs
  TopologyNodeDefinition.cs
  TopologyNodeKind.cs

Evaluators/
  Abstraction/
    IDependencyHealthEvaluator.cs
    IGroupHealthEvaluator.cs
  DependencyHealthEvaluator.cs
  GroupHealthEvaluator.cs

Snapshots/
  ResourceAvailability.cs
  TopologyEdgeSnapshot.cs
  TopologyGroupSnapshot.cs
  TopologyNodeSnapshot.cs
  TopologySnapshot.cs

Validation/
  TopologyValidationResult.cs
  TopologyValidator.cs
```

### Static topology definitions

`TopologyDefinition` is the root static model. It contains ordered collections of nodes, directed edges, and groups.

#### Nodes

A `TopologyNodeDefinition` describes a service or storage resource:

- `Id` is the stable serialized identifier.
- `DisplayName` is the user-facing label.
- `Kind` distinguishes service and storage nodes.
- `HealthSource` optionally identifies the service and health-report entry that provide the node's direct health.

Identifiers are case-sensitive contracts. Display names may change without changing topology identity.

#### Dependency edges

A `TopologyEdgeDefinition` describes a directed dependency:

```text
SourceNodeId -> TargetNodeId
```

`Required` dependencies contribute their observed health directly. An unhealthy `Optional` dependency contributes degraded health instead of unhealthy health.

`HealthEntryKey` optionally identifies the source node's health-report entry that represents the dependency.

#### Groups

A `TopologyGroupDefinition` organizes nodes for presentation and aggregate health. Group membership:

- preserves definition order;
- contributes to group health evaluation;
- does not imply dependency direction;
- does not imply startup ordering.

### Runtime snapshots

Snapshot contracts represent evaluated topology state at one point in time.

#### TopologySnapshot

`TopologySnapshot` contains:

- `GeneratedAtUtc`;
- ordered node snapshots;
- ordered edge snapshots;
- ordered group snapshots.

The root snapshot defensively copies its supplied collections so later mutation of producer-owned lists does not change an already published snapshot.

#### Node snapshots

`TopologyNodeSnapshot` records:

- the stable node ID;
- optional runtime availability;
- direct health;
- optional observation timestamp;
- optional check duration;
- an optional non-sensitive description.

Availability describes reachability and is independent of health. A reachable resource can still report degraded or unhealthy status.

#### Edge snapshots

`TopologyEdgeSnapshot` records point-in-time health for a directed dependency. Source and target identifiers retain the same direction as the corresponding edge definition.

#### Group snapshots

`TopologyGroupSnapshot` contains the group ID and evaluated aggregate health. Membership remains in the static group definition and is not duplicated in the runtime snapshot.

### Health evaluation

Topology-specific evaluators depend on the shared `IHealthStatusEvaluator` abstraction from Observability.Health.

#### DependencyHealthEvaluator

`DependencyHealthEvaluator`:

- matches edge definitions to snapshots by source and target ID;
- treats a missing edge snapshot as unknown;
- converts an unhealthy optional dependency to degraded;
- delegates final aggregation to `IHealthStatusEvaluator`.

#### GroupHealthEvaluator

`GroupHealthEvaluator`:

- resolves every configured member to a node snapshot;
- treats a missing member snapshot as unknown;
- preserves group member order while collecting observations;
- delegates final aggregation to `IHealthStatusEvaluator`.

Consumers should depend on `IDependencyHealthEvaluator` and `IGroupHealthEvaluator` rather than concrete implementations where substitution or testing is required.

### Validation

Validate a topology before publishing or evaluating it:

```csharp
TopologyValidator validator = new();
TopologyValidationResult result = validator.Validate(topology);

if (!result.IsValid)
{
    foreach (string error in result.Errors)
    {
        Console.WriteLine(error);
    }
}
```

`TopologyValidator` checks:

- node and group identifiers;
- display names;
- supported node-kind and dependency-requirement values;
- duplicate node and group IDs;
- edge source and target references;
- self-dependencies;
- duplicate directed edges;
- group member references and duplicate membership;
- health-provider service references;
- health entry keys.

Validation reports malformed topology content through `TopologyValidationResult`. Passing a null topology throws `ArgumentNullException`.

### Serialization contract

Definitions and snapshots are shared across API boundaries. Treat these values as compatibility-sensitive:

- node and group identifiers;
- edge direction;
- health entry keys;
- enum member names;
- public property names;
- collection ordering.

The public contracts use ordered read-only lists for deterministic serialization and presentation. Runtime lookup indexes may use dictionaries internally, but should not replace ordered wire-format collections.

Descriptions may be exposed through APIs and user interfaces. They must not contain secrets, credentials, personal data, or sensitive implementation details.

### Prerequisites

Use the .NET SDK required by the repository. Restore dependencies from the repository root:

```console
dotnet restore
```

Observability.Topology depends on Observability.Health for the shared health-status model and aggregation abstraction.

### Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

When changing topology contracts or evaluation behavior, validate serialization, definition ordering, graph validation, missing-snapshot behavior, optional dependencies, and group aggregation.

### Adding or changing topology behavior

When modifying this project:

- Keep definitions and snapshots independent of Aspire and UI concerns.
- Preserve stable serialized identifiers and property names.
- Add edges in source-to-target direction.
- Keep graph-wide invariants in `TopologyValidator` rather than duplicating them in transport records.
- Preserve ordered collections when order affects serialization or presentation.
- Keep availability separate from health.
- Keep topology-specific observation selection in topology evaluators.
- Keep generic health aggregation in Observability.Health.
- Depend on evaluator interfaces where consumers require substitution.
- Treat evaluation-policy changes as behavioral compatibility changes.
- Keep descriptions non-sensitive.
- Document every declared type and member.
- Update this README when the public contract, folder structure, or evaluation semantics change.

### Naming conventions

- Interfaces use the `I` prefix.
- Public types and members use PascalCase.
- Private fields use `_camelCase`.
- Node and group IDs are stable and case-sensitive.
- Display names are user-facing and independent of stable IDs.
- Dependency direction is expressed as source to target.
- Definitions describe static structure; snapshots describe evaluated runtime state.
- Evaluators apply health policy; validators enforce structural invariants.

### Scope

Observability.Topology provides topology definitions, point-in-time snapshots, health evaluation, and structural validation. It does not discover runtime resources, invoke health endpoints, poll dependencies, persist snapshot history, render topology views, configure Aspire resources, publish telemetry, or define production monitoring and alerting policy.

Those responsibilities belong to the hosting, service-defaults, Workbench, and deployment projects that consume this library.
