using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Idfy.Api.Data;

/// <summary>Reports the log database as healthy when a trivial query succeeds.</summary>
public sealed class LogDbHealthCheck(LogRepository repository) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await repository.PingAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Log database is not reachable.", ex);
        }
    }
}
