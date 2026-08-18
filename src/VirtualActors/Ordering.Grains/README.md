# Ordering.Grains

Ordering.Grains contains the Orleans grain contracts, implementations, persisted state models, and serialized result contracts used by the **Microservices vs Virtual Actors** architecture workbench. It models order placement, product inventory, and payment authorization as identity-addressed virtual actors.

This project does not host the Orleans silo, configure clustering, or provide the SQLite storage implementation. Hosting belongs to `Ordering.Silo`, while persistence infrastructure belongs to `Ordering.Persistence.Sqlite`.

## Repository context

The repository implements the same order workflow in two architectural styles:

- **Microservices**, with explicit HTTP service boundaries for order orchestration, inventory, and payments
- **Virtual actors**, with Orleans grains providing identity-based state ownership and serialized execution per actor identity

Ordering.Grains defines the virtual actor side of the comparison. The interfaces describe the remotely callable grain contracts, the grain classes implement those contracts, the state classes represent persisted actor state, and the contract records carry serialized results between callers and grains.

See the repository-level README and docs directory for the scenario guide, architecture discussions, operational interpretation, known limitations, and scope boundaries.

## Responsibilities

The project performs five main tasks:

- Defines Orleans grain interfaces for orders, inventory items, and payment accounts
- Implements the corresponding grain behavior
- Defines Orleans-serializable result and snapshot contracts
- Defines mutable persisted state for each grain type
- Maintains stable Orleans aliases and serialization member identifiers

## Grain interfaces

The grain interfaces are the remotely callable Orleans contracts. Each interface uses an explicit Orleans alias and an identity-specific grain-key type.

### Inventory item grain

`IInventoryItemGrain` uses a string grain key representing one product identity. It exposes operations to:

- reset available inventory for deterministic scenarios
- retrieve the current inventory snapshot
- reserve a quantity for an order
- release a previous reservation

Reservation calls use a stable reservation identifier and order identifier. The interface returns either an `InventorySnapshot` or an `InventoryReservationResult` rather than exposing persisted state directly.

### Order grain

`IOrderGrain` uses a GUID grain key representing one order identity. It exposes operations to:

- place an order using an idempotency key, customer ID, product ID, quantity, and showcase payment-failure flag
- retrieve the current order result when one is available

`GetAsync` returns `null` when no order result is available.

### Payment account grain

`IPaymentAccountGrain` uses a string grain key representing one customer or account identity. It authorizes a payment request using:

- a payment identifier
- an order identifier
- an idempotency key
- a showcase failure-simulation flag

The operation returns a `PaymentAuthorizationResult`.

## Orleans aliases

Interfaces, grain methods, and serialized types use explicit `[Alias]` values. These aliases are part of the Orleans contract and should remain stable after publication.

Interface aliases currently match their CLR namespaces:

```text
Ordering.Grains.Grains.Abstraction.IInventoryItemGrain
Ordering.Grains.Grains.Abstraction.IOrderGrain
Ordering.Grains.Grains.Abstraction.IPaymentAccountGrain
```

Method aliases remain explicit string literals such as:

```text
ResetAsync
GetAsync
ReserveAsync
ReleaseAsync
PlaceAsync
AuthorizeAsync
```

Do not replace compatibility-sensitive aliases with `nameof(...)`. A CLR rename should not silently rename an established Orleans contract.

## Result and snapshot contracts

The `Contracts` directory contains immutable positional records used in grain calls.

### GrainOrderResult

`GrainOrderResult` represents the terminal result of an order workflow:

- `OrderId` identifies the order
- `Status` contains the stable status contract value
- `Reason` optionally explains why the order did not complete successfully

### InventoryReservationResult

`InventoryReservationResult` represents the outcome of an inventory reservation attempt:

- `Reserved` indicates whether inventory was reserved
- `Reason` optionally explains a failed reservation
- `AvailableQuantity` reports the quantity available after the attempt

### InventorySnapshot

`InventorySnapshot` represents a point-in-time view of inventory state rather than the outcome of one specific operation:

- `ProductId` identifies the product
- `AvailableQuantity` reports the quantity available when the snapshot was created

The `Snapshot` suffix is intentional. Adding `Result` would be redundant because the type describes state, not an operation outcome.

### PaymentAuthorizationResult

`PaymentAuthorizationResult` represents the outcome of a payment authorization attempt:

- `Authorized` indicates whether authorization succeeded
- `Reason` optionally explains a failed authorization

## Serialized contracts

Grain-call contracts use:

```csharp
[GenerateSerializer]
[Alias("...")]
```

Serialized members use stable numeric identifiers:

```csharp
[property: Id(0)]
[property: Id(1)]
```

When evolving a serialized contract:

- do not change an established alias without treating it as a compatibility change
- do not renumber existing member IDs
- do not reuse a removed member ID for a different meaning
- assign a new unused ID to each new serialized member
- preserve nullable semantics unless the contract change is intentional

## Persisted grain state

The `State` directory contains mutable Orleans persistence models:

- `InventoryItemState` for inventory quantity and active reservations
- `OrderState` for order workflow state
- `PaymentAccountState` for payment-account state

Persisted state uses mutable classes because grain implementations update state before writing it through Orleans persistence. This differs intentionally from the immutable positional records used as grain-call results.

State classes must remain deserializable. Command validation therefore belongs in grain operations rather than property setters.

## Validation boundary

Grain interfaces and serialized contracts describe data and remote operations. The owning grain implementations are responsible for validating commands and preserving state invariants before persistence.

Examples of implementation-level invariants include:

- quantities accepted by inventory and order operations
- reservation identity and repeat-call behavior
- idempotency-key handling
- valid status and reason combinations
- deterministic showcase failure paths
- state consistency before persistence

Result records and state properties should not duplicate the complete workflow validation policy.

## Idempotency boundary

The grain contracts expose explicit identifiers for repeat-call handling:

- order placement receives an `idempotencyKey`
- inventory reservation receives a `reservationId` and `orderId`
- payment authorization receives a `paymentId`, `orderId`, and `idempotencyKey`

The grain implementations should treat these identifiers as part of the operation contract. Changes to repeated-call behavior are behavioral compatibility changes even when method signatures remain unchanged.

## Persistence boundary

Grain implementations should persist durable state through Orleans persistence abstractions. They should not depend directly on the SQLite entity model or database context from `Ordering.Persistence.Sqlite`.

This keeps the dependency direction clear:

```text
Ordering.Grains
    uses Orleans persistence contracts

Ordering.Persistence.Sqlite
    implements Orleans grain storage

Ordering.Silo
    registers and hosts both
```

## Failure simulation

Order placement and payment authorization expose Boolean flags for deterministic failure simulation in the architecture workbench.

These flags are showcase controls, not general production payment or fault-injection APIs. Their behavior should remain deterministic so repeated scenarios can be compared across the microservices and virtual actor implementations.

## Prerequisites

Use the .NET SDK required by the repository. The current project structure targets `net10.0`.

Restore dependencies from the repository root:

```console
dotnet restore
```

The project requires the Orleans SDK for grain interfaces, generated proxies, aliases, and serialization code.

## Validate changes

From the repository root:

```console
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Changes to this project should cover at least:

- Orleans code generation
- alias stability
- serialization round trips
- grain activation by the expected key type
- order placement and retrieval
- inventory reset, reservation, retrieval, and release
- payment authorization
- repeated requests and idempotency behavior
- invalid quantities and identifiers
- simulated payment failures
- persistence and reactivation of grain state

## Adding or changing grain behavior

When modifying this project:

- Keep grain interfaces focused on remotely callable operations
- Keep grain identities aligned with their declared key types
- Preserve established Orleans aliases and serialization IDs
- Use new unused IDs for new serialized members
- Keep result contracts immutable unless mutation is required by the call contract
- Keep persisted state mutable and deserializable
- Validate commands in grain implementations before mutating persisted state
- Preserve deterministic idempotency and failure-simulation behavior
- Avoid exposing persistence-provider implementation details through grain contracts
- Treat changes to repeat-call behavior as compatibility changes
- Update this README when grain contracts, state models, aliases, or workflow boundaries change

## Naming conventions

- Grain interfaces use the `I` prefix and the `Grain` suffix
- Grain implementations use the `Grain` suffix
- Persisted models use the `State` suffix
- Operation outcomes use the `Result` suffix
- Point-in-time state views use the `Snapshot` suffix
- Orleans aliases are explicit stable string literals
- Serialized member IDs are explicit non-negative integers
- Public types and members use PascalCase

## Scope

Ordering.Grains defines the virtual actor contracts, implementations, state models, and serialized results for the ordering showcase. It does not configure silo hosting, clustering, storage-provider infrastructure, health endpoints, the Orleans Dashboard, deployment, autoscaling, or production recovery policy.

Those responsibilities belong to `Ordering.Silo`, `Ordering.Persistence.Sqlite`, shared hosting projects, and deployment infrastructure.
