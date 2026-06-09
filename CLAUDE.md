# LNDocumentProcessor — Project Intelligence

## Assignment Overview

Take-home assignment for **LexisNexis**. Build a production-minded backend service that ingests legal documents from external providers, stores them in cloud storage, processes them in the background to produce a short preview, and exposes them for retrieval by downstream systems.

---

## Core Functional Requirements

1. **Document Intake** — Accept document submissions (multipart or JSON body) from upstream providers via an HTTP API.
2. **Cloud Storage** — Persist raw document content to object storage (S3 or Azure Blob).
3. **Background Processing** — Queue a message after successful intake; a background worker consumes it and generates a short preview/summary.
4. **Retrieval** — Expose endpoints to retrieve document metadata, content, and processing status.

---

## Technology Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 8 / C# |
| API | ASP.NET Core Web API (Minimal APIs or Controllers) |
| Background worker | .NET `BackgroundService` / `IHostedService` (in-process) |
| Cloud path chosen | **Azure** — Azure Blob Storage + Azure Service Bus |
| Local emulation | Azurite (Blob + Queue local emulator) or in-memory stubs |
| Queue (local) | In-memory `Channel<T>` acceptable for local runs |
| Tests | xUnit, minimal (1–2 unit tests only) |
| CI | GitHub Actions |
| Containerization | Docker (Dockerfile) OR a run script — choose one |

> **Decision point**: The cloud path (AWS vs Azure) is not yet decided. Update this file once chosen.

---

## Design Constraints

- Must run **locally without a real cloud account** — use Azurite, LocalStack, or in-memory/file-system stubs while keeping the architecture compatible with the real cloud services.
- An **in-process background worker** with an **in-memory queue** is acceptable for local runs.
- **Deduplication key**: `provider + sourceDocumentId` — if the same document is submitted more than once, do not create a duplicate record; return the existing one.
- **Document size**: up to 5 MB.
- **Throughput**: tens of submissions per minute (not high-volume; correctness over optimisation).
- **Audit trail**: lightweight but meaningful — record timestamps for: `received`, `stored`, `queued`, `processed`, `failed`.

---

## Data Schemas (Illustrative — Final Schemas TBD)

### Submission Payload (inbound from provider)
```
sourceDocumentId   string   Provider's own document ID
provider           string   Provider identifier
title              string
jurisdiction       string
categories         string[]
tags               string[]
receivedAt         datetime
contentType        string   e.g. application/pdf
fileName           string
[file content]     binary   Raw document bytes
```

### Background Processing Message (intake → worker)
```
documentId         guid     Internal ID assigned at intake
sourceDocumentId   string
action             string   e.g. "generate-preview"
submittedAt        datetime
```

### Status / Retrieval Response
```
documentId         guid
sourceDocumentId   string
status             enum     received | stored | queued | processed | failed
timestamp          datetime Last status change
previewSizeBytes   int?     Set after processing
```

---

## Architecture (Planned)

```
Upstream Provider
     │  POST /documents
     ▼
┌─────────────────────────────────┐
│         ASP.NET Core API        │
│  DocumentsController / Endpoint │
│  - Validate & deduplicate       │
│  - Assign internal documentId   │
│  - Store to Blob Storage        │
│  - Publish to queue             │
│  - Persist metadata + status    │
└────────────┬────────────────────┘
             │ queue message
             ▼
┌─────────────────────────────────┐
│    BackgroundService (Worker)   │
│  - Consume queue message        │
│  - Read blob content            │
│  - Generate short preview       │
│  - Update status → processed    │
└─────────────────────────────────┘
             │
             ▼
     Document metadata / status
     retrievable via GET endpoints
```

---

## Project Structure (Target)

```
LNDocumentProcessor/
├── src/
│   ├── LNDocumentProcessor.Api/          # ASP.NET Core host, endpoints, DI wiring
│   ├── LNDocumentProcessor.Application/  # Use cases, interfaces, DTOs
│   ├── LNDocumentProcessor.Domain/       # Entities, enums, value objects (Document, AuditEntry)
│   └── LNDocumentProcessor.Infrastructure/  # Storage, queue, repository implementations
├── tests/
│   └── LNDocumentProcessor.Tests/        # xUnit — 1-2 unit tests
├── .github/
│   └── workflows/
│       └── ci.yml                        # Build + test GitHub Actions workflow
├── Dockerfile  OR  run.sh/run.ps1
├── .gitignore
├── README.md
└── CLAUDE.md
```

---

## Development Priorities

1. Get the solution skeleton compiling and the API running locally.
2. Implement document intake with deduplication (in-memory or SQLite for metadata store).
3. Wire up storage (file-system stub first, Azurite-compatible abstraction).
4. Implement the background worker and queue (in-memory `Channel<T>` locally).
5. Implement preview generation (simple: first N characters or page-count stub).
6. Add retrieval endpoints.
7. Add 1–2 unit tests.
8. Write Dockerfile or run script.
9. Write GitHub Actions CI workflow.
10. Polish README with local run instructions.

---

## Best Practices to Follow

- **Dependency inversion** — infrastructure implements application-layer interfaces; the domain knows nothing about cloud SDKs.
- **Options pattern** — all configuration (storage connection strings, queue names) via `IOptions<T>` and `appsettings.json`.
- **Cancellation tokens** — all async paths accept `CancellationToken`.
- **Structured logging** — use `ILogger<T>` throughout; log at appropriate levels.
- **No global state** — background worker communicates via injected channel/queue abstraction.
- **Idempotent intake** — deduplication check before any side effects.
- **Status machine** — document status transitions are explicit and audited.
