# ADR-001: Deterministic State Machine and Policy Guardrails for Agentic Orchestration

* **Status:** Accepted
* **Date:** 2026-11-18
* **Author:** Rafael Paraná
* **Scope:** `Sentinel.Domain`, `Sentinel.Application`

---

## 1. Context and Problem Statement

Operational workflows that incorporate LLM-based planning or automated remediation carry inherent risks: probabilistic outputs can propose invalid parameters, trigger unintended side effects, or bypass operational compliance rules. Additionally, workflows involving human approval must survive process restarts and remain suspended indefinitely without holding open threads or database locks.

We need an execution model that separates **probabilistic action proposal** from **deterministic policy enforcement and state progression**.

---

## 2. Decision Drivers

* **Strict Governance:** No side-effecting step may execute without passing deterministic policy validation.
* **Durability:** Workflow state must survive worker crashes and support indefinite Human-in-the-Loop (HITL) pauses.
* **Domain Clarity:** Business rules and valid state transitions must be explicit, self-documenting, and testable without external infrastructure.

---

## 3. Considered Options

1. **Autonomous Agent Loop:** Allow an LLM framework to invoke tools directly in a loop until completion. *Rejected* due to non-deterministic control flow, weak auditability, and inability to cleanly pause/resume across restarts.
2. **Third-Party Workflow Engine (e.g., Temporal / Elsa):** Delegate orchestration to an external workflow cluster. *Rejected* because it adds heavy infrastructure overhead for local execution and hides core domain modeling decisions behind framework abstractions.
3. **Domain-Driven Finite State Machine + Deterministic Policy Engine (Selected):** Model workflow instances as rich DDD aggregates enforcing explicit state transitions, gated by a pure C# Policy Engine.

---

## 4. Decision Outcome

We selected **Option 3**.

* **Proposal vs. Execution Boundary:** AI/Planner steps only produce structured `ActionProposal` value objects validated against strict schemas. They never hold credentials or execute side effects directly.
* **Explicit State Transitions:** The `WorkflowInstance` aggregate encapsulates state transitions (`Pending` -> `EvaluatingPolicy` -> `AwaitingApproval` -> `ExecutingStep` -> `Completed`), throwing domain exceptions on invalid transitions.
* **Pure Policy Engine:** `IPolicyEvaluator` evaluates proposals against deterministic rules (environment, risk tier, action allowlists) and emits a structured `PolicyDecision` (`Allow`, `RequireApproval`, `Deny`) persisted in the audit log.

---

## 5. Consequences

* **Positive:** 100% deterministic control flow; complete audit trail of every decision; domain logic is tested in milliseconds with zero mocks/containers.
* **Negative:** Requires explicit modeling of step contracts and transition handlers for each new step type.