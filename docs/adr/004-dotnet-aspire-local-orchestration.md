# ADR-004: Zero-Friction Local Orchestration and Observability with .NET Aspire

* **Status:** Accepted
* **Date:** 2026-11-18
* **Author:** Rafael Paraná
* **Scope:** `Sentinel.AppHost`, `Sentinel.ServiceDefaults`, `docker-compose.yml`

---

## 1. Context and Problem Statement

Portfolio repositories that require complex manual setup (installing specific database versions, configuring connection strings, running multiple terminals, or provisioning cloud accounts) are rarely executed by technical reviewers. Furthermore, distributed asynchronous workflows are hard to debug without correlated traces across the frontend, API, and background workers.

---

## 2. Decision Drivers

* **One-Command Startup:** Any developer with .NET SDK and Docker Desktop must be able to run the entire system locally in a single command.
* **Built-In Distributed Observability:** OpenTelemetry traces, metrics, and structured logs must work out of the box without manual Prometheus/Jaeger/Grafana wiring.
* **Production-Like Dependencies:** Use real PostgreSQL and Redis instances rather than in-memory database fakes that hide concurrency and SQL behaviors.

---

## 3. Considered Options

1. **Manual Multi-Terminal Setup + SQLite/In-Memory Mocks:** *Rejected* because in-memory providers do not support PostgreSQL concurrency tokens (`xmin`) or `FOR UPDATE SKIP LOCKED`.
2. **Docker Compose Only:** *Partially Adopted* as a fallback (`docker-compose.yml`), but lacks integrated developer dashboard and automatic service discovery during active coding.
3. **.NET Aspire AppHost + Docker Compose Fallback (Selected):** Use `.NET Aspire` as the primary local orchestrator managing PostgreSQL, Redis, the .NET API, the Outbox Worker, and the Angular dev server, with unified OpenTelemetry collection.

---

## 4. Decision Outcome

We selected **Option 3**.

* **Centralized AppHost (`Sentinel.AppHost`):** Declares all infrastructure containers (PostgreSQL, Redis) and application services (`Sentinel.Api`, `Sentinel.Worker`, `sentinel-web`) with automatic service discovery and health checks.
* **Unified Telemetry (`Sentinel.ServiceDefaults`):** Configures OpenTelemetry tracing, metrics, and health endpoints across all .NET services so every workflow execution displays a complete waterfall trace in the Aspire Dashboard.
* **Automatic Database Seeding:** On startup in development mode, the API applies EF Core migrations and seeds realistic workflow templates and sample scenarios so the UI is immediately interactive.

---

## 5. Consequences

* **Positive:** Reviewers can inspect running services, live OpenTelemetry traces, structured logs, and PostgreSQL data immediately after booting; real database semantics are preserved locally.
* **Negative:** Requires Docker/Podman runtime available locally to spin up PostgreSQL and Redis containers.