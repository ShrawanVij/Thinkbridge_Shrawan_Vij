namespace QuotesApi.Jobs;

public class EmailWorker : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly ILogger<EmailWorker> _logger;

    public EmailWorker(EmailQueue queue, ILogger<EmailWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailWorker started");

        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await Task.Delay(2000, stoppingToken);
                    _logger.LogInformation("Processed queued email: {Message}", message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to process queued email: {Message}", message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stoppingToken is cancelled during shutdown.
        }

        _logger.LogInformation("EmailWorker stopped");
    }
}
