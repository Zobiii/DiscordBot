using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Health;
using DiscordBot.Application.Handlers;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register bot services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the Discord bot services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDiscordBot(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure and validate bot configuration
        services.Configure<BotConfiguration>(configuration);
        services.AddSingleton<IValidateOptions<BotConfiguration>, ValidateBotConfiguration>();

        // Add Discord client and services
        services.AddDiscordClient(configuration);
        services.AddBotServices();
        services.AddCommandHandlers();
        
        // Add resilience policies
        services.AddResiliencePolicies(configuration);

        // Add health checks
        services.AddBotHealthChecks(configuration);

        return services;
    }

    /// <summary>
    /// Adds Discord client with proper configuration
    /// </summary>
    private static IServiceCollection AddDiscordClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(serviceProvider =>
        {
            var botConfig = serviceProvider.GetRequiredService<IOptions<BotConfiguration>>().Value;
            var discordConfig = botConfig.Discord;

            var intents = GatewayIntents.None;
            if (discordConfig.Intents.UseAllUnprivileged)
                intents |= GatewayIntents.AllUnprivileged;
            if (discordConfig.Intents.UseGuildMembers)
                intents |= GatewayIntents.GuildMembers;
            if (discordConfig.Intents.UseMessageContent)
                intents |= GatewayIntents.MessageContent;
            if (discordConfig.Intents.UsePresenceUpdate)
                intents |= GatewayIntents.GuildPresences;

            var clientConfig = new DiscordSocketConfig
            {
                GatewayIntents = intents,
                MessageCacheSize = discordConfig.Client.MessageCacheSize,
                AlwaysDownloadUsers = discordConfig.Client.AlwaysDownloadUsers,
                LogGatewayIntentWarnings = discordConfig.Client.LogGatewayIntentWarnings,
                UseInteractionSnowflakeDate = false,
                DefaultRetryMode = Enum.TryParse<RetryMode>(discordConfig.Client.RetryMode, out var retryMode) 
                    ? retryMode 
                    : RetryMode.AlwaysRetry,
                RestClientProvider = DefaultRestClientProvider.Create(timeout: TimeSpan.FromSeconds(discordConfig.Client.RequestTimeoutSeconds))
            };

            return new DiscordSocketClient(clientConfig);
        });

        services.AddSingleton(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<DiscordSocketClient>();
            var config = new InteractionServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                UseCompiledLambda = true
            };
            return new InteractionService(client, config);
        });

        return services;
    }

    /// <summary>
    /// Adds bot-specific services
    /// </summary>
    private static IServiceCollection AddBotServices(this IServiceCollection services)
    {
        services.AddSingleton<IBotService, BotService>();
        services.AddSingleton<IInteractionHandler, InteractionHandler>();
        services.AddHostedService<BotHostedService>();

        return services;
    }

    /// <summary>
    /// Adds command handlers and validators
    /// </summary>
    private static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        // Add FluentValidation
        services.AddValidatorsFromAssemblyContaining<Program>();

        // Add command-related services here
        return services;
    }

    /// <summary>
    /// Adds resilience policies using Polly
    /// </summary>
    private static IServiceCollection AddResiliencePolicies(this IServiceCollection services, IConfiguration configuration)
    {
        var resilienceConfig = configuration.GetSection("Resilience").Get<ResilienceConfiguration>() ?? new();

        // Add retry policy
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                resilienceConfig.MaxRetryAttempts,
                retryAttempt => resilienceConfig.UseExponentialBackoff
                    ? TimeSpan.FromMilliseconds(resilienceConfig.RetryDelayMs * Math.Pow(2, retryAttempt - 1))
                    : TimeSpan.FromMilliseconds(resilienceConfig.RetryDelayMs));

        services.AddSingleton(retryPolicy);

        // Add circuit breaker if enabled
        if (resilienceConfig.CircuitBreaker.Enabled)
        {
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    resilienceConfig.CircuitBreaker.FailureThreshold,
                    TimeSpan.FromSeconds(resilienceConfig.CircuitBreaker.BreakDurationSeconds));

            services.AddSingleton(circuitBreakerPolicy);
        }

        return services;
    }

    /// <summary>
    /// Adds health checks for the bot
    /// </summary>
    private static IServiceCollection AddBotHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthConfig = configuration.GetSection("HealthChecks").Get<HealthCheckConfiguration>() ?? new();

        if (healthConfig.Enabled)
        {
            services.AddHealthChecks()
                .AddCheck<BotHealthCheck>("bot")
                .AddCheck<DiscordHealthCheck>("discord")
                .AddCheck<MemoryHealthCheck>("memory");
        }

        return services;
    }
}

/// <summary>
/// Validator for bot configuration
/// </summary>
public class ValidateBotConfiguration : IValidateOptions<BotConfiguration>
{
    public ValidateOptionsResult Validate(string? name, BotConfiguration options)
    {
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
        {
            var errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown validation error");
            return ValidateOptionsResult.Fail(errors);
        }

        // Additional custom validations
        if (string.IsNullOrWhiteSpace(options.Discord.Token))
        {
            return ValidateOptionsResult.Fail("Discord token is required but not provided. Check your configuration or user secrets.");
        }

        if (!options.Discord.RegisterCommandsGlobally && !options.Discord.DevGuildId.HasValue)
        {
            return ValidateOptionsResult.Fail("Either RegisterCommandsGlobally must be true or DevGuildId must be provided.");
        }

        return ValidateOptionsResult.Success;
    }
}