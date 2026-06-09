using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Documents;

namespace LNDocumentProcessor.Api.Endpoints;

/// <summary>
/// HTTP surface for Task 1: submit a document, retrieve its metadata/status,
/// and download its raw content.
/// </summary>
public static class DocumentEndpoints
{
    /// <summary>Maximum accepted document size (5 MB per the assignment scope).</summary>
    public const long MaxContentBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/documents").WithTags("Documents");

        group.MapPost("/", SubmitAsync)
            .WithName("SubmitDocument")
            .WithSummary("Submit a document (multipart/form-data: metadata fields + file).")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentResponse>(StatusCodes.Status201Created)
            .Produces<DocumentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetDocument")
            .WithSummary("Retrieve document metadata, status, and audit trail.")
            .Produces<DocumentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/content", GetContentAsync)
            .WithName("GetDocumentContent")
            .WithSummary("Download the raw stored content for a document.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SubmitAsync(
        HttpRequest request,
        SubmitDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                "Request must be multipart/form-data.", statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        var errors = new Dictionary<string, string[]>();
        string Required(string field)
        {
            var value = form[field].ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                errors[field] = ["The field is required."];
            }
            return value;
        }

        var sourceDocumentId = Required("sourceDocumentId");
        var provider = Required("provider");
        var title = Required("title");

        if (file is null || file.Length == 0)
        {
            errors["file"] = ["A non-empty file is required."];
        }
        else if (file.Length > MaxContentBytes)
        {
            errors["file"] = [$"File exceeds the maximum size of {MaxContentBytes} bytes."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var contentType = !string.IsNullOrWhiteSpace(form["contentType"])
            ? form["contentType"].ToString()
            : (file!.ContentType ?? "application/octet-stream");

        var command = new SubmitDocumentCommand(
            SourceDocumentId: sourceDocumentId,
            Provider: provider,
            Title: title,
            Jurisdiction: NullIfEmpty(form["jurisdiction"].ToString()),
            Categories: SplitCsv(form["categories"].ToString()),
            Tags: SplitCsv(form["tags"].ToString()),
            ContentType: contentType,
            FileName: !string.IsNullOrWhiteSpace(form["fileName"])
                ? form["fileName"].ToString()
                : file!.FileName,
            Content: file!.OpenReadStream());

        var result = await handler.HandleAsync(command, cancellationToken);
        var response = DocumentResponse.FromDomain(result.Document);

        // Idempotent: a duplicate returns 200 with the existing record;
        // a fresh submission returns 201 with a Location header.
        return result.IsDuplicate
            ? Results.Ok(response)
            : Results.Created($"/documents/{response.DocumentId}", response);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, IDocumentRepository repository, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.Ok(DocumentResponse.FromDomain(document));
    }

    private static async Task<IResult> GetContentAsync(
        Guid id,
        IDocumentRepository repository,
        IStorageService storage,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document?.BlobReference is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(document.BlobReference, cancellationToken);
        return stream is null
            ? Results.NotFound()
            : Results.File(stream, document.Metadata.ContentType, document.Metadata.FileName);
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> SplitCsv(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
