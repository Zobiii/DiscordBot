using Discord.WebSocket;
using DiscordBot.Core.Models;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for the main bot service that manages Discord client lifecycle
/// </summary>
public interface IBotService
{
    /// <summary>
    /// Gets the current Discord client instance
    /// </summary>
    DiscordSocketClient Client { get; }

    /// <summary>
    /// Gets the current bot status
    /// </summary>
    BotStatus Status { get; }

    /// <summary>
    /// Event fired when bot status changes
    /// </summary>
    event EventHandler<BotStatusChangedEventArgs> StatusChanged;

    /// <summary>
    /// Starts the bot service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the bot service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bot statistics
    /// </summary>
    /// <returns>Bot statistics</returns>
    Task<BotStatistics> GetStatisticsAsync();
}