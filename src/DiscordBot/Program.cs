using DiscordBot.Core.Configuration;
using DiscordBot.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Reflection;

namespace DiscordBot;

/// <summary>
/// Main entry point for the Discord bot application
/// </summary>
public static class Program
{
    /// <summary>
    /// Main application entry point
    /// </summary>
    /// <param name="arguments">Command line arguments</param>
    /// <returns>Exit code (0 for success, non-zero for failure)</returns>
    [System.STAThread]
    public static async Task<int> Main(string[] arguments)
    {
        // Configure early logging before host is built (only for critical startup errors)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .CreateBootstrapLogger(); // No console sink to avoid duplicate output

        try
        {
            Log.Information("Starting Discord Bot v{Version}", GetVersion());
            Log.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");
            
            var host = CreateHostBuilder(arguments).Build();
            
            await host.RunAsync();
            
            Log.Information("Discord Bot stopped gracefully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Discord Bot terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Creates and configures the host builder
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Configured host builder</returns>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()
            .ConfigureAppConfiguration(ConfigureAppConfiguration)
            .ConfigureServices(ConfigureServices)
            .UseSerilog(ConfigureSerilog)
            .UseDefaultServiceProvider((context, options) =>
            {
                // Enable validation of scopes in development
                options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
                options.ValidateOnBuild = true;
            });

    /// <summary>
    /// Configures application configuration sources
    /// </summary>
    /// <param name="context">Host builder context</param>
    /// <param name="config">Configuration builder</param>
    private static void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder config)
    {
        var env = context.HostingEnvironment;
        
        config.Sources.Clear();
        
        // Add configuration sources in order of precedence (later sources override earlier ones)
        config
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

        // Add user secrets in development
        if (env.IsDevelopment())
        {
            var assembly = Assembly.GetExecutingAssembly();
            config.AddUserSecrets(assembly, optional: true);
        }

        // Environment variables override everything
        config.AddEnvironmentVariables("DISCORDBOT_");
        
        // Command line arguments override everything else
        // Note: args from method parameter not available in this scope
        // Will be handled by Host.CreateDefaultBuilder

        Log.Debug("Configuration sources configured for environment: {Environment}", env.EnvironmentName);
    }

    /// <summary>
    /// Configures application services
    /// </summary>
    /// <param name="context">Host builder context</param>
    /// <param name="services">Service collection</param>
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        try
        {
            // Add and validate bot configuration
            var botConfiguration = new BotConfiguration();
            context.Configuration.Bind(botConfiguration);

            // Log configuration summary (without sensitive data)
            LogConfigurationSummary(botConfiguration);

            // Add Discord bot services
            services.AddDiscordBot(context.Configuration);

            Log.Information("Services configured successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to configure services");
            throw;
        }
    }

    /// <summary>
    /// Configures Serilog logging
    /// </summary>
    /// <param name="context">Host builder context</param>
    /// <param name="services">Service provider</param>
    /// <param name="configuration">Logger configuration</param>
    private static void ConfigureSerilog(HostBuilderContext context, IServiceProvider services, LoggerConfiguration configuration)
    {
        var botConfig = context.Configuration.Get<BotConfiguration>() ?? new BotConfiguration();
        var loggingConfig = botConfig.Logging;

        // Clear any existing configuration to avoid duplicates
        configuration
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DiscordBot")
            .Enrich.WithProperty("Version", GetVersion())
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

        // Configure minimum level
        if (Enum.TryParse<LogEventLevel>(loggingConfig.MinimumLevel, true, out var minLevel))
        {
            configuration.MinimumLevel.Is(minLevel);
        }

        // Override levels for noisy Microsoft loggers
        configuration
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning);

        // Configure console sink
        if (loggingConfig.EnableConsole)
        {
            var consoleConfig = loggingConfig.Console;
            var outputTemplate = consoleConfig.OutputTemplate;

            if (consoleConfig.UseColors)
            {
                configuration.WriteTo.Console(
                    outputTemplate: outputTemplate,
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code);
            }
            else
            {
                configuration.WriteTo.Console(outputTemplate: outputTemplate);
            }
        }

        // Configure file sink
        if (loggingConfig.EnableFile)
        {
            var fileConfig = loggingConfig.File;
            
            if (Enum.TryParse<RollingInterval>(fileConfig.RollingInterval, true, out var rollingInterval))
            {
                if (fileConfig.UseCompactFormat)
                {
                    configuration.WriteTo.File(
                        new Serilog.Formatting.Compact.CompactJsonFormatter(),
                        fileConfig.PathTemplate,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: fileConfig.RetainedFileCountLimit,
                        fileSizeLimitBytes: fileConfig.FileSizeLimitBytes);
                }
                else
                {
                    configuration.WriteTo.File(
                        fileConfig.PathTemplate,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: fileConfig.RetainedFileCountLimit,
                        fileSizeLimitBytes: fileConfig.FileSizeLimitBytes);
                }
            }
        }

        // Note: Debug sink not available in this Serilog version

        Log.Information("Serilog configured - Console: {Console}, File: {File}, MinLevel: {MinLevel}", 
            loggingConfig.EnableConsole, loggingConfig.EnableFile, loggingConfig.MinimumLevel);
    }

    /// <summary>
    /// Logs a summary of the configuration without sensitive information
    /// </summary>
    /// <param name="config">Bot configuration</param>
    private static void LogConfigurationSummary(BotConfiguration config)
    {
        Log.Information("Configuration Summary:");
        Log.Information("  Discord:");
        Log.Information("    Token: {TokenStatus}", string.IsNullOrEmpty(config.Discord.Token) ? "❌ Not Set" : "✅ Set");
        Log.Information("    Dev Guild ID: {DevGuildId}", config.Discord.DevGuildId?.ToString() ?? "Not Set");
        Log.Information("    Register Globally: {RegisterGlobally}", config.Discord.RegisterCommandsGlobally);
        Log.Information("    Command Timeout: {CommandTimeout}s", config.Discord.CommandTimeoutSeconds);
        Log.Information("    Max Concurrent Commands: {MaxConcurrentCommands}", config.Discord.MaxConcurrentCommands);
        Log.Information("  Intents:");
        Log.Information("    All Unprivileged: {AllUnprivileged}", config.Discord.Intents.UseAllUnprivileged);
        Log.Information("    Guild Members: {GuildMembers}", config.Discord.Intents.UseGuildMembers);
        Log.Information("    Message Content: {MessageContent}", config.Discord.Intents.UseMessageContent);
        Log.Information("    Presence Update: {PresenceUpdate}", config.Discord.Intents.UsePresenceUpdate);
        Log.Information("  Health Checks: {HealthChecksEnabled}", config.HealthChecks.Enabled);
        Log.Information("  Logging: Console={Console}, File={File}, Level={Level}", 
            config.Logging.EnableConsole, config.Logging.EnableFile, config.Logging.MinimumLevel);
    }

    /// <summary>
    /// Gets the application version
    /// </summary>
    /// <returns>Version string</returns>
    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                     ?? assembly.GetName().Version?.ToString()
                     ?? "0.0.1";
        return version;
    }
}