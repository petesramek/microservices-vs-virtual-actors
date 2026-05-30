# End-to-end validation

This checklist validates the repository as a complete architecture comparison sample, not only as separate projects.

Use this document when you want to confirm that the full stack can be built, started, and used to run comparison scenarios through the UI and gateway.

## 1. Clean build and tests

Run from the repository root:

```powershell
dotnet clean
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Expected result:

- restore succeeds
- build succeeds in `Release`
- all tests pass

## 2. Local validation without Docker

If Docker is not installed or the Docker daemon is not available locally, run:

```powershell
./scripts/validate-e2e.ps1 -SkipDocker
```

Expected result:

- .NET restore, build, and tests complete successfully
- Docker-specific validation is skipped

This path is useful for validating source code and tests on machines where Docker is not available.

## 3. Visual Studio local validation

Visual Studio is a supported local validation flow.

Configure multiple startup projects and set the action to start for these projects:

- `Inventory.Api`
- `Payments.Api`
- `Orders.Api`
- `Ordering.Api`
- `Comparison.Gateway`
- `Comparison.Ui`

Expected result:

- all startup projects launch successfully
- the comparison dashboard opens at `http://localhost:5000`
- backend status indicators are available
- scenarios can be run with architecture set to `Both`

This is the recommended validation path when working primarily inside Visual Studio because each service keeps its own launch profile, debugging experience, and output window.

## 4. Docker image build

On a machine with Docker available, build the full stack:

```powershell
docker compose -f deploy/docker-compose.full.yml build
```

Expected result:

- all images build successfully
- project references resolve correctly inside the Docker build context
- no local IDE state is required for the container build

## 5. Full stack startup

Start the full comparison stack:

```powershell
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

Expected result:

- the Blazor Server comparison dashboard loads
- the gateway is reachable
- both backend implementations are reachable through the comparison layer
- the UI can run scenarios against both implementations

## 6. Scenario validation through the UI

Run these scenarios with architecture set to `Both`:

- successful order
- insufficient inventory
- payment failure with compensation
- concurrent orders
- duplicate request
- hot product contention
- payment timeout after reservation

Expected result:

- both the Microservices and Virtual Actors result cards render
- both cards show final status, counts, remaining inventory, elapsed time, and event timeline
- request-submission terminology is consistent across scenarios
- duplicate request scenarios report one unique successful order and idempotent duplicate responses
- concurrent scenarios do not over-reserve inventory
- timeout and compensation scenarios release inventory when the sample policy requires it

## 7. Gateway header validation

The gateway should support these architecture selection values:

```text
X-Architecture: microservices
X-Architecture: virtual-actors
X-Architecture: both
```

Expected result:

- `microservices` returns only the microservice result
- `virtual-actors` returns only the virtual actor result
- `both` returns side-by-side results
- an unknown value returns `400 Bad Request`

## 8. Result interpretation checks

When validating end to end, focus on externally visible behavior rather than implementation details.

Check that both implementations preserve these rules:

- successful orders are counted as unique successful orders
- rejected submissions are reported separately from successful orders
- duplicate submissions do not create multiple unique successful orders
- duplicate submissions do not reserve inventory more than once
- concurrent submissions do not over-reserve inventory
- remaining inventory matches the expected scenario outcome
- elapsed time is treated as local feedback, not as a benchmark

## 9. Observability checks

For a completed scenario run, confirm that the UI displays a correlation ID.

Use that correlation ID to inspect logs across the relevant processes:

- `Comparison.Gateway`
- `Orders.Api`
- `Inventory.Api`
- `Payments.Api`
- `Ordering.Api`

Expected result:

- the same correlation ID can be found across gateway and backend logs for the same run
- the correlation ID is diagnostic metadata, not part of the business contract

## 10. Shutdown

When using Docker Compose, stop the full stack with:

```powershell
docker compose -f deploy/docker-compose.full.yml down -v
```

Expected result:

- all containers for the full comparison stack stop
- volumes created by the compose stack are removed

## Validation summary

A successful end-to-end validation means:

- the solution builds
- all tests pass
- the full stack starts through at least one supported local run path
- the dashboard loads
- both implementations can run the same scenarios
- scenario results preserve the expected business invariants
- architecture selection works through the gateway
- correlation IDs can be used to inspect logs for a scenario run

The goal is to validate the repository as a complete comparison sample with consistent externally visible behavior across both architecture implementations.
