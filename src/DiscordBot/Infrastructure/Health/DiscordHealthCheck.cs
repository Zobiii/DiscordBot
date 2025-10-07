using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Health;

/// <summary>
/// Health check for Discord connection status
/// </summary>
public sealed class DiscordHealthCheck : IHealthCheck
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordHealthCheck> _logger;

    public DiscordHealthCheck(DiscordSocketClient client, ILogger<DiscordHealthCheck> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionState = _client.ConnectionState;
            var latency = _client.Latency;
            var loginState = _client.LoginState;

            var data = new Dictionary<string, object>
            {
                ["connectionState"] = connectionState.ToString(),
                ["loginState"] = loginState.ToString(),
                ["latency"] = $"{latency}ms",
                ["guildCount"] = _client.Guilds?.Count ?? 0,
                ["shardId"] = _client.ShardId,
                ["currentUser"] = _client.CurrentUser?.Username ?? "Unknown"
            };

            // Check connection state
            if (connectionState == ConnectionState.Connected && loginState == LoginState.LoggedIn)
            {
                // Check latency - if it's too high, mark as degraded
                if (latency > 1000) // 1 second
                {
                    return HealthCheckResult.Degraded($"Discord connection is slow (latency: {latency}ms)", data);
                }
                
                return HealthCheckResult.Healthy("Discord connection is healthy", data);
            }
            
            if (connectionState == ConnectionState.Connecting)
            {
                return HealthCheckResult.Degraded("Discord client is connecting", data);
            }
            
            if (connectionState == ConnectionState.Disconnecting)
            {
                return HealthCheckResult.Degraded("Discord client is disconnecting", data);
            }

            return HealthCheckResult.Unhealthy($"Discord connection is unhealthy (State: {connectionState}, Login: {loginState})", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Discord health check");
            return HealthCheckResult.Unhealthy("Failed to check Discord health", ex);
        }
    }
}