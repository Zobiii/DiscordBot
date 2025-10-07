using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.Configuration;

/// <summary>
/// Main configuration class for the Discord bot
/// </summary>
public class BotConfiguration
{
    /// <summary>
    /// Discord-specific configuration
    /// </summary>
    [Required]
    public DiscordConfiguration Discord { get; set; } = new();

    /// <summary>
    /// Logging configuration
    /// </summary>
    [Required]
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckConfiguration HealthChecks { get; set; } = new();

    /// <summary>
    /// Performance and resilience configuration
    /// </summary>
    public ResilienceConfiguration Resilience { get; set; } = new();
}

/// <summary>
/// Discord-specific configuration settings
/// </summary>
public class DiscordConfiguration
{
    /// <summary>
    /// Bot token (should be set via user secrets or environment variables)
    /// </summary>
    [Required(ErrorMessage = "Discord bot token is required")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Development guild ID for testing commands
    /// </summary>
    public ulong? DevGuildId { get; set; }

    /// <summary>
    /// Whether to register commands globally (production) or only to dev guild (development)
    /// </summary>
    public bool RegisterCommandsGlobally { get; set; } = false;

    /// <summary>
    /// Gateway intents configuration
    /// </summary>
    public IntentsConfiguration Intents { get; set; } = new();

    /// <summary>
    /// Client configuration options
    /// </summary>
    public ClientConfiguration Client { get; set; } = new();

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    [Range(1, 300, ErrorMessage = "Command timeout must be between 1 and 300 seconds")]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent command executions
    /// </summary>
    [Range(1, 100, ErrorMessage = "Max concurrent commands must be between 1 and 100")]
    public int MaxConcurrentCommands { get; set; } = 10;
}

/// <summary>
/// Gateway intents configuration
/// </summary>
public class IntentsConfiguration
{
    /// <summary>
    /// Whether to use all unprivileged intents
    /// </summary>
    public bool UseAllUnprivileged { get; set; } = true;

    /// <summary>
    /// Whether to use the guild members intent (privileged)
    /// </summary>
    public bool UseGuildMembers { get; set; } = false;

    /// <summary>
    /// Whether to use the message content intent (privileged)
    /// </summary>
    public bool UseMessageContent { get; set; } = false;

    /// <summary>
    /// Whether to use the presence update intent (privileged)
    /// </summary>
    public bool UsePresenceUpdate { get; set; } = false;
}

/// <summary>
/// Discord client configuration
/// </summary>
public class ClientConfiguration
{
    /// <summary>
    /// Message cache size
    /// </summary>
    [Range(0, 1000, ErrorMessage = "Message cache size must be between 0 and 1000")]
    public int MessageCacheSize { get; set; } = 100;

    /// <summary>
    /// Whether to always download users
    /// </summary>
    public bool AlwaysDownloadUsers { get; set; } = false;

    /// <summary>
    /// Whether to log gateway intent warnings
    /// </summary>
    public bool LogGatewayIntentWarnings { get; set; } = true;

    /// <summary>
    /// Default retry mode for requests
    /// </summary>
    public string RetryMode { get; set; } = "AlwaysRetry";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(1, 300, ErrorMessage = "Request timeout must be between 1 and 300 seconds")]
    public int RequestTimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Logging configuration
/// </summary>
public class LoggingConfiguration
{
    /// <summary>
    /// Minimum log level
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Whether to enable console logging
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Whether to enable file logging
    /// </summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>
    /// File logging configuration
    /// </summary>
    public FileLoggingConfiguration File { get; set; } = new();

    /// <summary>
    /// Console logging configuration
    /// </summary>
    public ConsoleLoggingConfiguration Console { get; set; } = new();
}

/// <summary>
/// File logging configuration
/// </summary>
public class FileLoggingConfiguration
{
    /// <summary>
    /// Path template for log files
    /// </summary>
    public string PathTemplate { get; set; } = "logs/bot-.txt";

    /// <summary>
    /// Rolling interval for log files
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Maximum number of log files to retain
    /// </summary>
    [Range(1, 365, ErrorMessage = "Retained file count limit must be between 1 and 365")]
    public int RetainedFileCountLimit { get; set; } = 30;

    /// <summary>
    /// File size limit in bytes
    /// </summary>
    public long? FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Whether to use compact JSON format
    /// </summary>
    public bool UseCompactFormat { get; set; } = false;
}

/// <summary>
/// Console logging configuration
/// </summary>
public class ConsoleLoggingConfiguration
{
    /// <summary>
    /// Whether to include timestamps
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Whether to use colors
    /// </summary>
    public bool UseColors { get; set; } = true;

    /// <summary>
    /// Output template for console logs
    /// </summary>
    public string OutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}";
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckConfiguration
{
    /// <summary>
    /// Whether health checks are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    [Range(10, 3600, ErrorMessage = "Health check interval must be between 10 and 3600 seconds")]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for individual health checks in seconds
    /// </summary>
    [Range(1, 60, ErrorMessage = "Health check timeout must be between 1 and 60 seconds")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum allowed memory usage in MB before marking as unhealthy
    /// </summary>
    [Range(100, 10000, ErrorMessage = "Memory threshold must be between 100 and 10000 MB")]
    public int MemoryThresholdMB { get; set; } = 512;
}

/// <summary>
/// Resilience and retry configuration
/// </summary>
public class ResilienceConfiguration
{
    /// <summary>
    /// Maximum retry attempts for operations
    /// </summary>
    [Range(1, 10, ErrorMessage = "Max retry attempts must be between 1 and 10")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries in milliseconds
    /// </summary>
    [Range(100, 30000, ErrorMessage = "Retry delay must be between 100 and 30000 milliseconds")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Whether circuit breaker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of failures before opening the circuit
    /// </summary>
    [Range(1, 100, ErrorMessage = "Failure threshold must be between 1 and 100")]
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Sampling duration in seconds
    /// </summary>
    [Range(10, 300, ErrorMessage = "Sampling duration must be between 10 and 300 seconds")]
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Break duration in seconds (how long to keep circuit open)
    /// </summary>
    [Range(10, 600, ErrorMessage = "Break duration must be between 10 and 600 seconds")]
    public int BreakDurationSeconds { get; set; } = 30;
}