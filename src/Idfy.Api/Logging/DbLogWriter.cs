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

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        return base.StopAsync(cancellationToken);
    }
}
