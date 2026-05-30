# End-to-end validation

This checklist validates the repository as a complete comparison sample, not only as separate projects.

## 1. Clean build and tests

```bash
dotnet clean
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Expected result:

```text
Build succeeded.
All tests passed.
```

## 2. Local validation without Docker

If Docker is not installed or the Docker daemon is not available locally, run:

```powershell
./scripts/validate-e2e.ps1 -SkipDocker
```

This validates the .NET build and tests only.

## 3. Docker image build

On a machine with Docker available, run:

```bash
docker compose -f deploy/docker-compose.full.yml build
```

Expected result:

```text
All images build successfully.
```

## 4. Full stack startup

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

Expected result:

```text
The Blazor Server comparison dashboard loads.
```

## 5. Scenario validation through UI

Run these scenarios with architecture set to `Both`:

- Successful order
- Insufficient inventory
- Payment failure compensation
- Concurrent orders
- Duplicate request

Expected result:

```text
Both the Microservices and Virtual Actors cards render.
Both cards show final status, counts, remaining inventory, elapsed time, and event timeline.
```

## 6. Gateway header validation

The gateway should support these values:

```http
X-Architecture: microservices
X-Architecture: virtual-actors
X-Architecture: both
```

An unknown value should return `400 Bad Request`.

## 7. Cleanup

```bash
docker compose -f deploy/docker-compose.full.yml down -v
```

## 8. Public repository hygiene

Before publishing or pinning the repository, check that these files are not committed at the repository root unless intentionally kept in a tooling folder:

```text
add-phase*.ps1
fix-*.ps1
create-*.ps1
*-template.zip
```

Also check that local runtime artifacts are not committed:

```text
*.db
*.db-shm
*.db-wal
bin/
obj/
TestResults/
```

### Visual Studio local validation

As an alternative to scripts or Docker Compose, the full stack can be validated from Visual Studio by configuring multiple startup projects:

- `Inventory.Api`
- `Payments.Api`
- `Orders.Api`
- `Ordering.Api`
- `Comparison.Gateway`
- `Comparison.Ui`

Expected result:

- all startup projects launch successfully
- the comparison dashboard opens at `http://localhost:5000`
- backend status indicators are online
- scenarios can be run with architecture set to `Both`

