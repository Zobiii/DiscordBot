using Discord;
using Discord.Interactions;

namespace DiscordBot.Modules;

public sealed class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Antwortet mit Latenz und Laufzeit.")]
    public async Task Ping()
    {
        var before = Context.Client.Latency;
        await RespondAsync($"🏓 Pong! Gateway-Latenz: {before}ms", ephemeral: true);
    }

    [SlashCommand("echo", "Gibt deine Nachricht zurück.")]
    public async Task Echo([Summary(description: "Der Text, der zurück kommt.")] string text, bool ephemeral = true)
    {
        await RespondAsync(text, ephemeral: ephemeral);
    }

    [SlashCommand("userinfo", "Infos über einen User.")]
    public async Task UserInfo(IUser? user = null)
    {
        user ??= Context.User;

        var eb = new EmbedBuilder()
            .WithAuthor(user)
            .WithTitle("UserInfo")
            .AddField("ID", user.Id, true)
            .AddField("Erstellt", user.CreatedAt.ToString("dd.MM.yyyy HH:mm"), true)
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .WithColor(Color.Blue);

        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }

    [DefaultMemberPermissions(GuildPermission.ManageMessages)]
    [SlashCommand("purge", "Löscht die letzten N Nachrichten im aktuellen Kanal.")]
    public async Task Purge([Summary(description: "Anzahl 1 - 100")] int count)
    {
        if (count < 1 || count > 100)
        {
            await RespondAsync("Bitte 1 - 100 angeben.", ephemeral: true);
            return;
        }

        var chan = Context.Channel as ITextChannel;
        if (chan == null)
        {
            await RespondAsync("Dieser Befehl geht nur in Textkanälen.", ephemeral: true);
            return;
        }

        var msgs = await chan.GetMessagesAsync(limit: count).FlattenAsync();
        await (chan as ITextChannel)!.DeleteMessagesAsync(msgs);
        await RespondAsync($"🧹 {msgs.Count()} Nachrichten gelöscht.", ephemeral: true);
    }
}