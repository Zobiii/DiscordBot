# Discord Bot

[![.NET Version](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue)](https://docker.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

A production-ready Discord bot built with .NET 9.0, featuring clean architecture, comprehensive logging, health monitoring, and enterprise-grade reliability patterns.

## 🚀 Features

### 🏗️ Architecture
- **Clean Architecture** with clear separation of concerns
- **SOLID Principles** throughout the codebase
- **Dependency Injection** with Microsoft.Extensions
- **Type-safe Configuration** with validation
- **Interface-based Design** for testability

### 🔧 Technical Features
- **Discord.Net 3.x** for Discord API integration
- **Serilog** for structured logging with multiple sinks
- **Health Checks** for monitoring bot status and dependencies
- **Resilience Patterns** with Polly (retry, circuit breaker)
- **Docker Support** with multi-stage builds
- **Environment-based Configuration** for development and production

### 🎮 Bot Features
- **Slash Commands** with comprehensive error handling
- **User Information** commands with rich embeds
- **Server Information** and statistics
- **Administrative Commands** (purge, kick) with proper permissions
- **Real-time Bot Statistics** and health monitoring
- **German Language** for user-facing messages

### 🔒 Security & Production Ready
- **Secure Secret Management** (user secrets for dev, env vars for prod)
- **Non-root Docker Container** execution
- **Resource Limits** and security options
- **Input Validation** and sanitization
- **Rate Limiting** and concurrency control
- **Comprehensive Error Handling** with user-friendly messages

## 📋 Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docker.com/) (optional, for containerized deployment)
- Discord Bot Token ([Discord Developer Portal](https://discord.com/developers/applications))

### 🔧 Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd DiscordBot
   ```

2. **Configure secrets**
   ```powershell
   # Initialize user secrets
   dotnet user-secrets init --project src/DiscordBot
   
   # Set your bot token
   dotnet user-secrets set "Discord:Token" "your-discord-bot-token" --project src/DiscordBot
   dotnet user-secrets set "Discord:DevGuildId" "your-development-guild-id" --project src/DiscordBot
   ```

3. **Build and run**
   ```powershell
   # Build with custom script (recommended)
   .\src\DiscordBot\Scripts\build.ps1 -Configuration Debug
   
   # Run the bot
   dotnet run --project src/DiscordBot --configuration Development
   ```

### 🐳 Docker Deployment

1. **Copy environment template**
   ```bash
   cp .env.example .env
   # Edit .env with your bot token and settings
   ```

2. **Deploy with Docker Compose**
   ```bash
   docker-compose up -d
   ```

3. **View logs**
   ```bash
   docker-compose logs -f discordbot
   ```

## 🏛️ Architecture Overview

```
src/DiscordBot/
├── Core/                     # Business logic and abstractions
│   ├── Interfaces/          # Service contracts and abstractions
│   ├── Models/              # Domain models and DTOs  
│   ├── Configuration/       # Type-safe configuration classes
│   └── Extensions/          # Service registration extensions
├── Infrastructure/          # External dependencies implementation
│   ├── Services/            # Core service implementations
│   ├── Health/              # Health check implementations
│   └── Logging/             # Logging infrastructure
├── Application/             # Application layer services
│   ├── Commands/            # Command definitions and DTOs
│   ├── Handlers/            # Command and interaction handlers
│   └── Validators/          # Input validation logic
├── Presentation/            # Discord interface layer
│   └── Modules/             # Discord slash command modules
└── Tests/                   # Test projects
    ├── Unit/                # Unit tests
    └── Integration/         # Integration tests
```

### 🔌 Key Components

- **IBotService**: Main bot lifecycle management
- **IInteractionHandler**: Discord interaction processing
- **BotConfiguration**: Type-safe configuration with validation
- **Health Checks**: Monitor bot, Discord connection, and memory usage
- **Resilience Policies**: Retry logic and circuit breakers using Polly

## ⚙️ Configuration

The bot uses a hierarchical configuration system:

1. **appsettings.json** - Base configuration
2. **appsettings.{Environment}.json** - Environment overrides  
3. **User Secrets** - Development secrets (never committed)
4. **Environment Variables** - Production deployment (prefix: `DISCORDBOT_`)
5. **Command Line Arguments** - Runtime overrides

### 📝 Configuration Example

```json
{
  "Discord": {
    "Token": "",
    "DevGuildId": null,
    "RegisterCommandsGlobally": false,
    "CommandTimeoutSeconds": 30,
    "MaxConcurrentCommands": 10,
    "Intents": {
      "UseAllUnprivileged": true,
      "UseGuildMembers": false,
      "UseMessageContent": false,
      "UsePresenceUpdate": false
    }
  },
  "Logging": {
    "MinimumLevel": "Information",
    "EnableConsole": true,
    "EnableFile": true
  },
  "HealthChecks": {
    "Enabled": true,
    "MemoryThresholdMB": 512
  },
  "Resilience": {
    "MaxRetryAttempts": 3,
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5
    }
  }
}
```

## 🎮 Commands

### Utility Commands (`/utility`)
- **`/utility ping`** - Shows bot latency and response time
- **`/utility echo <text>`** - Echoes back the provided text
- **`/utility userinfo [user]`** - Displays user information
- **`/utility serverinfo`** - Shows server information and statistics
- **`/utility stats`** - Displays bot statistics and metrics

### Admin Commands (`/admin`)
- **`/admin purge <count> [reason]`** - Deletes messages (requires Manage Messages)
- **`/admin kick <user> [reason]`** - Kicks a user (requires Kick Members)

All commands include:
- ✅ Comprehensive error handling
- ✅ Permission validation
- ✅ Input sanitization
- ✅ Structured logging
- ✅ German language responses

## 📊 Monitoring & Health Checks

### Built-in Health Checks
- **Bot Health**: Service status, uptime, and statistics
- **Discord Health**: Connection state and gateway latency
- **Memory Health**: Usage monitoring with configurable thresholds

### Metrics Tracked
- Command execution counts and response times
- Error rates and types
- Memory usage and performance
- Guild and user statistics
- Connection stability

### Logging
- **Serilog** with structured logging
- **Console output** with colors and formatting
- **File logging** with daily rotation
- **JSON format** support for log aggregation
- **Configurable log levels** per namespace

## 🛡️ Error Handling & Resilience

### Resilience Patterns
- **Retry Policy**: Exponential backoff for transient failures
- **Circuit Breaker**: Prevents cascading failures
- **Timeout Handling**: Command execution limits
- **Concurrency Control**: Semaphore-based throttling

### Error Response Strategy
- User-friendly error messages in German
- Ephemeral responses for errors (private to user)
- Comprehensive logging of all errors
- Graceful degradation for non-critical failures

## 🔧 Development

### Building
```powershell
# Recommended: Use custom build script
.\src\DiscordBot\Scripts\build.ps1 -Configuration Debug -Verbose

# Standard .NET commands
dotnet build src/DiscordBot
dotnet run --project src/DiscordBot
```

### Testing
```powershell
# Run tests
dotnet test src/DiscordBot --logger console

# Format code
dotnet format src/DiscordBot --severity info
```

### Adding New Commands

1. Create a new module class inheriting from `InteractionModuleBase<SocketInteractionContext>`
2. Use attributes for commands: `[SlashCommand]`, `[Group]`, `[RequireUserPermission]`
3. Include comprehensive error handling and logging
4. Follow existing patterns for parameter validation and responses

Example:
```csharp
[Group("mygroup", "My command group")]
public sealed class MyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<MyModule> _logger;

    [SlashCommand("mycommand", "My command description")]
    public async Task MyCommandAsync(
        [Summary("param", "Parameter description")] string parameter)
    {
        await DeferAsync(ephemeral: true);
        
        // Command logic here
        _logger.LogInformation("MyCommand executed by {UserId}", Context.User.Id);
        
        await FollowupAsync("✅ Command executed successfully!", ephemeral: true);
    }
}
```

## 🚀 Deployment

### Docker Production Deployment

1. **Set up environment variables**
   ```bash
   export DISCORDBOT_Discord__Token="your-bot-token"
   export DISCORDBOT_Discord__RegisterCommandsGlobally="true"
   ```

2. **Deploy with Docker Compose**
   ```bash
   docker-compose up -d
   ```

3. **Monitor the deployment**
   ```bash
   # View logs
   docker-compose logs -f discordbot
   
   # Check health
   curl http://localhost:8080/health
   ```

### Environment-Specific Configurations

- **Development**: Debug logging, local user secrets, guild-specific commands
- **Staging**: Info logging, environment variables, guild-specific commands  
- **Production**: Warning+ logging, environment variables, global commands

## 📁 Project Structure

```
DiscordBot/
├── src/DiscordBot/               # Main application
│   ├── Core/                     # Business logic layer
│   ├── Infrastructure/           # External dependencies
│   ├── Application/              # Application services  
│   ├── Presentation/             # Discord interface
│   ├── Scripts/                  # Build and deployment scripts
│   ├── appsettings*.json         # Configuration files
│   ├── Dockerfile               # Container definition
│   └── Program.cs               # Application entry point
├── docker-compose.yml           # Container orchestration
├── .env.example                 # Environment template
├── README.md                    # This file
└── WARP.md                      # Warp-specific documentation
```

## 🤝 Contributing

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Follow** the existing architecture and patterns
4. **Add** tests for new functionality
5. **Commit** your changes (`git commit -m 'Add amazing feature'`)
6. **Push** to the branch (`git push origin feature/amazing-feature`)
7. **Open** a Pull Request

### Code Style
- Follow existing patterns and architecture
- Use structured logging throughout
- Include comprehensive error handling
- Add XML documentation for public APIs
- Ensure all new features have tests

## 📝 Version History

### v0.0.1 (Current)
- ✨ Initial release with clean architecture
- ✨ Comprehensive logging with Serilog
- ✨ Health monitoring and diagnostics  
- ✨ Docker support with multi-stage builds
- ✨ Type-safe configuration system
- ✨ Resilience patterns (retry, circuit breaker)
- ✨ Basic utility and admin commands
- ✨ German language user interface

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Discord.Net](https://github.com/discord-net/Discord.Net) - Excellent Discord API wrapper
- [Serilog](https://serilog.net/) - Flexible logging framework
- [Polly](https://github.com/App-vNext/Polly) - Resilience and transient-fault handling
- [Microsoft Extensions](https://github.com/dotnet/extensions) - Dependency injection and hosting

## 📞 Support

For questions, issues, or feature requests:
- 🐛 **Issues**: Use GitHub Issues for bug reports
- 💡 **Features**: Use GitHub Issues for feature requests  
- 📖 **Documentation**: Check [WARP.md](WARP.md) for development guidance

---

**Built with ❤️ using .NET 9.0 and Discord.Net**