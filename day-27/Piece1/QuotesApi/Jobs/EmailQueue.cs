using System.Threading.Channels;

namespace QuotesApi.Jobs;

public record EmailRequest(string Message);

// Registered as a singleton so both the API endpoint (producer)
// and EmailWorker (consumer) share the same underlying channel.
public class EmailQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ValueTask EnqueueAsync(string message) =>
        _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
