# Deployment

This folder contains local Docker Compose files for comparing deployment shapes.

## Full stack

```bash
docker compose -f deploy/docker-compose.full.yml up --build
```

Open:

```text
http://localhost:5000
```

## Microservices only

```bash
docker compose -f deploy/microservices/docker-compose.yml up --build
```

## Virtual actors only

```bash
docker compose -f deploy/virtual-actors/docker-compose.yml up --build
```

## Cleanup

```bash
docker compose -f deploy/docker-compose.full.yml down -v
```

## Notes

The Dockerfiles copy all project files required for restore before publishing. This is important because several projects have project references, especially `Ordering.Api` and `Ordering.Silo`, which depend on `Ordering.Grains`.
