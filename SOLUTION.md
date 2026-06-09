# SOLUTION.md — Design & Trade-offs

## Overview

LNDocumentProcessor is an ASP.NET Core 8 backend service built around three concerns: **intake** (accept and deduplicate documents), **storage** (persist raw content to object storage), and **processing** (generate a short preview asynchronously via a background worker).

The design is intentionally scoped to the assignment constraints — small documents (≤ 5 MB), tens of submissions per minute, local-first runability — while keeping the architecture compatible with a real cloud deployment.

---

## Architecture

```
Upstream Provider
     │  POST /documents
     ▼
┌──────────────────────────────────────┐
│          ASP.NET Core API            │
│  - Validate input                    │
│  - Deduplicate (provider + sourceId) │
│  - Assign internal documentId (GUID) │
│  - Store raw content → Blob/S3       │
│  - Publish message → Queue           │
│  - Persist metadata + status         │
│  - Return 201 / 200 (duplicate)      │
└───────────────┬──────────────────────┘
                │ queue message
                ▼
┌──────────────────────────────────────┐
│    BackgroundService (Worker)        │
│  - Consume queue message             │
│  - Read blob content                 │
│  - Generate short preview            │
│  - Update status → processed / failed│
└──────────────────────────────────────┘
                │
                ▼
     GET /documents/{id}
     GET /documents/{id}/preview
```

---

## Layer Breakdown

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `LNDocumentProcessor.Domain` | `Document` aggregate, `AuditEntry`, `DocumentStatus` enum — no framework dependencies |
| Application | `LNDocumentProcessor.Application` | Use-case handlers (`SubmitDocument`, `GetDocument`), port interfaces (`IDocumentRepository`, `IStorageService`, `IQueuePublisher`) |
| Infrastructure | `LNDocumentProcessor.Infrastructure` | Implementations: file-system storage, in-memory queue, in-memory/SQLite repository |
| API | `LNDocumentProcessor.Api` | ASP.NET Core host, endpoint mapping, DI composition root, Swagger |

---

## Key Design Decisions

### 1. Cloud-Abstracted Storage and Queue

Storage and queue are accessed only through interfaces (`IStorageService`, `IQueuePublisher`) defined in the Application layer. The Infrastructure layer supplies two sets of implementations:

- **Local** — `FileSystemStorageService`, `InMemoryQueuePublisher` (backed by `Channel<T>`)
- **Cloud** — _(stubs, wired to Azure Blob / Service Bus or AWS S3 / SQS SDK classes)_

Switching from local to cloud requires only a configuration change and DI registration swap — no business logic changes.

**Trade-off**: Adds an abstraction layer. For a small service this is boilerplate, but it is the correct call here because the assignment explicitly requires local-runability while keeping the design cloud-compatible.

### 2. Deduplication Strategy

Deduplication key: `provider + sourceDocumentId`. On intake, the repository is queried for an existing record with this composite key before any side effects (storage write, queue publish). If found, the existing document is returned with HTTP 200 instead of 201.

**Trade-off**: This is a synchronous check against the local repository, which is sufficient at the stated throughput (tens per minute). At higher scale, a distributed lock or idempotent message key on the queue would be needed to guard against concurrent duplicate submissions.

### 3. In-Process Background Worker

The preview worker runs as a .NET `BackgroundService` inside the same process as the API. It consumes from an injected `IQueueConsumer` (backed by `Channel<T>` locally).

**Trade-off**: Shares the process lifetime with the API — a worker crash could affect the API. For this scale and scope that is an acceptable simplification. In production the worker would be a separate deployable (e.g. Azure Functions, AWS Lambda, or a dedicated container) consuming from a real queue.

### 4. Preview Generation

The preview is a short text extract — the first N characters of the document content (for text-based documents) or a byte-size indicator for binary formats. This is intentionally minimal.

**Trade-off**: Real legal document previews would involve PDF parsing (PdfPig, iTextSharp) or OCR. That complexity is out of scope; the abstraction point (`IPreviewGenerator`) is in place to swap in a real implementation.

### 5. Metadata Persistence

Document metadata and audit trail are stored in an in-memory repository (`IDocumentRepository`) for local runs. The repository interface is compatible with a real database (SQL Server, Cosmos DB) via the same pattern.

**Trade-off**: In-memory state is lost on restart. Acceptable for local development and the assignment scope. A production implementation would use a durable store with an appropriate schema and index on the deduplication key.

### 6. Audit Trail

Each document carries a list of `AuditEntry` records (status + timestamp). Statuses in order: `Received → Stored → Queued → Processing → Processed` (or `Failed`).

**Trade-off**: Kept deliberately flat and append-only. No event sourcing or outbox pattern — correct at this scale, but noted as a gap for production.

### 7. Containerization vs Run Script

Chose a **Dockerfile** (multi-stage: .NET 8 SDK build → ASP.NET 8 runtime). It produces a fully self-contained artifact a reviewer can run with two commands and no local .NET SDK, and is the more production-representative option. A run script was the alternative, but since all dependencies (worker, queue, storage) run in-process there is nothing extra to orchestrate, so a container is both sufficient and cleaner.

**Trade-off**: Slightly slower first run (image build) than `dotnet run`, and Swagger is gated to the Development environment so it is off by default in the container (documented env flag enables it).

### 8. SDK Pinning for Portability

`global.json` pins a floor of `8.0.100` with `rollForward: latestMajor`. This documents the .NET 8 target while still building on a machine that only has the .NET 9 SDK installed (as the development environment did), so neither an 8-only nor a 9-only reviewer is blocked.

### 9. CI

A single GitHub Actions workflow restores, builds in Release, and runs the tests on every push and on PRs to `main`. It installs the .NET 8 SDK only — `global.json` resolves to it, and the tests target `net8.0`.

---

## What Was Left Out (and Why)

| Concern | Decision |
|---|---|
| Authentication / authorization | Out of scope per the assignment |
| Rate limiting | Not required at stated throughput |
| Distributed tracing (OpenTelemetry) | Structured logging with `ILogger<T>` is sufficient for the scope |
| Integration tests | Explicitly excluded by the assignment |
| Real cloud deployment | Not required; stubs keep the design compatible |
| Outbox pattern | Overkill at this scale; noted as a production gap |

---

## If This Were Production

- Replace `InMemoryRepository` with Entity Framework Core + SQL Server / Cosmos DB.
- Replace `FileSystemStorageService` with Azure Blob Storage SDK (`BlobServiceClient`) or AWS S3 SDK.
- Replace `InMemoryQueuePublisher` with Azure Service Bus SDK (`ServiceBusSender`) or AWS SQS SDK (`AmazonSQSClient`).
- Move the background worker to a dedicated deployable (Azure Function / AWS Lambda / separate container).
- Add an outbox pattern to guarantee at-least-once delivery of queue messages even if the API restarts mid-transaction.
- Add distributed deduplication (Redis SETNX or idempotency keys on the message broker).
- Add OpenTelemetry for distributed tracing.
- Add health-check endpoints (`/healthz/live`, `/healthz/ready`) for container orchestration.
