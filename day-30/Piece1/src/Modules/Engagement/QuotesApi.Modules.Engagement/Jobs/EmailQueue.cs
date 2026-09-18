using System.Threading.Channels;

namespace QuotesApi.Modules.Engagement.Jobs;

public record EmailRequest(string Message);

public class EmailQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ValueTask EnqueueAsync(string message) =>
        _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
