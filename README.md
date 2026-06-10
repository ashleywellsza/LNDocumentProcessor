# LNDocumentProcessor

A backend service for ingesting, storing, and processing legal documents at LexisNexis. Documents are accepted via HTTP, persisted to cloud-compatible object storage, processed asynchronously in the background to produce a short preview, and exposed for retrieval by downstream systems.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 8.x |
| Docker (optional) | 24+ |

> No cloud account is required. The service runs locally using in-memory/file-system stubs that are compatible with the chosen cloud provider's SDK interfaces.

---

## Running Locally

### Option 1 — .NET CLI

```bash
# 1. Restore dependencies
dotnet restore

# 2. Build the solution
dotnet build

# 3. Run the API
dotnet run --project src/LNDocumentProcessor.Api
```

The API and its Swagger UI will be available at the URL printed on startup (e.g. `http://localhost:5080/swagger`). Override the port with `--urls`:

```bash
dotnet run --project src/LNDocumentProcessor.Api --urls http://localhost:5080
```

### Option 2 — Docker

A multi-stage [Dockerfile](Dockerfile) builds and runs the service with no local .NET SDK required:

```bash
docker build -t ln-document-processor .
docker run --rm -p 8080:8080 ln-document-processor
```

The API is then available at `http://localhost:8080`. To enable the Swagger UI in the container, run it in the Development environment:

```bash
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development ln-document-processor
# Swagger UI: http://localhost:8080/swagger
```

> All dependencies (background worker, queue, storage) run in-process, so there is nothing else to start.

---

## Test it in the browser (Web console)

The service ships with a built-in web console for submitting and tracking documents — no separate frontend to install or run. It is a static page served by the API itself, so it is available as soon as the service is running.

**1. Start the service** (either option above), e.g.:

```bash
dotnet run --project src/LNDocumentProcessor.Api --urls http://localhost:5080
```

**2. Open the web console** at the site root:

```
http://localhost:5080
```

(With Docker, use `http://localhost:8080`.)

**3. Submit a document:**

- Pick a **Provider** from the dropdown (e.g. *Acme Legal*).
- Enter a **Source document ID** (any value, e.g. `SRC-1001`) and a **Title**.
- Optionally set jurisdiction, categories, and tags (comma-separated).
- **Drag & drop** a file onto the upload area, or click to browse. A ready-made sample, [`Acme Legal.txt`](Acme%20Legal.txt), is included in the repo root — use it if you don't have a document handy.
- Click **Submit document**.

**4. Watch it process.** The console shows the document card and automatically polls the status, animating it through `Received → Stored → Queued → Processing → Processed`. When complete it displays the generated **preview**, the **audit trail** timeline, and a link to download the raw content.

**5. Try the extras:**

- **Submit again (test dedup)** — re-submits the same `provider + sourceDocumentId`; the console shows a *“Duplicate detected — HTTP 200, not re-queued”* banner, demonstrating idempotent intake.
- **Look up a document** — paste any document ID (GUID) to fetch its current status and preview.

> Prefer raw HTTP? The same operations are available via the Swagger UI (`/swagger`) or `curl` — see [API Endpoints](#api-endpoints) below.

---

## Configuration

All runtime configuration lives in `src/LNDocumentProcessor.Api/appsettings.json`.

| Key | Default | Description |
|---|---|---|
| `Storage:FileSystem:BasePath` | `./local-storage` | Root path for the local file-system storage substitute |

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/documents` | Submit a document (`multipart/form-data`: metadata fields + `file`). Returns **201** for a new document, or **200** if the same `provider + sourceDocumentId` was already submitted (idempotent). |
| `GET` | `/documents/{id}` | Retrieve document metadata, status, and audit trail |
| `GET` | `/documents/{id}/content` | Download the raw stored content |
| `GET` | `/documents/{id}/status` | Check processing status (status, last timestamp, preview size, failure reason) |
| `GET` | `/documents/{id}/preview` | Retrieve the generated preview/summary (once processed) |

The `POST /documents` form accepts: `sourceDocumentId` (required), `provider` (required), `title` (required), `jurisdiction`, `categories` (comma-separated), `tags` (comma-separated), `contentType`, `fileName`, and `file` (the document, ≤ 5 MB).

Full request/response schemas are documented in the Swagger UI (see the startup URL) when running in Development mode.

### Example flow

```bash
# 1. Submit a document (returns 201 with a documentId; status starts as "Queued")
curl -X POST http://localhost:5080/documents \
  -F "sourceDocumentId=SRC-1001" -F "provider=acme-legal" -F "title=Sample Brief" \
  -F "jurisdiction=ZA" -F "categories=filing,brief" -F "tags=urgent,q2" \
  -F "file=@sample.txt;type=text/plain"

# 2. The in-process worker generates a preview almost immediately. Check status:
curl http://localhost:5080/documents/{documentId}/status   # -> "Processed", previewSizeBytes

# 3. Retrieve the generated preview:
curl http://localhost:5080/documents/{documentId}/preview
```

Re-submitting the same `provider + sourceDocumentId` returns the existing record (HTTP 200) and is **not** re-queued.

---

## Running Tests

```bash
dotnet test
```

---

## Project Structure

The solution is one API project with clear internal layer folders (kept lightweight rather than split into four projects), plus a test project:

```
LNDocumentProcessor/
├── src/
│   └── LNDocumentProcessor.Api/
│       ├── Domain/            # Document aggregate, AuditEntry, DocumentStatus
│       ├── Application/       # Ports (abstractions), use cases, DTOs
│       │   ├── Abstractions/  # IStorageService, IDocumentRepository, IDocumentProcessingQueue, ...
│       │   ├── Documents/     # SubmitDocumentHandler, responses
│       │   └── Processing/    # DocumentProcessor, DocumentProcessingMessage
│       ├── Infrastructure/    # File-system storage, in-memory repo + queue, preview, notifier
│       ├── Endpoints/         # Minimal-API endpoint mapping
│       ├── Worker/            # DocumentProcessingWorker (BackgroundService)
│       └── DependencyInjection.cs  # Composition root
├── tests/
│   └── LNDocumentProcessor.Tests/   # xUnit unit tests
├── .github/workflows/ci.yml         # GitHub Actions — build and test
├── Dockerfile
├── README.md
└── SOLUTION.md
```

---

## CI

A GitHub Actions workflow at [`.github/workflows/ci.yml`](.github/workflows/ci.yml) restores, builds (Release), and runs the tests on every push to `development` and on pull requests targeting `development`.
