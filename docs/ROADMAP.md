# Implementation Roadmap: Sentinel Orchestrator

* **Repository:** `sentinel-orchestrator`
* **Stack:** .NET 10 (C# 14, Aspire, Clean Architecture, DDD), Angular 22 (Zoneless, Signals), PostgreSQL 17, Redis 7, SignalR
* **Execution Strategy:** Incremental vertical slices designed for AI-assisted coding agents. Each milestone produces a working, testable increment with clear acceptance criteria.

---

## Milestone 0: Monorepo Scaffolding & Local Orchestration (.NET 10 + Angular 22)
**Goal:** Bootstrap the solution structure, project dependencies, and .NET Aspire local orchestration so the entire stack compiles and boots cleanly.

* [x] **0.1 .NET 10 Solution & Clean Architecture Projects**
  * Create `SentinelOrchestrator.sln` with `Sentinel.AppHost`, `Sentinel.ServiceDefaults`, `Sentinel.Domain`, `Sentinel.Application`, `Sentinel.Infrastructure`, `Sentinel.Api`, `Sentinel.Domain.Tests`, and `Sentinel.Integration.Tests`.
  * Enforce dependency rules (`Domain` has zero external NuGet dependencies; `Application` references only `Domain`).
* [x] **0.2 Angular 22 Workspace Setup**
  * Initialize `src/sentinel-web` using Angular 22 (`--standalone`, `--zoneless`, `--strict`, Tailwind CSS).
  * Configure proxy (`proxy.conf.json`) for `/api` and `/hubs` forwarding to `Sentinel.Api`.
* [x] **0.3 .NET Aspire AppHost Configuration**
  * Wire PostgreSQL (`sentineldb`), Redis (`redis`), `Sentinel.Api`, and `sentinel-web` inside `Sentinel.AppHost/Program.cs` with service discovery and health checks.
* [x] **Acceptance Criteria:**
  * `dotnet build` and `pnpm --dir src/sentinel-web build` succeed with zero errors.
  * `dotnet run --project src/Sentinel.AppHost` launches the Aspire Dashboard showing healthy Postgres, Redis, API, and Angular resources.

---

## Milestone 1: Core Domain & Deterministic Policy Guardrails (`Sentinel.Domain`)
**Goal:** Implement the pure C# finite state machine, audit log model, and deterministic Policy Engine with 100% unit test coverage. (Ref: `ADR-001`)

* [x] **1.1 Workflow Aggregate & Finite State Machine**
  * Implement `WorkflowInstance`, `WorkflowStep`, `WorkflowAuditEntry`, `WorkflowStatus`, `PolicyVerdict`, and `RiskTier`.
  * Enforce strict state transition guards (`Pending` -> `Running` -> `EvaluatingPolicy` -> `AwaitingApproval` | `BlockedByPolicy` | `ExecutingStep` -> `Completed` | `Failed` | `Rejected`).
* [x] **1.2 Deterministic Policy Engine**
  * Implement `IPolicyEvaluator` and `DeterministicPolicyEngine` with explicit rules:
    * `Deny`: Hard-blocked destructive actions (`DeleteProductionVolume`, `DropDatabase`, `DisableFirewall`) or empty proposals.
    * `RequireApproval`: `High` or `Critical` risk actions targeting `production`, or any `Critical` risk action.
    * `Allow`: Low/medium risk actions within safe boundaries.
* [x] **1.3 Domain Unit Tests (`Sentinel.Domain.Tests`)**
  * Cover all valid transitions, invalid transition exceptions, audit trail generation, and policy verdicts (`Allow`, `RequireApproval`, `Deny`).
* [x] **Acceptance Criteria:**
  * `dotnet test tests/Sentinel.Domain.Tests` passes completely in < 1 second without Docker or database dependencies.

---

## Milestone 2: Persistence, Transactional Outbox & Concurrency (`Sentinel.Infrastructure`)
**Goal:** Implement PostgreSQL persistence, EF Core mappings with `xmin` optimistic concurrency, Transactional Outbox, and Redis idempotency locks. (Ref: `ADR-002`)

* [x] **2.1 EF Core DbContext & Migrations**
  * Configure `SentinelDbContext` with PostgreSQL mappings for `WorkflowInstance`, `WorkflowAuditEntry`, and `OutboxMessage`.
  * Map `WorkflowInstance.RowVersion` to PostgreSQL `xmin` system column for optimistic concurrency.
* [x] **2.2 Transactional Outbox & Background Worker**
  * Intercept domain state transitions to persist `OutboxMessage` records atomically within the same `SaveChangesAsync` transaction.
  * Implement `OutboxProcessorWorker` using PostgreSQL `FOR UPDATE SKIP LOCKED` to dispatch step execution jobs safely across concurrent workers.
* [x] **2.3 Pluggable Agent Planner & Step Executors**
  * Implement `IAgentPlanner` with `DeterministicMockPlanner` (default zero-dependency realistic planner) and optional `OllamaAgentPlanner`.
  * Implement `IStepExecutor` with Redis idempotency guard (`{WorkflowId}:{StepIndex}:{Attempt}`).
* [x] **2.4 Integration Tests (`Sentinel.Integration.Tests`)**
  * Use `Testcontainers.PostgreSql` and `Testcontainers.Redis` to verify atomic outbox persistence, worker recovery, and `DbUpdateConcurrencyException` on simultaneous approvals.
* [x] **Acceptance Criteria:**
  * `dotnet test tests/Sentinel.Integration.Tests` passes against real PostgreSQL and Redis containers.

---

## Milestone 3: Application Use Cases, Minimal APIs & Real-Time SignalR (`Sentinel.Application` & `Sentinel.Api`)
**Goal:** Expose REST endpoints, automatic scenario seeding, OpenAPI/Scalar docs, and real-time SignalR notifications. (Ref: `ADR-003`, `ADR-004`)

* [x] **3.1 Application CQRS Commands & Queries**
  * Implement handlers for `CreateWorkflowCommand`, `TriggerScenarioCommand`, `ApproveWorkflowCommand`, `RejectWorkflowCommand`, `GetWorkflowsQuery`, and `GetWorkflowByIdQuery`.
* [x] **3.2 Minimal API Endpoints & RFC 7807 Error Handling**
  * Expose `/api/workflows`, `/api/workflows/{id}`, `/api/workflows/{id}/approve`, `/api/workflows/{id}/reject`, and `/api/scenarios/{scenarioKey}/trigger`.
  * Map domain/concurrency errors cleanly (`400 Bad Request`, `404 Not Found`, `409 Conflict` via `ProblemDetails`).
  * Enable .NET 10 built-in OpenAPI + Scalar UI (`/scalar/v1`).
* [x] **3.3 SignalR `WorkflowHub` & Automatic Seeding**
  * Broadcast `WorkflowUpdated` and `ApprovalRequested` events to connected clients upon state changes.
  * Implement startup database migration + seeder with the 3 showcase scenarios (`Auto-Allowed`, `Human-in-the-Loop`, `BlockedByPolicy`).
* [x] **Acceptance Criteria:**
  * Triggering scenarios via Scalar/HTTP executes workflows end-to-end, persists audit logs, and emits SignalR events.

---

## Milestone 4: Reactive Control Plane UI (`sentinel-web` — Angular 22)
**Goal:** Build a modern, zoneless Angular 22 control plane powered by Signals and SignalR to inspect workflows and manage Human-in-the-Loop approvals. (Ref: `ADR-003`)

* [x] **4.1 SignalR Service & Signal-Based Feature Store**
  * Create `WorkflowSignalRService` with automatic reconnect and state resync.
  * Create `WorkflowStore` using Angular `signal()` and `computed()` (`activeWorkflows`, `pendingApprovals`, `blockedWorkflows`).
* [x] **4.2 Live Dashboard & Scenario Trigger Bar**
  * Build KPI summary cards, real-time workflow feed with status badges, and one-click buttons to trigger the 3 showcase scenarios.
* [x] **4.3 Workflow Execution Inspector & HITL Approval Modal/Drawer**
  * Display step-by-step timeline, input/output JSON viewer, Policy Engine decision banner, and chronological audit log.
  * Implement **Approve / Reject** panel with mandatory operator comment and graceful `409 Conflict` handling.
* [x] **Acceptance Criteria:**
  * Triggering the "High Risk Production" scenario in the UI pauses at `AwaitingApproval` in real time, displays the approval prompt without page refresh, and completes immediately upon operator approval.

---

## Milestone 5: Polish, Docker Compose Fallback & Portfolio Readiness
**Goal:** Ensure frictionless onboarding for technical reviewers and recruiters inspecting the repository.

* [x] **5.1 Standalone `docker-compose.yml`**
  * Provide a pure-Docker Compose file (`postgres`, `redis`, `api`, `web`) for reviewers running without .NET Aspire workloads installed.
* [x] **5.2 End-to-End Smoke Verification & README Polish**
  * Verify cold-start execution (`docker compose up --build` and `dotnet run --project src/Sentinel.AppHost`).
  * Add UI screenshots / GIFs and OpenTelemetry trace examples to `README.md`.
* [x] **Acceptance Criteria:**
  * A fresh clone runs cleanly in 1 command and demonstrates all 3 scenarios end-to-end.