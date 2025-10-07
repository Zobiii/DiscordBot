using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Hosted service that manages the bot lifecycle within the .NET Generic Host
/// </summary>
public sealed class BotHostedService : BackgroundService
{
    private readonly IBotService _botService;
    private readonly IInteractionHandler _interactionHandler;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IHostApplicationLifetime _hostLifetime;

    public BotHostedService(
        IBotService botService,
        IInteractionHandler interactionHandler,
        ILogger<BotHostedService> logger,
        IHostApplicationLifetime hostLifetime)
    {
        _botService = botService ?? throw new ArgumentNullException(nameof(botService));
        _interactionHandler = interactionHandler ?? throw new ArgumentNullException(nameof(interactionHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));

        // Subscribe to bot status changes
        _botService.StatusChanged += OnBotStatusChanged;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bot hosted service is starting...");
        
        try
        {
            // Initialize the interaction handler first
            await _interactionHandler.InitializeAsync(cancellationToken);
            _logger.LogInformation("Interaction handler initialized");

            await base.StartAsync(cancellationToken);
            _logger.LogInformation("Bot hosted service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start bot hosted service");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting bot service execution...");

        try
        {
            // Start the bot service
            await _botService.StartAsync(stoppingToken);

            // Wait for the bot to be ready before registering commands
            await WaitForBotReady(stoppingToken);

            // Register commands
            await _interactionHandler.RegisterCommandsAsync(stoppingToken);
            
            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bot service execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot service execution failed");
            
            // Stop the application if the bot service fails
            _hostLifetime.StopApplication();
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bot hosted service is stopping...");

        try
        {
            await _botService.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
            
            _logger.LogInformation("Bot hosted service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping bot hosted service");
            throw;
        }
        finally
        {
            // Unsubscribe from events
            _botService.StatusChanged -= OnBotStatusChanged;
        }
    }

    private async Task WaitForBotReady(CancellationToken cancellationToken)
    {
        var maxWaitTime = TimeSpan.FromMinutes(2);
        var checkInterval = TimeSpan.FromSeconds(1);
        var startTime = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_botService.Status == Core.Models.BotStatus.Running)
            {
                _logger.LogInformation("Bot is ready and running");
                return;
            }

            if (_botService.Status == Core.Models.BotStatus.Error)
            {
                throw new InvalidOperationException("Bot failed to start - status is Error");
            }

            if (DateTimeOffset.UtcNow - startTime > maxWaitTime)
            {
                throw new TimeoutException($"Bot did not become ready within {maxWaitTime.TotalSeconds} seconds");
            }

            await Task.Delay(checkInterval, cancellationToken);
        }
    }

    private void OnBotStatusChanged(object? sender, Core.Models.BotStatusChangedEventArgs e)
    {
        _logger.LogInformation("Bot status changed from {PreviousStatus} to {NewStatus}: {Message}",
            e.PreviousStatus, e.NewStatus, e.Message);

        // If the bot enters an error state unexpectedly, stop the application
        if (e.NewStatus == Core.Models.BotStatus.Error && e.PreviousStatus == Core.Models.BotStatus.Running)
        {
            _logger.LogCritical("Bot entered error state unexpectedly. Stopping application...");
            _hostLifetime.StopApplication();
        }
    }
}