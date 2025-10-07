using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace DiscordBot.Application.Handlers;

/// <summary>
/// Handles Discord interactions (slash commands, context menus, etc.) with comprehensive error handling and metrics
/// </summary>
public sealed class InteractionHandler : IInteractionHandler, IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotConfiguration _config;
    private readonly ILogger<InteractionHandler> _logger;
    private readonly IAsyncPolicy? _retryPolicy;
    private readonly SemaphoreSlim _commandSemaphore;

    private readonly ConcurrentDictionary<string, int> _commandStatistics = new();
    private volatile bool _initialized = false;
    private volatile bool _disposed = false;

    public InteractionService InteractionService => _interactionService;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> config,
        ILogger<InteractionHandler> logger,
        IAsyncPolicy? retryPolicy = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy;

        _commandSemaphore = new SemaphoreSlim(_config.Discord.MaxConcurrentCommands, _config.Discord.MaxConcurrentCommands);

        // Subscribe to events
        _client.InteractionCreated += HandleInteractionAsync;
        _interactionService.SlashCommandExecuted += OnSlashCommandExecuted;
        _interactionService.ContextCommandExecuted += OnContextCommandExecuted;
        _interactionService.ComponentCommandExecuted += OnComponentCommandExecuted;
        _interactionService.Log += OnInteractionServiceLog;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            _logger.LogWarning("InteractionHandler is already initialized");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing interaction handler...");

            // Add modules from the current assembly
            await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);
            _logger.LogInformation("Added {ModuleCount} interaction modules", _interactionService.Modules.Count);

            _initialized = true;
            _logger.LogInformation("Interaction handler initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize interaction handler");
            throw;
        }
    }

    public async Task RegisterCommandsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            throw new InvalidOperationException("InteractionHandler must be initialized before registering commands");
        }

        try
        {
            _logger.LogInformation("Registering slash commands...");

            var registerCommands = async () =>
            {
                if (_config.Discord.RegisterCommandsGlobally)
                {
                    await _interactionService.RegisterCommandsGloballyAsync();
                    _logger.LogInformation("Slash commands registered globally");
                }
                else if (_config.Discord.DevGuildId.HasValue)
                {
                    await _interactionService.RegisterCommandsToGuildAsync(_config.Discord.DevGuildId.Value);
                    _logger.LogInformation("Slash commands registered to development guild {GuildId}", _config.Discord.DevGuildId.Value);
                }
                else
                {
                    _logger.LogWarning("No guild ID provided for command registration and global registration is disabled");
                    return;
                }
            };

            if (_retryPolicy != null)
            {
                await _retryPolicy.ExecuteAsync(registerCommands);
            }
            else
            {
                await registerCommands();
            }

            var commandCount = _interactionService.SlashCommands.Count + _interactionService.ContextCommands.Count;
            _logger.LogInformation("Successfully registered {CommandCount} commands", commandCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register commands");
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetCommandStatisticsAsync()
    {
        ThrowIfDisposed();
        return new Dictionary<string, int>(_commandStatistics);
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (_disposed) return;

        // Acquire semaphore to limit concurrent command executions
        if (!await _commandSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            _logger.LogWarning("Command execution rejected - too many concurrent commands. User: {UserId}, Command: {CommandName}",
                interaction.User.Id, GetInteractionName(interaction));

            try
            {
                await interaction.RespondAsync("🚫 Der Bot ist überlastet. Bitte versuchen Sie es in einem Moment erneut.", ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send overload response");
            }
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var context = new SocketInteractionContext(_client, interaction);
        var commandName = GetInteractionName(interaction);
        var userId = interaction.User.Id;
        var guildId = interaction.GuildId;

        try
        {
            _logger.LogInformation("Executing command {CommandName} for user {UserId} in guild {GuildId}",
                commandName, userId, guildId);

            // Set a timeout for command execution
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.Discord.CommandTimeoutSeconds));

            // Execute the command with timeout
            var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider).WaitAsync(timeoutCts.Token);

            stopwatch.Stop();

            // Log command result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Command {CommandName} executed successfully in {ExecutionTime}ms",
                    commandName, stopwatch.ElapsedMilliseconds);

                // Increment command statistics
                _commandStatistics.AddOrUpdate(commandName, 1, (key, value) => value + 1);

                // Increment bot service command counter
                if (_serviceProvider.GetService<IBotService>() is BotService botService)
                {
                    botService.IncrementCommandCounter();
                }
            }
            else
            {
                _logger.LogError("Command {CommandName} failed: {ErrorReason}",
                    commandName, result.ErrorReason);

                await HandleCommandError(interaction, result, stopwatch.Elapsed);
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Command {CommandName} timed out after {Timeout}s", commandName, _config.Discord.CommandTimeoutSeconds);

            await HandleCommandTimeout(interaction, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unhandled exception in command {CommandName} execution", commandName);

            await HandleCommandException(interaction, ex, stopwatch.Elapsed);
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }

    private async Task HandleCommandError(SocketInteraction interaction, IResult result, TimeSpan executionTime)
    {
        try
        {
            var errorMessage = result.Error switch
            {
                InteractionCommandError.UnmetPrecondition => "❌ Sie haben nicht die erforderlichen Berechtigungen für diesen Befehl.",
                InteractionCommandError.UnknownCommand => "❓ Dieser Befehl ist nicht bekannt oder wurde entfernt.",
                InteractionCommandError.BadArgs => "⚠️ Ungültige Argumente. Bitte überprüfen Sie Ihre Eingabe.",
                InteractionCommandError.Exception => "💥 Ein Fehler ist beim Ausführen des Befehls aufgetreten.",
                InteractionCommandError.Unsuccessful => "❌ Der Befehl konnte nicht erfolgreich ausgeführt werden.",
                _ => "❓ Ein unbekannter Fehler ist aufgetreten."
            };

            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(errorMessage, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(errorMessage, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error response for command failure");
        }
    }

    private async Task HandleCommandTimeout(SocketInteraction interaction, TimeSpan executionTime)
    {
        try
        {
            const string timeoutMessage = "⏱️ Der Befehl hat zu lange gedauert und wurde abgebrochen. Bitte versuchen Sie es erneut.";

            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(timeoutMessage, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(timeoutMessage, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send timeout response");
        }
    }

    private async Task HandleCommandException(SocketInteraction interaction, Exception exception, TimeSpan executionTime)
    {
        try
        {
            const string errorMessage = "💥 Ein unerwarteter Fehler ist aufgetreten. Der Fehler wurde gemeldet und wird untersucht.";

            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(errorMessage, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(errorMessage, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send exception response");
        }
    }

    private static string GetInteractionName(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand slashCommand => slashCommand.CommandName,
        SocketMessageCommand messageCommand => messageCommand.CommandName,
        SocketUserCommand userCommand => userCommand.CommandName,
        SocketMessageComponent component => component.Data.CustomId,
        _ => "Unknown"
    };

    private async Task OnSlashCommandExecuted(SlashCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        // Additional logging for slash command execution can be added here
    }

    private async Task OnContextCommandExecuted(ContextCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        // Additional logging for context command execution can be added here
    }

    private async Task OnComponentCommandExecuted(ComponentCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        // Additional logging for component command execution can be added here
    }

    private async Task OnInteractionServiceLog(LogMessage logMessage)
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

        _logger.Log(logLevel, logMessage.Exception, "InteractionService: {Message}", logMessage.Message);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InteractionHandler));
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Unsubscribe from events
            _client.InteractionCreated -= HandleInteractionAsync;
            _interactionService.SlashCommandExecuted -= OnSlashCommandExecuted;
            _interactionService.ContextCommandExecuted -= OnContextCommandExecuted;
            _interactionService.ComponentCommandExecuted -= OnComponentCommandExecuted;
            _interactionService.Log -= OnInteractionServiceLog;

            _commandSemaphore.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during InteractionHandler disposal");
        }
        finally
        {
            _disposed = true;
        }
    }
}