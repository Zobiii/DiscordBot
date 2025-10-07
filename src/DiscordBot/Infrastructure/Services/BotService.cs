using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Diagnostics;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Main bot service that manages Discord client lifecycle and statistics
/// </summary>
public sealed class BotService : IBotService, IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfiguration _config;
    private readonly ILogger<BotService> _logger;
    private readonly IAsyncPolicy? _retryPolicy;

    private BotStatus _status = BotStatus.Stopped;
    private readonly DateTimeOffset _startedAt;
    private long _commandsExecuted = 0;
    private volatile bool _disposed = false;

    public DiscordSocketClient Client => _client;
    public BotStatus Status => _status;

    public event EventHandler<BotStatusChangedEventArgs>? StatusChanged;

    public BotService(
        DiscordSocketClient client,
        IOptions<BotConfiguration> config,
        ILogger<BotService> logger,
        IAsyncPolicy? retryPolicy = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy;
        _startedAt = DateTimeOffset.UtcNow;

        // Subscribe to Discord client events
        _client.Log += OnClientLog;
        _client.Ready += OnClientReady;
        _client.Disconnected += OnClientDisconnected;
        _client.Connected += OnClientConnected;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_status != BotStatus.Stopped)
        {
            _logger.LogWarning("Bot service is already starting or running (current status: {Status})", _status);
            return;
        }

        await ChangeStatusAsync(BotStatus.Starting, "Starting bot service...");

        try
        {
            var loginAndStart = async () =>
            {
                _logger.LogInformation("Logging in to Discord...");
                await _client.LoginAsync(TokenType.Bot, _config.Discord.Token);
                
                _logger.LogInformation("Starting Discord client...");
                await _client.StartAsync();
            };

            if (_retryPolicy != null)
            {
                await _retryPolicy.ExecuteAsync(loginAndStart);
            }
            else
            {
                await loginAndStart();
            }

            _logger.LogInformation("Bot service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start bot service");
            await ChangeStatusAsync(BotStatus.Error, "Failed to start bot service", ex);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_status == BotStatus.Stopped || _status == BotStatus.Stopping)
        {
            _logger.LogWarning("Bot service is already stopped or stopping (current status: {Status})", _status);
            return;
        }

        await ChangeStatusAsync(BotStatus.Stopping, "Stopping bot service...");

        try
        {
            _logger.LogInformation("Logging out from Discord...");
            await _client.LogoutAsync();

            _logger.LogInformation("Stopping Discord client...");
            await _client.StopAsync();

            await ChangeStatusAsync(BotStatus.Stopped, "Bot service stopped");
            _logger.LogInformation("Bot service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping bot service");
            await ChangeStatusAsync(BotStatus.Error, "Error occurred while stopping", ex);
            throw;
        }
    }

    public Task<BotStatistics> GetStatisticsAsync()
    {
        ThrowIfDisposed();

        var process = Process.GetCurrentProcess();
        var memoryUsage = process.WorkingSet64;

        var guildCount = _client.ConnectionState == ConnectionState.Connected ? _client.Guilds.Count : 0;
        var userCount = _client.ConnectionState == ConnectionState.Connected 
            ? _client.Guilds.Sum(g => g.MemberCount) 
            : 0;

        return Task.FromResult(new BotStatistics
        {
            StartedAt = _startedAt,
            GuildCount = guildCount,
            UserCount = userCount,
            CommandsExecuted = _commandsExecuted,
            GatewayLatency = _client.Latency,
            MemoryUsage = memoryUsage,
            Version = "0.0.1"
        });
    }

    public void IncrementCommandCounter()
    {
        Interlocked.Increment(ref _commandsExecuted);
    }

    private Task OnClientLog(LogMessage logMessage)
    {
        var logLevel = logMessage.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, logMessage.Exception, "Discord: {Message}", logMessage.Message);
        return Task.CompletedTask;
    }

    private async Task OnClientReady()
    {
        await ChangeStatusAsync(BotStatus.Running, "Bot is ready and connected");
        
        var stats = await GetStatisticsAsync();
        _logger.LogInformation("Bot is ready! Connected to {GuildCount} guilds with {UserCount} total users", 
            stats.GuildCount, stats.UserCount);
    }

    private Task OnClientConnected()
    {
        _logger.LogInformation("Discord client connected");
        return Task.CompletedTask;
    }

    private async Task OnClientDisconnected(Exception exception)
    {
        _logger.LogWarning(exception, "Discord client disconnected");
        
        if (_status == BotStatus.Running)
        {
            await ChangeStatusAsync(BotStatus.Error, "Unexpectedly disconnected from Discord", exception);
        }
    }

    private Task ChangeStatusAsync(BotStatus newStatus, string? message = null, Exception? exception = null)
    {
        var previousStatus = _status;
        _status = newStatus;

        var eventArgs = new BotStatusChangedEventArgs
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Message = message,
            Exception = exception
        };

        _logger.LogInformation("Bot status changed from {PreviousStatus} to {NewStatus}: {Message}",
            previousStatus, newStatus, message);

        StatusChanged?.Invoke(this, eventArgs);
        return Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BotService));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // Unsubscribe from events
            _client.Log -= OnClientLog;
            _client.Ready -= OnClientReady;
            _client.Disconnected -= OnClientDisconnected;
            _client.Connected -= OnClientConnected;

            // Stop the client if it's running
            if (_status == BotStatus.Running || _status == BotStatus.Starting)
            {
                _client.StopAsync().GetAwaiter().GetResult();
            }

            _client.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during BotService disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}