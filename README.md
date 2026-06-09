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

> A containerized run path (Dockerfile) or a run script will be added as a later deliverable; for now use the .NET CLI above.

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

The `POST /documents` form accepts: `sourceDocumentId` (required), `provider` (required), `title` (required), `jurisdiction`, `categories` (comma-separated), `tags` (comma-separated), `contentType`, `fileName`, and `file` (the document, ≤ 5 MB).

Full request/response schemas are documented in the Swagger UI (see the startup URL) when running in Development mode.

### Example

```bash
curl -X POST http://localhost:5080/documents \
  -F "sourceDocumentId=SRC-1001" -F "provider=acme-legal" -F "title=Sample Brief" \
  -F "jurisdiction=ZA" -F "categories=filing,brief" -F "tags=urgent,q2" \
  -F "file=@sample.txt;type=text/plain"
```

---

## Running Tests

```bash
dotnet test
```

---

## Project Structure

```
LNDocumentProcessor/
├── src/
│   ├── LNDocumentProcessor.Api/           # ASP.NET Core host, endpoints, DI wiring
│   ├── LNDocumentProcessor.Application/   # Use cases, interfaces, DTOs
│   ├── LNDocumentProcessor.Domain/        # Entities, enums, value objects
│   └── LNDocumentProcessor.Infrastructure/# Storage, queue, and repository implementations
├── tests/
│   └── LNDocumentProcessor.Tests/         # xUnit unit tests
├── .github/
│   └── workflows/
│       └── ci.yml                         # GitHub Actions — build and test
├── Dockerfile
├── run.ps1 / run.sh
├── README.md
└── SOLUTION.md
```

---

## CI

A GitHub Actions workflow at [`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs `dotnet build` and `dotnet test` on every push and pull request to `main`.
