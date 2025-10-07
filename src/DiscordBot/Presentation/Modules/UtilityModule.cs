using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace DiscordBot.Presentation.Modules;

/// <summary>
/// Module containing utility commands for bot administration and user convenience
/// </summary>
[Group("utility", "Nützliche Befehle für alle Benutzer")]
public sealed class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IBotService _botService;
    private readonly ILogger<UtilityModule> _logger;

    public UtilityModule(IBotService botService, ILogger<UtilityModule> logger)
    {
        _botService = botService ?? throw new ArgumentNullException(nameof(botService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [SlashCommand("ping", "Zeigt die aktuelle Latenz und Systemstatus an")]
    public async Task PingAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        await DeferAsync(ephemeral: true);
        
        stopwatch.Stop();
        var responseTime = stopwatch.ElapsedMilliseconds;
        var gatewayLatency = Context.Client.Latency;

        var embed = new EmbedBuilder()
            .WithTitle("🏓 Pong!")
            .WithColor(Color.Green)
            .AddField("Gateway Latenz", $"{gatewayLatency}ms", true)
            .AddField("API Antwortzeit", $"{responseTime}ms", true)
            .WithFooter($"Bot Version 0.0.1")
            .WithCurrentTimestamp()
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
        
        _logger.LogInformation("Ping command executed by {UserId} - Gateway: {GatewayLatency}ms, API: {ResponseTime}ms", 
            Context.User.Id, gatewayLatency, responseTime);
    }

    [SlashCommand("echo", "Gibt den eingegebenen Text zurück")]
    public async Task EchoAsync(
        [Summary("text", "Der Text, der wiederholt werden soll")] string text,
        [Summary("ephemeral", "Ob die Antwort nur für Sie sichtbar sein soll")] bool ephemeral = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await RespondAsync("❌ Der Text darf nicht leer sein.", ephemeral: true);
            return;
        }

        if (text.Length > 2000)
        {
            await RespondAsync("❌ Der Text ist zu lang (Maximum: 2000 Zeichen).", ephemeral: true);
            return;
        }

        await RespondAsync($"🔄 **Echo:** {text}", ephemeral: ephemeral);
        
        _logger.LogInformation("Echo command executed by {UserId} with text length {TextLength}", 
            Context.User.Id, text.Length);
    }

    [SlashCommand("userinfo", "Zeigt Informationen über einen Benutzer an")]
    public async Task UserInfoAsync(
        [Summary("user", "Der Benutzer, über den Informationen angezeigt werden sollen")] IUser? user = null)
    {
        user ??= Context.User;

        await DeferAsync(ephemeral: true);

        var guildUser = user as IGuildUser;
        var embed = new EmbedBuilder()
            .WithTitle("👤 Benutzerinformationen")
            .WithThumbnailUrl(user.GetAvatarUrl(size: 256) ?? user.GetDefaultAvatarUrl())
            .WithColor(guildUser?.RoleColor ?? Color.Blue)
            .AddField("Benutzername", $"{user.Username}", true)
            .AddField("Diskriminator", $"#{user.Discriminator}", true)
            .AddField("ID", user.Id.ToString(), true)
            .AddField("Erstellt am", user.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss UTC"), true);

        if (guildUser != null)
        {
            embed.AddField("Beigetreten am", 
                guildUser.JoinedAt?.ToString("dd.MM.yyyy HH:mm:ss UTC") ?? "Unbekannt", true);

            if (guildUser.Nickname != null)
            {
                embed.AddField("Spitzname", guildUser.Nickname, true);
            }

            var roles = guildUser.RoleIds
                .Where(id => id != Context.Guild.EveryoneRole.Id)
                .Select(id => Context.Guild.GetRole(id))
                .Where(role => role != null)
                .OrderByDescending(role => role!.Position)
                .Take(10)
                .Select(role => role!.Mention);

            if (roles.Any())
            {
                embed.AddField($"Rollen ({guildUser.RoleIds.Count - 1})", 
                    string.Join(", ", roles), false);
            }
        }

        embed.AddField("Bot?", user.IsBot ? "✅ Ja" : "❌ Nein", true)
             .WithFooter($"Angefordert von {Context.User.Username}", Context.User.GetAvatarUrl())
             .WithCurrentTimestamp();

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
        
        _logger.LogInformation("UserInfo command executed by {UserId} for target user {TargetUserId}", 
            Context.User.Id, user.Id);
    }

    [SlashCommand("serverinfo", "Zeigt Informationen über den aktuellen Server an")]
    public async Task ServerInfoAsync()
    {
        if (Context.Guild == null)
        {
            await RespondAsync("❌ Dieser Befehl kann nur in einem Server verwendet werden.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        var guild = Context.Guild;
        var owner = await guild.GetOwnerAsync();

        var embed = new EmbedBuilder()
            .WithTitle($"🏰 {guild.Name}")
            .WithThumbnailUrl(guild.IconUrl)
            .WithColor(Color.Purple)
            .AddField("Server ID", guild.Id.ToString(), true)
            .AddField("Besitzer", owner?.Mention ?? "Unbekannt", true)
            .AddField("Erstellt am", guild.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss UTC"), true)
            .AddField("Mitglieder", guild.MemberCount.ToString("N0"), true)
            .AddField("Kanäle", guild.Channels.Count.ToString("N0"), true)
            .AddField("Rollen", guild.Roles.Count.ToString("N0"), true)
            .AddField("Emojis", guild.Emotes.Count.ToString("N0"), true)
            .AddField("Boosts", $"{guild.PremiumSubscriptionCount} (Stufe {(int)guild.PremiumTier})", true)
            .WithFooter($"Angefordert von {Context.User.Username}", Context.User.GetAvatarUrl())
            .WithCurrentTimestamp();

        if (!string.IsNullOrEmpty(guild.Description))
        {
            embed.WithDescription(guild.Description);
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
        
        _logger.LogInformation("ServerInfo command executed by {UserId} in guild {GuildId}", 
            Context.User.Id, guild.Id);
    }

    [SlashCommand("stats", "Zeigt Bot-Statistiken an")]
    public async Task StatsAsync()
    {
        await DeferAsync(ephemeral: true);

        var statistics = await _botService.GetStatisticsAsync();
        var process = Process.GetCurrentProcess();

        var embed = new EmbedBuilder()
            .WithTitle("📊 Bot Statistiken")
            .WithColor(Color.Gold)
            .AddField("🔄 Laufzeit", 
                $"{statistics.Uptime.Days}d {statistics.Uptime.Hours}h {statistics.Uptime.Minutes}m", true)
            .AddField("🏰 Server", statistics.GuildCount.ToString("N0"), true)
            .AddField("👥 Benutzer", statistics.UserCount.ToString("N0"), true)
            .AddField("⚡ Befehle ausgeführt", statistics.CommandsExecuted.ToString("N0"), true)
            .AddField("📡 Gateway Latenz", $"{statistics.GatewayLatency}ms", true)
            .AddField("💾 Speicherverbrauch", $"{statistics.MemoryUsage / 1024.0 / 1024.0:F1} MB", true)
            .AddField("🔢 Version", statistics.Version, true)
            .AddField("⚙️ .NET Version", Environment.Version.ToString(), true)
            .AddField("💻 Betriebssystem", Environment.OSVersion.ToString(), true)
            .WithFooter($"Bot gestartet", statistics.StartedAt)
            .WithCurrentTimestamp();

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
        
        _logger.LogInformation("Stats command executed by {UserId}", Context.User.Id);
    }
}

/// <summary>
/// Module containing administrative commands that require special permissions
/// </summary>
[Group("admin", "Administrative Befehle")]
[DefaultMemberPermissions(GuildPermission.ManageMessages)]
public sealed class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<AdminModule> _logger;

    public AdminModule(ILogger<AdminModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [SlashCommand("purge", "Löscht eine bestimmte Anzahl von Nachrichten")]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task PurgeAsync(
        [Summary("count", "Anzahl der zu löschenden Nachrichten (1-100)")] 
        [MinValue(1), MaxValue(100)] int count,
        [Summary("reason", "Grund für das Löschen der Nachrichten")] string? reason = null)
    {
        if (Context.Channel is not ITextChannel textChannel)
        {
            await RespondAsync("❌ Dieser Befehl kann nur in Textkanälen verwendet werden.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        try
        {
            var messages = await textChannel.GetMessagesAsync(count + 1).FlattenAsync(); // +1 to include the command message
            var messagesToDelete = messages.Where(m => DateTimeOffset.UtcNow - m.CreatedAt < TimeSpan.FromDays(14));
            
            var deletedCount = 0;
            if (messagesToDelete.Any())
            {
                await textChannel.DeleteMessagesAsync(messagesToDelete);
                deletedCount = messagesToDelete.Count();
            }

            var embed = new EmbedBuilder()
                .WithTitle("🧹 Nachrichten gelöscht")
                .WithDescription($"**{deletedCount}** Nachrichten wurden erfolgreich gelöscht.")
                .WithColor(Color.Green)
                .AddField("Moderator", Context.User.Mention, true)
                .AddField("Kanal", textChannel.Mention, true);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed.AddField("Grund", reason, false);
            }

            embed.WithFooter($"Ausgeführt von {Context.User.Username}", Context.User.GetAvatarUrl())
                 .WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogInformation("Purge command executed by {UserId} in channel {ChannelId}: {DeletedCount} messages deleted. Reason: {Reason}", 
                Context.User.Id, textChannel.Id, deletedCount, reason ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing purge command");
            
            await FollowupAsync(
                "❌ Beim Löschen der Nachrichten ist ein Fehler aufgetreten. " +
                "Stellen Sie sicher, dass der Bot die erforderlichen Berechtigungen hat.",
                ephemeral: true);
        }
    }

    [SlashCommand("kick", "Entfernt einen Benutzer vom Server")]
    [RequireBotPermission(GuildPermission.KickMembers)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task KickAsync(
        [Summary("user", "Der zu entfernende Benutzer")] IGuildUser user,
        [Summary("reason", "Grund für den Kick")] string? reason = null)
    {
        if (user.Id == Context.User.Id)
        {
            await RespondAsync("❌ Sie können sich nicht selbst kicken.", ephemeral: true);
            return;
        }

        if (user.Id == Context.Client.CurrentUser.Id)
        {
            await RespondAsync("❌ Ich kann mich nicht selbst kicken.", ephemeral: true);
            return;
        }

        var contextUser = Context.User as IGuildUser;
        if (contextUser != null && user.Hierarchy >= contextUser.Hierarchy)
        {
            await RespondAsync("❌ Sie können keine Benutzer kicken, die eine höhere oder gleiche Rolle haben.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        try
        {
            await user.KickAsync(reason);

            var embed = new EmbedBuilder()
                .WithTitle("👢 Benutzer gekickt")
                .WithDescription($"**{user.DisplayName}** wurde vom Server entfernt.")
                .WithColor(Color.Orange)
                .AddField("Benutzer", $"{user.Username}#{user.Discriminator}", true)
                .AddField("Moderator", Context.User.Mention, true);

            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed.AddField("Grund", reason, false);
            }

            embed.WithFooter($"Ausgeführt von {Context.User.Username}", Context.User.GetAvatarUrl())
                 .WithCurrentTimestamp();

            await FollowupAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogInformation("Kick command executed by {UserId} on target {TargetUserId}. Reason: {Reason}", 
                Context.User.Id, user.Id, reason ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kick command");
            
            await FollowupAsync(
                "❌ Beim Kicken des Benutzers ist ein Fehler aufgetreten. " +
                "Stellen Sie sicher, dass der Bot die erforderlichen Berechtigungen hat.",
                ephemeral: true);
        }
    }
}