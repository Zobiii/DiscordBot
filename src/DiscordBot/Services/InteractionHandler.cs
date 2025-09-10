using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using DiscordBot.Modules;

namespace DiscordBot.Services;

public sealed class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly ILogger<InteractionHandler> _logger;
    private readonly IServiceProvider _services;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactions,
        ILogger<InteractionHandler> logger,
        IServiceProvider services)
    {
        _client = client;
        _interactions = interactions;
        _logger = logger;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        _client.InteractionCreated += HandleInteraction;
        _interactions.Log += msg => { Log(msg); return Task.CompletedTask; };

        await _interactions.AddModuleAsync<UtilityModule>(_services);
    }

    private void Log(LogMessage msg)
    {
        var level = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
        _logger.Log(level, msg.Exception, msg.Message);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Interaction.");
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                try { await interaction.GetOriginalResponseAsync(); }
                catch { }

                try
                {
                    await interaction.RespondAsync("Unerwarteter Fehler. Bitte später nochmal probieren.", ephemeral: true);
                }
                catch { }
            }
        }
    }
}