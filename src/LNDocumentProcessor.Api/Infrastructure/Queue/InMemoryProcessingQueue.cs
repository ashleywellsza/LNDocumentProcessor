using System.Threading.Channels;
using LNDocumentProcessor.Api.Application.Abstractions;
using LNDocumentProcessor.Api.Application.Processing;

namespace LNDocumentProcessor.Api.Infrastructure.Queue;

/// <summary>
/// In-process queue backed by an unbounded <see cref="Channel{T}"/>. Registered
/// as a singleton so the producer (intake) and consumer (worker) share one
/// channel. Compatible in shape with a real broker behind
/// <see cref="IDocumentProcessingQueue"/>.
/// </summary>
public sealed class InMemoryProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<DocumentProcessingMessage> _channel =
        Channel.CreateUnbounded<DocumentProcessingMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(DocumentProcessingMessage message, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<DocumentProcessingMessage> DequeueAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
