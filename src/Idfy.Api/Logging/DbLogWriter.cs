using System.Threading.Channels;
using Idfy.Api.Data;

namespace Idfy.Api.Logging;

/// <summary>
/// Queues log entities so requests never wait on (or fail because of) the log database.
/// </summary>
public sealed class DbLogQueue(ILogger<DbLogQueue> logger)
{
    private readonly Channel<object> _channel = Channel.CreateBounded<object>(
        new BoundedChannelOptions(10_000) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public ChannelReader<object> Reader => _channel.Reader;

    public void Enqueue(ApiCallLog entry) => Write(entry);

    public void Enqueue(ErrorLog entry) => Write(entry);

    public void Enqueue(IdfyTaskLog entry) => Write(entry);

    public void Complete() => _channel.Writer.TryComplete();

    private void Write(object entry)
    {
        if (!_channel.Writer.TryWrite(entry))
            logger.LogWarning("DB log queue is full; dropped {EntryType}.", entry.GetType().Name);
    }
}

/// <summary>Drains <see cref="DbLogQueue"/> into SQL Server in batches.</summary>
public sealed class DbLogWriter(
    DbLogQueue queue,
    LogRepository repository,
    ILogger<DbLogWriter> logger) : BackgroundService
{
    private const int MaxBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<object>(MaxBatchSize);

        // Not bound to stoppingToken: on shutdown StopAsync completes the channel and we drain what's left.
        while (await queue.Reader.WaitToReadAsync(CancellationToken.None))
        {
            while (batch.Count < MaxBatchSize && queue.Reader.TryRead(out var entry))
                batch.Add(entry);

            try
            {
                await repository.InsertAsync(
                    batch.OfType<ApiCallLog>().ToList(),
                    batch.OfType<ErrorLog>().ToList(),
                    batch.OfType<IdfyTaskLog>().ToList(),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write {Count} log entries to the database.", batch.Count);
            }

            batch.Clear();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop accepting new entries and let ExecuteAsync drain what's queued (within the host
        // ShutdownTimeout). On an IIS app-pool recycle / iisreset this is our chance to flush.
        queue.Complete();
        await base.StopAsync(cancellationToken);

        if (queue.Reader.TryPeek(out _))
            logger.LogWarning("Shutdown drain did not finish; some log entries may not have been written.");
    }
}
