namespace LNDocumentProcessor.Api.Infrastructure.Storage;

/// <summary>Options for the local file-system storage implementation.</summary>
public sealed class FileSystemStorageOptions
{
    public const string SectionName = "Storage:FileSystem";

    /// <summary>Root directory under which document content is written.</summary>
    public string BasePath { get; set; } = "./local-storage";
}
