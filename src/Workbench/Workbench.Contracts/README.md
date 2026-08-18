# Workbench.Contracts

Workbench.Contracts contains the shared request, response, and status contracts used by the **Microservices vs Virtual Actors** architecture workbench. The project provides stable data-transfer types that allow the repository's implementations and deployable services to exchange the same scenario data without depending on service-local persistence entities or infrastructure types.

The project contains contracts only. It does not host endpoints, coordinate order workflows, access databases, call downstream services, or define deployment behavior.

## Repository context

The repository implements the same ordering scenario in two architectural styles:

- **Microservices**, with explicit HTTP boundaries between Orders.Api, Inventory.Api, and Payments.Api
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Workbench.Contracts supplies the shared vocabulary used across those boundaries. Keeping these types independent of ASP.NET Core, Entity Framework Core, HTTP clients, and Orleans grain implementations allows both architecture paths to represent equivalent requests and outcomes.

See the repository-level README and the project-specific READMEs for workflow behavior, endpoint mappings, persistence, observability, deployment, and architecture interpretation.

## Responsibilities

The project performs five main tasks:

- Defines shared request contracts
- Defines shared response contracts
- Defines shared externally visible status values
- Keeps cross-project API shapes independent of service implementations
- Provides a coordinated compatibility boundary for both architecture paths

The inventory filenames above reflect contracts referenced by the reviewed Inventory.Api endpoint implementation. If the current project uses different filenames or namespaces, keep the source files authoritative and update this tree accordingly.

Generated `bin` and `obj` directories and user-specific project artifacts are intentionally omitted.

## Contract organization

Use capability-based namespaces:

```csharp
namespace Workbench.Contracts.Inventory;
namespace Workbench.Contracts.Orders;
namespace Workbench.Contracts.Payments;
```

Use `Requests` and `Responses` sub-namespaces only when the additional grouping improves navigation without creating unnecessary namespace churn. Folder organization and namespaces should follow the repository's established convention consistently.

A type belongs in Workbench.Contracts when it is intentionally shared by more than one project or architecture implementation. A type used only inside one deployable service should remain in that service.

## Orders contracts

The reviewed Orders contracts are:

```text
Orders/OrderResponse.cs
Orders/OrderStatus.cs
```

`OrderResponse` is an immutable externally visible order result containing:

- `OrderId`, the unique order identifier
- `Status`, the externally visible workflow state
- `Reason`, optional details explaining rejection

`OrderStatus` defines the shared order states:

- `Created`, when the workflow has been initialized
- `InventoryReserved`, when the requested inventory has been reserved
- `PaymentAuthorized`, when payment authorization has succeeded
- `Completed`, the successful terminal state
- `Rejected`, an unsuccessful terminal outcome

The response and enum are used across architecture boundaries. Changes to member names, types, nullability, enum names, or enum ordering require coordinated review of every producer, consumer, persisted representation, and serialized payload.

## Payments contracts

The reviewed Payments contracts are:

```text
Payments/AuthorizePaymentRequest.cs
Payments/AuthorizePaymentResponse.cs
```

`AuthorizePaymentRequest` contains:

- `PaymentId`, the unique payment-attempt identifier
- `OrderId`, the associated order identifier
- `CustomerId`, the customer whose payment is being authorized
- `IdempotencyKey`, the identity of the logical authorization request
- `SimulateFailure`, the deterministic failure control used by the workbench

`AuthorizePaymentResponse` contains:

- `Authorized`, whether payment was authorized
- `Reason`, optional details explaining rejection

`PaymentId` and `IdempotencyKey` have different responsibilities. The payment identifier identifies an attempt, while the idempotency key identifies the logical operation for replay. Do not merge or reinterpret them without reviewing Orders.Api, Payments.Api, persistence constraints, and tests together.

`SimulateFailure` is a workbench scenario control. It is not a production payment-provider option or a general fault-injection contract.

## Inventory contracts

The reviewed Inventory.Api endpoint implementation references these shared contracts:

```text
ResetInventoryRequest
ReserveInventoryRequest
ReleaseInventoryRequest
InventoryResponse
ReserveInventoryResponse
```

Their scenario responsibilities are:

- reset one product's available quantity
- reserve a quantity for an order using a reservation identifier
- release a reservation
- return the current available quantity
- return a reservation outcome and optional rejection reason

Reservation identifiers are part of the inventory idempotency boundary. Changes to their type or meaning can cause repeated requests to decrement inventory more than once or prevent compensation from releasing the intended allocation.

Document the exact members, nullability, and namespaces from the current inventory contract source files when they are reviewed. Do not infer additional fields from endpoint behavior alone.

## Shared contracts versus persistence entities

Shared contracts describe communication boundaries. Persistence entities describe how one service stores state. They should remain separate.

Examples of service-local persistence types that do not belong in Workbench.Contracts include:

- `OrderRecord` in Orders.Api
- `InventoryItem` and `InventoryReservation` in Inventory.Api
- `PaymentAttempt` in Payments.Api

Keeping persistence entities out of the shared project prevents:

- database schema changes from becoming API changes
- EF Core configuration from leaking into consumers
- persistence-only fields from being exposed accidentally
- service implementations from sharing mutable state models
- architecture comparisons from becoming coupled to one storage design

Map between persistence entities and shared responses explicitly at the service boundary.

## Immutability

Prefer sealed positional records for request and response contracts when their members form a compact immutable value:

```csharp
public sealed record ExampleResponse(
    Guid Id,
    bool Succeeded,
    string? Reason);
```

This provides concise value semantics and discourages mutation after a contract has been created.

Use a class instead of a positional record only when the contract needs behavior or construction semantics that a record does not express clearly. Do not use mutable EF Core entities as substitutes for transport records.

## Nullability

Use nullable reference types to express optional data directly in the contract.

A nullable `Reason` means that no explanatory reason applies for outcomes such as successful completion or authorization. Documentation should state when `null` is expected and when a reason should be present.

Avoid using empty strings as an undocumented alternative to `null`. Producers and consumers should agree on one representation for absence.

## Idempotency contracts

Idempotency values are workflow identities, not general correlation identifiers.

- Order idempotency prevents repeated order-placement requests from creating another logical workflow
- Payment idempotency prevents repeated authorization requests from producing another logical authorization result
- Inventory reservation identity prevents repeated reservation requests from decrementing stock again and enables idempotent release

Do not replace idempotency keys with correlation IDs. Correlation IDs support observability, while idempotency values control business replay behavior.

Changes to idempotency property names, casing rules, maximum lengths, uniqueness, or lookup behavior must be coordinated with service persistence and endpoint behavior.

## Status and reason values

`OrderStatus` is strongly typed because it represents a shared closed set of externally visible order states.

Failure reasons are currently represented as nullable strings in the reviewed contracts. Known scenario values include outcomes such as insufficient inventory and payment failure. Treat these values as externally visible contract data when callers branch on them.

If reason values become numerous or require machine-readable stability, introduce a coordinated reason-code contract rather than independently defining string constants in each service. Such a change should include compatibility, JSON, persistence, and consumer migration tests.

## Serialization compatibility

These contracts cross process or implementation boundaries, so their serialized shapes are compatibility-sensitive even when the CLR code still compiles.

Review the following changes as contract changes:

- renaming a type or namespace
- renaming a property or positional parameter
- changing member order in a positional record
- changing a member type
- changing nullability
- adding a required member
- removing a member
- changing enum member names or order
- changing JSON enum representation
- changing casing or serializer options
- changing default values relied upon by consumers

Prefer additive, optional evolution where possible. Coordinate breaking changes across all producers and consumers in one repository change.

## JSON behavior

Workbench.Contracts should not silently choose service-specific JSON settings. Serializer configuration belongs to the hosting applications unless the repository deliberately defines shared serialization metadata.

When changing a contract, verify its JSON shape under the actual options used by:

- Orders.Api
- Inventory.Api
- Payments.Api
- the virtual-actor API boundary
- tests and scenario tooling

Pay particular attention to enum serialization. Numeric and string enum representations have different compatibility characteristics and must not be changed accidentally.

## Validation

Shared contracts describe data shape. Validation belongs at the boundary that accepts the data unless validation semantics are deliberately shared.

Services should validate relevant conditions such as:

- non-empty customer and product identifiers
- non-empty idempotency keys
- non-empty reservation identifiers where applicable
- positive quantities
- valid combinations of status and reason
- scenario-only controls being allowed in the current environment

Avoid adding ASP.NET Core, EF Core, or service-specific dependencies to Workbench.Contracts solely for validation convenience.

## XML documentation

All declared contract types and members should have XML documentation.

For positional records, include:

- `<summary>` for the record
- `<param>` for every positional member
- `<remarks>` for compatibility, idempotency, or scenario semantics when relevant

For enums, include:

- `<summary>` and compatibility remarks for the type
- `<summary>` for every member

Documentation must describe observable contract meaning rather than implementation details from one producer.

## Dependencies

Keep Workbench.Contracts dependency-light. The project should not require:

- ASP.NET Core hosting packages
- Entity Framework Core
- database providers
- HTTP client implementations
- service discovery
- health checks
- logging implementations
- Orleans runtime packages unless the contracts are intentionally Orleans-specific

A small dependency surface makes the project safe to reference from services, clients, tests, and both architecture implementations.

## Versioning guidance

The repository currently coordinates contract versions through source and deployment alignment. Even without a separately published package version, shared contracts should be treated as versioned APIs.

For every change:

1. identify every producer and consumer
2. determine whether the JSON shape changes
3. determine whether persisted values are affected
4. update mappings and tests together
5. document the compatibility impact
6. deploy in an order that supports mixed versions when deployments are independent

If the contracts are later published as a package, adopt semantic versioning and package-release notes for contract changes.

## Local development

From the Workbench.Contracts project directory:

```console
dotnet build
```

From the repository root:

```console
dotnet build --project <path-to-Workbench.Contracts.csproj>
```

Workbench.Contracts has no database, HTTP server, launch profile, or runtime process of its own. Its behavior is exercised through compilation, serialization tests, and consuming projects.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Contract changes should cover at least:

- compilation of every consuming project
- JSON serialization snapshots or equivalent shape assertions
- null and non-null reason values
- enum serialization behavior
- idempotency value round trips
- request and response round trips
- compatibility with both architecture implementations
- endpoint binding in each consuming API
- persistence-to-response mappings
- unknown or newly added enum-value behavior where applicable

## Adding or changing contracts

When modifying this project:

- Add a contract only when it is intentionally shared
- Keep service-local persistence and transport implementation types out of the project
- Prefer immutable sealed records for compact requests and responses
- Preserve namespace, member order, types, and nullability unless making a coordinated breaking change
- Keep idempotency semantics explicit and separate from correlation
- Document every type and member with XML comments
- Avoid dependencies on hosting, persistence, and observability frameworks
- Verify JSON behavior using the serializer options of consuming applications
- Update all producers, consumers, tests, and this README together

## Naming conventions

- Request contracts use the `Request` suffix
- Response contracts use the `Response` suffix
- Shared status enums use the `Status` suffix
- Contract namespaces are grouped by business capability
- Positional record parameters use PascalCase because they declare public properties
- Boolean names describe the positive condition, such as `Authorized` or `SimulateFailure`
- Optional explanatory values use nullable reference types

## Scope

Workbench.Contracts defines the shared data shapes required by the architecture workbench. It is not a domain-model assembly, persistence abstraction, service SDK, workflow engine, validation framework, authentication model, or compatibility gateway.

Production use would require independent decisions for package versioning, backward compatibility, deprecation, serializer governance, schema evolution, client generation, contract testing, deployment sequencing, and support policy.
