using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Documents;
using LNDocumentProcessor.Api.Application.Processing;
using LNDocumentProcessor.Api.Infrastructure.Notifications;
using LNDocumentProcessor.Api.Infrastructure.Persistence;
using LNDocumentProcessor.Api.Infrastructure.Processing;
using LNDocumentProcessor.Api.Infrastructure.Queue;
using LNDocumentProcessor.Api.Infrastructure.Storage;
using LNDocumentProcessor.Api.Worker;

namespace LNDocumentProcessor.Api;

/// <summary>
/// Composition root. Registers the application use cases and the local
/// (file-system + in-memory) infrastructure implementations. Swapping to a
/// cloud provider is a change confined to this file plus the new implementation.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDocumentProcessing(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Testable clock.
        services.AddSingleton(TimeProvider.System);

        // Use cases.
        services.AddScoped<SubmitDocumentHandler>();
        services.AddScoped<DocumentProcessor>();

        // Metadata store — in-memory, shared for the process lifetime.
        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();

        // Object storage — local file-system substitute.
        services.Configure<FileSystemStorageOptions>(
            configuration.GetSection(FileSystemStorageOptions.SectionName));
        services.AddSingleton<IStorageService, FileSystemStorageService>();

        // Background processing — in-memory queue (single shared channel),
        // preview generator, external status-emit stub, and the hosted worker.
        services.AddSingleton<InMemoryProcessingQueue>();
        services.AddSingleton<IDocumentProcessingQueue>(sp => sp.GetRequiredService<InMemoryProcessingQueue>());
        services.AddSingleton<IPreviewGenerator, ExcerptPreviewGenerator>();
        services.AddSingleton<IStatusNotifier, LoggingStatusNotifier>();
        services.AddHostedService<DocumentProcessingWorker>();

        return services;
    }
}
