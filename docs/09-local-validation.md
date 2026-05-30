# Local validation

This checklist verifies that the repository works from a clean local checkout and that both architecture implementations expose the same comparison behavior.

Use this document after documentation changes, test changes, solution-file changes, scenario changes, or infrastructure changes.

## Build and test validation

Run the standard .NET validation commands from the repository root:

```powershell
dotnet clean
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

On Windows PowerShell, the helper script can also be used:

```powershell
./scripts/test-build.ps1
```

Expected result:

- restore succeeds
- build succeeds in `Release`
- all tests pass

## Visual Studio multi-startup validation

Visual Studio is a supported local development flow.

Configure multiple startup projects and set the action to start for these projects:

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

After startup, open:

```text
http://localhost:5000
```

Then run scenarios with architecture set to `Both`.

Expected result:

- the comparison UI loads
- backend status indicators are available
- scenarios can be run against both implementations
- the microservice result and virtual actor result are shown side by side

## Script-based local run

The services can also be started from PowerShell scripts.

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

This run mode is useful when validating the local stack without using Docker Compose or Visual Studio startup profiles.

## Docker Compose validation

The full stack can be started with Docker Compose:

```powershell
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

Expected result:

- Docker Compose builds all projects from a clean checkout
- all required containers start successfully
- the comparison UI loads
- scenarios can be run against both backend implementations

## Architecture routing checks

The comparison gateway supports selecting which implementation should handle a scenario run.

Validate the expected behavior for each architecture selection:

- `X-Architecture: microservices` returns only the microservice result
- `X-Architecture: virtual-actors` returns only the virtual actor result
- `X-Architecture: both` returns side-by-side results

The UI should make these result shapes visible without requiring direct API calls.

## Scenario checks

Run the core scenarios from the comparison UI and confirm that both implementations report the expected result shape.

Recommended scenarios:

- successful order
- insufficient inventory
- payment failure with compensation
- concurrent orders
- duplicate request
- hot product contention
- payment timeout after reservation

Expected validation focus:

- successful scenarios create the expected number of unique successful orders
- rejected submissions are reported separately from successful orders
- duplicate submissions do not reserve inventory more than once
- remaining inventory matches the expected scenario outcome
- timeout and compensation scenarios release inventory when the sample policy requires it

## Observability checks

For a completed scenario run, confirm that the UI displays a correlation ID.

Use that correlation ID to search logs across the relevant processes:

- `Comparison.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Ordering.Api`

Expected result:

- the same correlation ID appears in gateway and backend logs
- the correlation ID is diagnostic metadata, not part of the business contract

## Practical validation order

A safe local validation order is:

1. Run build and tests.
2. Start the stack using Visual Studio, scripts, or Docker Compose.
3. Open the comparison UI.
4. Run scenarios with architecture set to `Both`.
5. Confirm result terminology and remaining inventory are correct.
6. Confirm correlation IDs appear in logs.
7. Run build and tests one final time.

The goal is to verify both architecture implementations through the same externally visible behavior, not to validate one implementation with different rules than the other.
