using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Documents;
using LNDocumentProcessor.Api.Infrastructure.Persistence;
using LNDocumentProcessor.Api.Infrastructure.Storage;

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

        // Metadata store — in-memory, shared for the process lifetime.
        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();

        // Object storage — local file-system substitute.
        services.Configure<FileSystemStorageOptions>(
            configuration.GetSection(FileSystemStorageOptions.SectionName));
        services.AddSingleton<IStorageService, FileSystemStorageService>();

        return services;
    }
}
