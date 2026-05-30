# Local validation

This checklist is intended to keep the repository honest after each phase.

## Build and tests

```bash
dotnet clean
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

On Windows PowerShell, you can also run:

```powershell
./scripts/test-build.ps1
```

## Manual .NET run

Start the microservice-style backend:

```powershell
./scripts/run-microservices.ps1
```

Start the virtual actor-style backend:

```powershell
./scripts/run-virtual-actors.ps1
```

Start the comparison layer:

```powershell
./scripts/run-comparison.ps1
```

Open:

```text
http://localhost:5000
```

## Docker Compose validation

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

## Common checks

- `dotnet test` passes.
- The Blazor Server UI loads.
- `X-Architecture: microservices` returns only the microservice result.
- `X-Architecture: virtual-actors` returns only the virtual actor result.
- `X-Architecture: both` returns side-by-side results.
- Docker Compose can build all projects from a clean checkout.

### Visual Studio multi-startup validation

Visual Studio is a supported local development flow. Configure multiple startup projects and start these projects together:

- `src/Microservices/Inventory.Api`
- `src/Microservices/Payments.Api`
- `src/Microservices/Orders.Api`
- `src/VirtualActors/Ordering.Api`
- `src/Comparison/Comparison.Gateway`
- `src/Comparison/Comparison.Ui`

Expected local URLs:

- Inventory API: `http://localhost:5201`
- Payments API: `http://localhost:5202`
- Orders API: `http://localhost:5200`
- Ordering API: `http://localhost:5300`
- Comparison Gateway: `http://localhost:5100`
- Comparison UI: `http://localhost:5000`

After startup, open `http://localhost:5000` and run scenarios with architecture set to `Both`.

