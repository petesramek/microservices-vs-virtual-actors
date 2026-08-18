# Hosting.AppHost

`Hosting.AppHost` is the .NET Aspire orchestration project for the **Microservices vs Virtual Actors** architecture workbench. It starts the projects required by the interactive comparison, supplies Aspire-managed service endpoints, defines startup dependencies and health checks, and publishes the application topology used by the Workbench experience.

This project does not implement either order-processing workflow. Its responsibility is to compose the local distributed application from the projects that do.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

The Workbench runs equivalent scenarios against both implementations so their behavior and trade-offs can be examined side by side. It is an architecture case study, not a benchmark. Local timings describe this sample topology only and should not be treated as general performance conclusions.

See the repository-level README and `docs` directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The AppHost performs five main tasks:

1. Registers the Workbench, microservices, and virtual actor projects with Aspire.
2. Replaces configured service URLs with Aspire-managed endpoint references.
3. Applies health checks and shared observability configuration.
4. Declares startup dependencies between project resources.
5. Publishes a neutral observability topology for the Workbench UI.

## Application model

### Workbench

| Resource | Role |
| --- | --- |
| Workbench UI | Hosts the interactive scenario dashboard and receives the serialized observability topology. |
| Workbench Gateway | Runs scenarios against the microservices and virtual actor entry points through a common interface. |

### Microservices path

| Resource | Role |
| --- | --- |
| Orders API | Owns order workflow orchestration. |
| Inventory API | Owns inventory state and reservation invariants. |
| Payments API | Owns payment authorization behavior. |

### Virtual actor path

| Resource | Role |
| --- | --- |
| Ordering API | Exposes the actor-backed ordering workflow. |
| Ordering Silo | Hosts the Orleans grains and Orleans Dashboard. |

## Runtime topology

```text
Workbench UI
    |
    v
Workbench Gateway
    |--------------------------------|
    v                                v
Orders API                      Ordering API
    |          |                      |
    v          v                      v
Inventory API  Payments API      Ordering Silo
```

The observability model also includes storage nodes for the orders, inventory, payments, and ordering data stores. These nodes describe health and dependency relationships, they are not separately registered Aspire project resources.

## Startup dependencies

The AppHost declares the following startup relationships:

- Orders API waits for Inventory API and Payments API
- Ordering API waits for Ordering Silo
- Workbench UI waits for Workbench Gateway

A startup relationship controls orchestration order. It is separate from visual topology grouping and from the complete set of runtime dependency edges shown by the Workbench.

## Service discovery and endpoint configuration

The AppHost uses Aspire endpoint references instead of fixed local ports. It supplies the following configuration overrides to dependent projects:

| Configuration key | Consumer | Aspire resource endpoint |
| --- | --- | --- |
| `Services__InventoryBaseUrl` | Orders API | Inventory API |
| `Services__PaymentsBaseUrl` | Orders API | Payments API |
| `ServiceEndpoints__MicroservicesBaseUrl` | Workbench Gateway | Orders API |
| `ServiceEndpoints__VirtualActorsBaseUrl` | Workbench Gateway | Ordering API |
| `Gateway__BaseUrl` | Workbench UI | Workbench Gateway |

The double underscore follows the .NET environment-variable convention for hierarchical configuration keys.

## Health checks

Every registered project resource exposes an HTTP readiness check at:

```text
/health
```

The Aspire Dashboard uses these checks to report resource health. The topology additionally associates service dependencies and storage nodes with named health-report entries so the Workbench can distinguish direct resource health from dependency health.

## Observability configuration

The AppHost forwards the configured observability section to participating project resources as environment variables. Nested configuration keys are flattened with `__` separators.

The AppHost also publishes a neutral topology containing:

- service nodes
- storage nodes
- directed dependency edges
- visual resource groups
- health-source mappings

Topology registration is order-dependent. Nodes must be registered before an edge or group refers to them. Project-backed node identifiers come from Aspire resource names, non-project storage nodes and visual groups use stable identifiers declared by the AppHost.

### Visual groups

The topology is organized into three Dashboard groups:

- **Workbench**: Workbench UI and Workbench Gateway
- **Microservices**: Orders, Inventory, and Payments APIs plus their storage nodes
- **Virtual Actors**: Ordering API, Ordering Silo, and the ordering storage node

Groups are visual only. Membership does not imply dependency direction or startup ordering.

## Prerequisites

Use the .NET SDK required by the repository and the version of Aspire referenced by this project. An OCI-compatible container runtime is required only when a resource in the application model depends on containers.

Before running the AppHost, restore the repository dependencies:

```bash
dotnet restore
```

## Run locally

From the `Hosting.AppHost` project directory:

```bash
dotnet run
```

Alternatively, run the project from the repository root by passing its actual project-file path:

```bash
dotnet run --project <path-to-Hosting.AppHost.csproj>
```

The AppHost prints the Aspire Dashboard URL after startup. Use the Dashboard to inspect resource state, logs, endpoints, health checks, and links to the Workbench UI and Orleans Dashboard.

## Validate changes

From the repository root:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

The repository's regression tests protect the scenario result semantics, including successful orders, business rejection, compensation, timeout handling, contention, and duplicate-request idempotency. When orchestration changes affect scenario behavior, update the relevant tests and repository documentation together.

## Adding or changing a resource

When modifying the application model:

1. Register the project with a stable lowercase kebab-case resource name.
2. Configure its Dashboard endpoint label and `/health` readiness check.
3. Supply downstream URLs through Aspire endpoint references rather than fixed ports.
4. Add `WaitFor` only when startup ordering is required.
5. Apply the shared observability configuration where appropriate.
6. Register the topology node before adding dependencies or group membership.
7. Add dependency edges in source-to-target direction.
8. Add non-project storage nodes with stable topology identifiers.
9. Place each node in the appropriate visual group.
10. Update this README if the AppHost contract or application shape changes.

## Naming conventions

- Aspire resource names use lowercase kebab-case
- Configuration overrides use .NET's `__` hierarchy separator
- Topology node and group identifiers are stable and case-sensitive
- Dashboard display names are user-facing labels and are independent of stable IDs
- Workbench, Microservices, and Virtual Actors are the domain names used for resource collections and registration helpers, a redundant `Services` suffix is avoided

## Scope

`Hosting.AppHost` is local orchestration and topology composition for the architecture workbench. It does not establish a production deployment platform, security model, autoscaling strategy, multi-region design, or production observability backend. Refer to the repository documentation for those limitations and for guidance on interpreting the sample responsibly.
