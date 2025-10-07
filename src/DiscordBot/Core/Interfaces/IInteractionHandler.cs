using Discord.Interactions;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for handling Discord interactions (slash commands, context menus, etc.)
/// </summary>
public interface IInteractionHandler
{
    /// <summary>
    /// Gets the interaction service instance
    /// </summary>
    InteractionService InteractionService { get; }

    /// <summary>
    /// Initializes the interaction handler
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers commands globally or to specific guilds
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task RegisterCommandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command statistics
    /// </summary>
    /// <returns>Dictionary of command names and their usage counts</returns>
    Task<Dictionary<string, int>> GetCommandStatisticsAsync();
}