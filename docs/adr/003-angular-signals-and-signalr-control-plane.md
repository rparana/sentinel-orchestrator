# ADR-003: Reactive Control Plane with Angular 22 Zoneless + Signals and SignalR

* **Status:** Accepted
* **Date:** 2026-11-18
* **Author:** Rafael Paraná
* **Scope:** `sentinel-web` (Angular Frontend), `Sentinel.Api`

---

## 1. Context and Problem Statement

An orchestration and approval control plane requires live visibility into asynchronous workflow transitions, step durations, and incoming Human-in-the-Loop (HITL) approval requests. Traditional HTTP polling introduces unnecessary load and UI lag, while complex global state libraries (e.g., full NgRx boilerplate) can overcomplicate a focused real-time dashboard.

---

## 2. Decision Drivers

* **Real-Time Responsiveness:** Operators must see workflow transitions and approval alerts immediately as background workers process steps.
* **Fine-Grained Reactivity:** UI updates should re-render only affected components without Zone.js overhead or subscription leaks.
* **Modern Angular Best Practices:** Showcase standalone components, strict TypeScript typing, and clean separation between transport services and reactive state stores.

---

## 3. Considered Options

1. **HTTP Short Polling + RxJS BehaviorSubjects:** Poll workflow endpoints every 2–3 seconds. *Rejected* due to stale UI states during fast step executions and wasted network traffic.
2. **Full NgRx Store + Effects:** Adopt Redux-style actions, reducers, and selectors. *Rejected* as excessive boilerplate for an event-driven control plane where the backend owns authoritative state.
3. **SignalR Real-Time Push + Angular 22 Zoneless + Signals Feature Stores (Selected):** Stream domain state-change events from .NET via SignalR and manage local reactive state using Angular `signal()` and `computed()`.

---

## 4. Decision Outcome

We selected **Option 3**.

* **Real-Time Transport (`SignalR`):** `Sentinel.Api` exposes a typed `WorkflowHub`. When the Outbox/Worker commits a state transition, it broadcasts lightweight event notifications (`WorkflowUpdated`, `ApprovalRequested`) to connected clients.
* **Signal-Based State Management:** Feature stores (`WorkflowStore`, `ApprovalStore`) encapsulate `signal<T>()` state, `computed()` selectors (e.g., `pendingApprovalsCount`, `activeWorkflows`), and explicit state updaters triggered by SignalR events or REST mutations.
* **Optimistic UI with Reconciliation:** Approval/rejection commands update UI feedback immediately while reconciling with the authoritative HTTP response (handling `409 Conflict` gracefully if another operator acted first).

---

## 5. Consequences

* **Positive:** Sub-second UI updates; minimal boilerplate; automatic cleanup and predictable reactivity via Signals; clean TypeScript contracts mirroring backend DTOs.
* **Negative:** Requires connection lifecycle handling (automatic reconnect + state resync on reconnect) when the backend restarts during local development.