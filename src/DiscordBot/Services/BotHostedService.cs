using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Services;

public sealed class BotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly ILogger<BotHostedService> _logger;
    private readonly InteractionHandler _handler;
    private readonly IConfiguration _cfg;
    private readonly BotConfig _botCfg;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionService interactions,
        ILogger<BotHostedService> logger,
        InteractionHandler handler,
        IConfiguration cfg)
    {
        _client = client;
        _interactions = interactions;
        _logger = logger;
        _handler = handler;
        _cfg = cfg;
        _botCfg = BotConfig.From(cfg);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += msg => { Log(msg); return Task.CompletedTask; };
        _client.Ready += OnReady;

        await _handler.InitializeAsync();

        await _client.LoginAsync(TokenType.Bot, _botCfg.Token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.LogoutAsync();
        await _client.StopAsync();
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

    private async Task OnReady()
    {
        if (_botCfg.RegisterCommandsGlobally)
        {
            await _interactions.RegisterCommandsGloballyAsync();
            _logger.LogInformation("Slash-Commands global registiert.");
        }
        else if (_botCfg.DevGuildId is ulong gid)
        {
            await _interactions.RegisterCommandsToGuildAsync(gid);
            _logger.LogInformation("Slash-Commands für DevGuild {GuildId} registriert.", gid);
        }
        else
        {
            _logger.LogWarning("Weder globale Registrierung noch DevGuildId gesetzt - keine Commands registriert.");
        }
    }
}