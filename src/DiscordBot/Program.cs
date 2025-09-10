using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Common;
using DiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddUserSecrets(typeof(Program).Assembly, optional: true)
        .AddEnvironmentVariables();
    })
    .ConfigureLogging((ctx, logging) =>
    {
        var min = ctx.Configuration.GetValue("Logging:MinLevel", "Information");
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(Enum.TryParse<LogLevel>(min, true, out var lv) ? lv : LogLevel.Information);
    })
    .ConfigureServices((ctx, services) =>
    {
        var cfg = BotConfig.From(ctx.Configuration);

        GatewayIntents intents = GatewayIntents.None;
        if (cfg.UseAllUnprivilegedIntents) intents |= GatewayIntents.AllUnprivileged;
        if (cfg.UseGuildMembersIntent) intents |= GatewayIntents.GuildMembers;

        var clientConfig = new DiscordSocketConfig
        {
            GatewayIntents = intents,
            AlwaysDownloadUsers = cfg.UseGuildMembersIntent,
            LogGatewayIntentWarnings = false,
            MessageCacheSize = 100,
            UseInteractionSnowflakeDate = false
        };

        services.AddSingleton(new DiscordSocketClient(clientConfig));
        services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));

        services.AddSingleton<InteractionHandler>();
        services.AddHostedService<BotHostedService>();
    })
    .Build();

await host.RunAsync();