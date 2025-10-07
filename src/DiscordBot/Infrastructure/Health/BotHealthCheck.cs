using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Health;

/// <summary>
/// Health check for the main bot service
/// </summary>
public sealed class BotHealthCheck : IHealthCheck
{
    private readonly IBotService _botService;
    private readonly ILogger<BotHealthCheck> _logger;

    public BotHealthCheck(IBotService botService, ILogger<BotHealthCheck> logger)
    {
        _botService = botService ?? throw new ArgumentNullException(nameof(botService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = _botService.Status;
            var statistics = await _botService.GetStatisticsAsync();

            var data = new Dictionary<string, object>
            {
                ["status"] = status.ToString(),
                ["uptime"] = statistics.Uptime.ToString(@"dd\.hh\:mm\:ss"),
                ["guildCount"] = statistics.GuildCount,
                ["userCount"] = statistics.UserCount,
                ["commandsExecuted"] = statistics.CommandsExecuted,
                ["gatewayLatency"] = $"{statistics.GatewayLatency}ms",
                ["memoryUsage"] = $"{statistics.MemoryUsage / 1024 / 1024:N0}MB",
                ["version"] = statistics.Version
            };

            return status switch
            {
                BotStatus.Running => HealthCheckResult.Healthy("Bot is running normally", data),
                BotStatus.Starting => HealthCheckResult.Degraded("Bot is starting up", data),
                BotStatus.Stopping => HealthCheckResult.Degraded("Bot is shutting down", data),
                BotStatus.Stopped => HealthCheckResult.Unhealthy("Bot is stopped", data),
                BotStatus.Error => HealthCheckResult.Unhealthy("Bot is in error state", data),
                _ => HealthCheckResult.Unhealthy("Bot is in unknown state", data)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during bot health check");
            return HealthCheckResult.Unhealthy("Failed to check bot health", ex);
        }
    }
}