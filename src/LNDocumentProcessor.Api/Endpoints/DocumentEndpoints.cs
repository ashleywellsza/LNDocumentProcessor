using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Documents;
using Microsoft.AspNetCore.Mvc;

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

        group.MapGet("/{id:guid}/status", GetStatusAsync)
            .WithName("GetDocumentStatus")
            .WithSummary("Check processing status for a document.")
            .Produces<DocumentStatusResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/preview", GetPreviewAsync)
            .WithName("GetDocumentPreview")
            .WithSummary("Retrieve the generated preview/summary (available once processed).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    // Explicit [FromForm] parameters so the multipart schema is described in
    // OpenAPI and the Swagger UI renders a field for each one plus a file picker.
    private static async Task<IResult> SubmitAsync(
        [FromForm] string? sourceDocumentId,
        [FromForm] string? provider,
        [FromForm] string? title,
        [FromForm] string? jurisdiction,
        [FromForm] string? categories,
        [FromForm] string? tags,
        [FromForm] string? contentType,
        [FromForm] string? fileName,
        IFormFile? file,
        SubmitDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(sourceDocumentId))
        {
            errors[nameof(sourceDocumentId)] = ["The field is required."];
        }
        if (string.IsNullOrWhiteSpace(provider))
        {
            errors[nameof(provider)] = ["The field is required."];
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            errors[nameof(title)] = ["The field is required."];
        }

        if (file is null || file.Length == 0)
        {
            errors[nameof(file)] = ["A non-empty file is required."];
        }
        else if (file.Length > MaxContentBytes)
        {
            errors[nameof(file)] = [$"File exceeds the maximum size of {MaxContentBytes} bytes."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var resolvedContentType = !string.IsNullOrWhiteSpace(contentType)
            ? contentType!
            : (file!.ContentType ?? "application/octet-stream");

        var command = new SubmitDocumentCommand(
            SourceDocumentId: sourceDocumentId!,
            Provider: provider!,
            Title: title!,
            Jurisdiction: NullIfEmpty(jurisdiction ?? string.Empty),
            Categories: SplitCsv(categories ?? string.Empty),
            Tags: SplitCsv(tags ?? string.Empty),
            ContentType: resolvedContentType,
            FileName: !string.IsNullOrWhiteSpace(fileName) ? fileName! : file!.FileName,
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

    private static async Task<IResult> GetStatusAsync(
        Guid id, IDocumentRepository repository, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.Ok(DocumentStatusResponse.FromDomain(document));
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid id,
        IDocumentRepository repository,
        IStorageService storage,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        // Preview only exists once processing has completed.
        if (document.PreviewReference is null)
        {
            return Results.Problem(
                $"Preview not available; document status is '{document.Status}'.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var stream = await storage.OpenReadAsync(document.PreviewReference, cancellationToken);
        return stream is null
            ? Results.NotFound()
            : Results.File(stream, "text/plain; charset=utf-8", $"{document.Id}-preview.txt");
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> SplitCsv(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
