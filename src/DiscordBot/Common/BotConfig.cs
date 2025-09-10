using Microsoft.Extensions.Configuration;

namespace DiscordBot.Common;

public sealed class BotConfig
{
    public string Token { get; init; } = "";
    public ulong? DevGuildId { get; init; }
    public bool UseAllUnprivilegedIntents { get; init; } = true;
    public bool UseGuildMembersIntent { get; init; } = true;
    public bool RegisterCommandsGlobally { get; init; } = false;

    public static BotConfig From(IConfiguration cfg)
    {
        var section = cfg.GetSection("Discord");
        return new BotConfig
        {
            Token = section.GetValue<string>("Token") ?? "",
            DevGuildId = TryUlong(section.GetValue<string>("DevGuildId")),
            UseAllUnprivilegedIntents = section.GetValue("UseAllUnprivilegedIntents", true),
            UseGuildMembersIntent = section.GetValue("UseGuildMembersIntent", true),
            RegisterCommandsGlobally = section.GetValue("RegisterCommandsGlobally", false),
        };
    }

    private static ulong? TryUlong(string? v) => ulong.TryParse(v, out var x) ? x : null;
}