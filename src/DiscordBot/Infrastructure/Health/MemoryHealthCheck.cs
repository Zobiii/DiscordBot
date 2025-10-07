using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DiscordBot.Infrastructure.Health;

/// <summary>
/// Health check for memory usage
/// </summary>
public sealed class MemoryHealthCheck : IHealthCheck
{
    private readonly HealthCheckConfiguration _config;
    private readonly ILogger<MemoryHealthCheck> _logger;

    public MemoryHealthCheck(IOptions<BotConfiguration> config, ILogger<MemoryHealthCheck> logger)
    {
        _config = config?.Value?.HealthChecks ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var memoryUsageBytes = process.WorkingSet64;
            var memoryUsageMB = memoryUsageBytes / 1024.0 / 1024.0;
            var thresholdMB = _config.MemoryThresholdMB;

            var data = new Dictionary<string, object>
            {
                ["memoryUsageMB"] = Math.Round(memoryUsageMB, 2),
                ["thresholdMB"] = thresholdMB,
                ["memoryUsageBytes"] = memoryUsageBytes,
                ["gcTotalMemory"] = GC.GetTotalMemory(false),
                ["gen0Collections"] = GC.CollectionCount(0),
                ["gen1Collections"] = GC.CollectionCount(1),
                ["gen2Collections"] = GC.CollectionCount(2)
            };

            if (memoryUsageMB > thresholdMB)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Memory usage ({memoryUsageMB:N1}MB) exceeds threshold ({thresholdMB}MB)", 
                    null, data));
            }

            // Warn if memory usage is above 80% of threshold
            var warningThreshold = thresholdMB * 0.8;
            if (memoryUsageMB > warningThreshold)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage ({memoryUsageMB:N1}MB) is approaching threshold ({thresholdMB}MB)", 
                    null, data));
            }

            return Task.FromResult(HealthCheckResult.Healthy($"Memory usage is normal ({memoryUsageMB:N1}MB)", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during memory health check");
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to check memory usage", ex));
        }
    }
}