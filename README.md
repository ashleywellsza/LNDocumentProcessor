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

The API will be available at `http://localhost:5000` (or as configured in `appsettings.Development.json`).

### Option 2 — Run Script

```powershell
# Windows
./run.ps1
```

```bash
# Linux / macOS
./run.sh
```

### Option 3 — Docker

```bash
docker build -t ln-document-processor .
docker run -p 5000:8080 ln-document-processor
```

---

## Configuration

All runtime configuration lives in `src/LNDocumentProcessor.Api/appsettings.Development.json`.

| Key | Default | Description |
|---|---|---|
| `Storage:Provider` | `FileSystem` | `FileSystem` for local runs; `AzureBlob` or `S3` for cloud |
| `Storage:BasePath` | `./local-storage` | Root path for file-system storage (local only) |
| `Queue:Provider` | `InMemory` | `InMemory` for local runs; `ServiceBus` or `SQS` for cloud |

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/documents` | Submit a new document |
| `GET` | `/documents/{documentId}` | Retrieve document metadata and status |
| `GET` | `/documents/{documentId}/content` | Download raw document content |
| `GET` | `/documents/{documentId}/preview` | Retrieve the generated preview |

Full request/response schemas are documented in the Swagger UI available at `http://localhost:5000/swagger` when running in Development mode.

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
