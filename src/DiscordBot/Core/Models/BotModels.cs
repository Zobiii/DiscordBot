namespace DiscordBot.Core.Models;

/// <summary>
/// Represents the current status of the bot
/// </summary>
public enum BotStatus
{
    /// <summary>
    /// Bot is stopped/offline
    /// </summary>
    Stopped,

    /// <summary>
    /// Bot is starting up
    /// </summary>
    Starting,

    /// <summary>
    /// Bot is running and connected
    /// </summary>
    Running,

    /// <summary>
    /// Bot is stopping
    /// </summary>
    Stopping,

    /// <summary>
    /// Bot encountered an error
    /// </summary>
    Error
}

/// <summary>
/// Event arguments for bot status changes
/// </summary>
public class BotStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Previous status
    /// </summary>
    public BotStatus PreviousStatus { get; init; }

    /// <summary>
    /// New status
    /// </summary>
    public BotStatus NewStatus { get; init; }

    /// <summary>
    /// Optional message describing the status change
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Exception that caused the status change (if applicable)
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// Statistics about the bot's operation
/// </summary>
public class BotStatistics
{
    /// <summary>
    /// When the bot was started
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Current uptime
    /// </summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAt;

    /// <summary>
    /// Number of guilds the bot is in
    /// </summary>
    public int GuildCount { get; init; }

    /// <summary>
    /// Total number of users the bot can see
    /// </summary>
    public int UserCount { get; init; }

    /// <summary>
    /// Number of commands executed
    /// </summary>
    public long CommandsExecuted { get; init; }

    /// <summary>
    /// Current gateway latency in milliseconds
    /// </summary>
    public int GatewayLatency { get; init; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; init; }

    /// <summary>
    /// Bot version
    /// </summary>
    public string Version { get; init; } = "0.0.1";
}

/// <summary>
/// Represents a command execution result
/// </summary>
public class CommandExecutionResult
{
    /// <summary>
    /// Whether the command executed successfully
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the command failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that occurred during execution (if any)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Time taken to execute the command
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Command that was executed
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// User who executed the command
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Guild where the command was executed (null for DMs)
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// When the command was executed
    /// </summary>
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static CommandExecutionResult Success(string commandName, ulong userId, ulong? guildId = null, TimeSpan? executionTime = null) => new()
    {
        IsSuccess = true,
        CommandName = commandName,
        UserId = userId,
        GuildId = guildId,
        ExecutionTime = executionTime ?? TimeSpan.Zero
    };

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static CommandExecutionResult Failure(string commandName, ulong userId, string errorMessage, Exception? exception = null, ulong? guildId = null, TimeSpan? executionTime = null) => new()
    {
        IsSuccess = false,
        CommandName = commandName,
        UserId = userId,
        GuildId = guildId,
        ErrorMessage = errorMessage,
        Exception = exception,
        ExecutionTime = executionTime ?? TimeSpan.Zero
    };
}