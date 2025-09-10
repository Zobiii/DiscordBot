namespace DiscordBot.Common;

public static class Extensions
{
    public static async Task RetryAsync(Func<Task> action, int retries = 3, TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromMilliseconds(300);
        for (int i = 0; ; i++)
        {
            try { await action(); return; }
            catch when (i < retries) { await Task.Delay(delay.Value); }
        }
    }

    public static string Humanize(this TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds}s";
        if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m {(int)ts.Seconds}s";
        if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h {(int)ts.Minutes}m";
        return $"{(int)ts.TotalDays}d {(int)ts.Hours}h";
    }
}