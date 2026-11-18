# ADR-002: Transactional Outbox Pattern and Optimistic Concurrency Control

* **Status:** Accepted
* **Date:** 2026-11-18
* **Author:** Rafael Paraná
* **Scope:** `Sentinel.Infrastructure`, `Sentinel.Api`

---

## 1. Context and Problem Statement

In an asynchronous workflow orchestrator, updating workflow state in PostgreSQL and dispatching background execution tasks (or real-time SignalR notifications) are two distinct operations. If the process crashes between committing database state and dispatching a job, workflows become stuck in intermediate states (dual-write problem).

Furthermore, concurrent actions—such as two operators clicking "Approve" simultaneously or two background workers picking up the same step—can cause race conditions or duplicate execution.

---

## 2. Decision Drivers

* **Atomicity:** State mutations and pending execution commands must succeed or fail together.
* **At-Least-Once Delivery with Idempotency:** Background step execution must recover cleanly from transient failures without duplicating side effects.
* **Zero Split-Brain Execution:** Concurrent mutations on the same workflow instance must be detected and rejected safely.

---

## 3. Considered Options

1. **In-Memory Channels (`System.Threading.Channels`) Only:** Dispatch work directly in memory after saving to PostgreSQL. *Rejected* because queued work is lost on process restart.
2. **External Message Broker with Two-Phase Commit (2PC):** Coordinate PostgreSQL and RabbitMQ/Kafka via distributed transactions. *Rejected* due to complexity, latency, and lack of broad support.
3. **PostgreSQL Transactional Outbox + Optimistic Concurrency + Redis Idempotency Locks (Selected):** Persist outbox messages in the same ACID transaction as the aggregate state, enforce row versioning, and guard step execution with idempotency keys.

---

## 4. Decision Outcome

We selected **Option 3**.

1. **Transactional Outbox:** Whenever `WorkflowInstance` transitions to a state requiring asynchronous work (e.g., `Running`, `ExecutingStep`), domain events are converted into `OutboxMessage` records within the same EF Core `DbContext` transaction.
2. **Outbox Processor Worker:** A hosted background service polls unprocessed `OutboxMessage` rows using `FOR UPDATE SKIP LOCKED` in PostgreSQL, allowing safe horizontal scaling of workers without contention.
3. **Optimistic Concurrency:** `WorkflowInstance` maps PostgreSQL's system `xmin` column as a concurrency token in EF Core. If two operators attempt to approve the same step concurrently, the second request fails cleanly with `HTTP 409 Conflict`.
4. **Idempotent Step Execution:** Each step execution generates a deterministic key (`{WorkflowId}:{StepIndex}:{Attempt}`) stored in Redis/PostgreSQL to guarantee side effects execute at most once.

---

## 5. Consequences

* **Positive:** Guaranteed consistency between workflow state and background execution; safe concurrent operator actions; zero lost workflows on crash.
* **Negative:** Introduces minor polling latency (configurable, e.g., 200ms) between state commit and worker pickup, which is negligible for operational workflows.