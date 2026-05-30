# Microservices vs Virtual Actors

Hands-on comparison of microservice-style and virtual actor-style designs for the same order workflow.

This repository compares development model, state ownership, communication, concurrency, failure handling, deployment, scaling, and operational trade-offs.

It is not trying to prove that one model is always better.

## Comparison focus

- Microservices: service/capability boundaries, independently deployable APIs, service-owned state.
- Virtual actors: stateful identity boundaries, actor-owned state, runtime-managed activation and concurrency.

## Workflow

The same order workflow is used for both implementations:

1. Place an order.
2. Reserve inventory.
3. Authorize payment.
4. Complete or reject the order.
5. Release inventory when payment fails.
6. Prevent inventory over-reservation under concurrent load.
7. Handle duplicate requests with idempotency keys.

## Acceptance scenarios

Both implementations must satisfy the same acceptance scenarios:

- Successful order
- Insufficient inventory
- Payment failure compensation
- Concurrent orders do not over-reserve inventory
- Duplicate order request is idempotent

## Projects

- `ArchitectureComparison.Contracts` — shared contracts and scenario models.
- `Comparison.Gateway` — backend selection/proxy layer using `X-Architecture`.
- `Comparison.Ui` — Blazor Server comparison dashboard.
- `Orders.Api`, `Inventory.Api`, `Payments.Api` — microservice-style implementation.
- `Ordering.Api`, `Ordering.Grains`, `Ordering.Silo` — virtual actor-style implementation.
- `ArchitectureComparison.AcceptanceTests` — gateway-level acceptance tests.
- `Microservices.Tests` — microservice workflow tests.
- `VirtualActors.Tests` — Orleans workflow tests.

## Dashboard

The Blazor Server dashboard includes:

- scenario runner
- side-by-side architecture results
- event timelines
- topology page
- trade-offs page

## How to build and test

```bash
dotnet restore
dotnet test
```

On Windows PowerShell:

```powershell
./scripts/test-build.ps1
```

For the complete validation pass:

```powershell
./scripts/validate-e2e.ps1
```

## Run locally with .NET

Run the backend services in separate terminals.

### Microservices backend

```bash
dotnet run --project src/Microservices/Inventory.Api --urls http://localhost:5201
dotnet run --project src/Microservices/Payments.Api --urls http://localhost:5202
dotnet run --project src/Microservices/Orders.Api --urls http://localhost:5200
```

Or on Windows PowerShell:

```powershell
./scripts/run-microservices.ps1
```

### Virtual actors backend

```bash
dotnet run --project src/VirtualActors/Ordering.Api --urls http://localhost:5300
```

Or on Windows PowerShell:

```powershell
./scripts/run-virtual-actors.ps1
```

### Comparison layer

```bash
dotnet run --project src/Comparison/Comparison.Gateway --urls http://localhost:5100
dotnet run --project src/Comparison/Comparison.Ui --urls http://localhost:5000
```

Or on Windows PowerShell:

```powershell
./scripts/run-comparison.ps1
```

Open:

```text
http://localhost:5000
```

## Run with Docker Compose

Run the full comparison stack:

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

Run only the microservice-style stack:

```bash
docker compose -f deploy/microservices/docker-compose.yml up --build
```

Run only the virtual actor-style stack:

```bash
docker compose -f deploy/virtual-actors/docker-compose.yml up --build
```

## Header-based architecture selection

The comparison gateway uses this header:

```http
X-Architecture: microservices
```

or:

```http
X-Architecture: virtual-actors
```

or:

```http
X-Architecture: both
```

## Documentation

- [Problem](docs/01-problem.md)
- [Microservices design](docs/02-microservices-design.md)
- [Virtual actors design](docs/03-virtual-actors-design.md)
- [Development comparison](docs/04-development-comparison.md)
- [Deployment comparison](docs/05-deployment-comparison.md)
- [Scaling comparison](docs/06-scaling-comparison.md)
- [Trade-offs](docs/07-tradeoffs.md)
- [Local validation](docs/08-local-validation.md)
- [UI dashboard](docs/09-ui-dashboard.md)
- [End-to-end validation](docs/10-end-to-end-validation.md)
- [Out of scope](docs/11-out-of-scope.md)

## Repository hygiene

Before publishing, run:

```powershell
./scripts/check-repo-hygiene.ps1
```

Generated phase scripts, template zip files, and local database files should not be committed to the public repository root.

## Notes

The repository is intentionally small. The value is in seeing the same workflow implemented, tested, deployed, and scaled through two different architectural models.
