# Product Requirements Document (PRD): Sentinel Orchestrator

* **Product Name:** Sentinel Orchestrator
* **Repository:** `sentinel-orchestrator`
* **Version:** 1.0 (MVP)
* **Author:** Rafael Paraná
* **Status:** Ready for Development
* **Target Stack:** .NET 10 (Clean Architecture, DDD, Aspire), Angular 22 (Standalone Components, Signals), PostgreSQL, Redis, SignalR, OpenTelemetry

---

## 1. Executive Summary & Vision

**Sentinel Orchestrator** is a local-first, governed workflow and agentic orchestration platform designed to execute operational automation safely. It bridges probabilistic AI reasoning (LLMs/agents proposing diagnostic or remediation plans) with **deterministic policy enforcement, transactional durability, and Human-in-the-Loop (HITL) approval gates**.

The primary goal of this project is to demonstrate enterprise-grade architectural patterns—state machines, transactional outbox, optimistic concurrency, policy-as-code, real-time control planes, and distributed observability—inside a self-contained monorepo executable locally with zero cloud dependencies.

---

## 2. Target Audience & Use Cases

### Target Personas
1. **Platform / SRE Engineer:** Creates operational runbooks/workflows (e.g., incident triage, service restart, traffic shifting) and reviews high-risk automated actions before execution.
2. **Engineering Lead / Architect:** Evaluates system reliability, inspects audit trails, configures risk policies, and monitors end-to-end execution latency.

### Core Showcase Scenarios (Pre-seeded in Local Environment)
1. **Scenario A — Automated Incident Triage & Safe Remediation (Low Risk -> Auto-Approved):**
   * An alert payload triggers a workflow -> AI/Diagnostic step analyzes mock logs -> Policy Engine validates read-only/low-risk action -> Automatically executes diagnostic report -> Completes with full audit trail.
2. **Scenario B — Production Infrastructure Mutation (High Risk -> Human-in-the-Loop):**
   * Workflow proposes a high-impact action (e.g., `ScaleDatabaseCluster` or `RollbackProductionDeploy`) -> Policy Engine flags `RequiresApproval` -> Execution pauses in `AwaitingApproval` -> Real-time notification appears in Angular Control Plane -> Operator inspects proposed payload, approves with justification -> Worker resumes and completes execution.
3. **Scenario C — Policy Violation Rejection:**
   * Agent/Step proposes an action violating a hard guardrail (e.g., `DeleteProductionVolume` or missing required parameters) -> Policy Engine blocks transition -> Workflow transitions to `BlockedByPolicy` with explicit violation reason.

---

## 3. Functional Requirements

### 3.1 Workflow Definition & Execution Engine
* **FR-01 (Workflow Templates):** Support defining multi-step workflows via structured JSON/YAML definitions or API payloads, specifying sequential steps, step types (`Diagnostic`, `AiPlanner`, `ActionExecutor`), and risk metadata.
* **FR-02 (Deterministic State Machine):** Every workflow instance must follow strict state transitions:
  `Pending` -> `Running` -> `EvaluatingPolicy` -> (`AwaitingApproval` | `BlockedByPolicy` | `ExecutingStep`) -> `Completed` | `Failed` | `Rejected`.
* **FR-03 (Pluggable Agent/LLM Provider):** Support an `IAgentPlanner` abstraction with two built-in implementations:
  * `DeterministicMockPlanner`: Zero-dependency simulator producing realistic structured action proposals with simulated latency (works out of the box).
  * `OllamaAgentPlanner`: Optional integration with local Ollama containers for real LLM reasoning.
* **FR-04 (Idempotency & Crash Recovery):** Step executions must use deterministic idempotency keys (`workflowId:stepIndex:attempt`) so worker restarts never duplicate side effects.

### 3.2 Deterministic Policy Engine (Guardrails)
* **FR-05 (Policy Evaluation):** Before any `ActionExecutor` step runs, the Policy Engine evaluates the proposed action against active rules (e.g., environment target `prod` vs `staging`, action risk tier, blast radius).
* **FR-06 (Policy Outcomes):** Evaluation must produce one of three deterministic verdicts:
  * `Allow`: Proceed directly to step execution.
  * `RequireApproval`: Suspend workflow and enqueue HITL approval task.
  * `Deny`: Immediately halt workflow and record policy violation details.
* **FR-07 (Schema Validation):** AI-proposed action payloads must pass strict JSON Schema / strongly-typed validation before reaching the Policy Engine.

### 3.3 Human-in-the-Loop (HITL) Approval Center
* **FR-08 (Approval Queue):** Expose pending approvals with full context: workflow name, step details, AI reasoning summary, proposed JSON payload, and triggered policy rule.
* **FR-09 (Approve / Reject Actions):** Operators can approve or reject a suspended workflow with a mandatory audit comment. Rejections transition the workflow to `Rejected`.

### 3.4 Real-Time Control Plane (Angular UI)
* **FR-10 (Live Dashboard):** Display active workflows, status counters, pending approval badges, and recent execution metrics updated in real time via SignalR.
* **FR-11 (Workflow Execution Inspector):** Interactive step-by-step timeline showing:
  * Step status and duration (ms)
  * Input context and structured output/proposal
  * Policy evaluation badge and matched rule explanation
  * Audit log of human approvals/rejections
* **FR-12 (Trigger & Chaos/Load Simulator):** UI controls to trigger pre-configured scenarios with one click and inspect how the system handles concurrent workflows.

---

## 4. Non-Functional Requirements

* **NFR-01 (Zero-Friction Local Execution):** The entire stack (API, Background Worker, PostgreSQL, Redis, Angular SPA, and OpenTelemetry Dashboard) must boot via a single command (`dotnet run --project src/Sentinel.AppHost` or `docker compose up --build`).
* **NFR-02 (Transactional Consistency):** State transitions and outbox messages must be committed atomically in PostgreSQL.
* **NFR-03 (Concurrency Safety):** Optimistic concurrency control (`xmin` / version token) must prevent double-approvals or split-brain worker execution.
* **NFR-04 (End-to-End Observability):** Distributed traces (`traceId`) must propagate from the Angular HTTP request -> .NET API -> Outbox -> Background Worker -> Step Execution, visible in the .NET Aspire Dashboard.
* **NFR-05 (Automated Verification):** Core domain state transitions and policy rules must be covered by unit tests; persistence and outbox concurrency must be verified via integration tests using Testcontainers.

---

## 5. Monorepo Structure

```text
sentinel-orchestrator/
├── docs/
│   ├── PRD.md
│   ├── architecture.mmd
│   └── adr/
│       ├── 001-deterministic-state-machine-and-guardrails.md
│       ├── 002-transactional-outbox-and-concurrency.md
│       ├── 003-angular-signals-and-signalr-control-plane.md
│       └── 004-dotnet-aspire-local-orchestration.md
├── src/
│   ├── Sentinel.AppHost/            # .NET Aspire orchestrator
│   ├── Sentinel.ServiceDefaults/    # OpenTelemetry, health checks, resilience
│   ├── Sentinel.Domain/             # Aggregates, State Machine, Policy Rules (Zero dependencies)
│   ├── Sentinel.Application/        # Use cases, CQRS handlers, ports/interfaces
│   ├── Sentinel.Infrastructure/     # EF Core (PostgreSQL), Outbox Worker, Redis, AI Adapters
│   ├── Sentinel.Api/                # Minimal APIs, SignalR Hubs, ProblemDetails
│   └── sentinel-web/                # Angular SPA (Standalone, Signals, Tailwind)
├── tests/
│   ├── Sentinel.Domain.Tests/       # Pure unit tests for state transitions & policies
│   └── Sentinel.Integration.Tests/  # Testcontainers (PostgreSQL + Redis)
└── docker-compose.yml               # Alternative single-command container execution
```
## 6. Out of Scope (v1.0)
* Multi-tenant SSO/OAuth external identity providers (simulated role switcher in UI for local demo clarity).

* Visual drag-and-drop DAG canvas builder (v1 focuses on structured linear/conditional step pipelines and execution inspection).

* Direct cloud provider mutations (actions execute against safe simulated infrastructure adapters).