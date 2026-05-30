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
