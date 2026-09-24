using Idfy.Api.Data;
using Idfy.Api.Options;
using Microsoft.Extensions.Options;

namespace Idfy.Api.Logging;

/// <summary>Periodically deletes log rows older than the retention window, so the tables stay bounded.</summary>
public sealed class LogRetentionService(
    LogRepository repository,
    IOptions<LogRetentionOptions> options,
    ILogger<LogRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (config.RetentionDays <= 0)
        {
            logger.LogInformation("Log retention disabled (RetentionDays <= 0).");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, config.SweepIntervalHours));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(config.RetentionDays);
                var deleted = await repository.DeleteOlderThanAsync(cutoff, config.BatchSize, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Log retention deleted {Count} rows older than {Cutoff:u}.", deleted, cutoff);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Log retention sweep failed; will retry next interval.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
