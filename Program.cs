using System.Threading.Tasks;
using Disqord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;

namespace VerificationBot
{
    public static class Program
    {
        public const string CONFIG_FILE = "config.json";

        public static async Task Main()
            => await new ServiceCollection()
                .AddSingleton(Config.Load(CONFIG_FILE).Result)
                .AddSingleton(LoggerFactory.Create(log => log.AddConsole().AddDebug()))
                .AddSingleton(provider =>
                {
                    Config config = provider.GetService<Config>();
                    ILogger logger = provider.GetService<ILoggerFactory>().CreateLogger("Verification Bot");

                    return new VerificationBot
                    (
                        TokenType.Bot,
                        config,
                        logger,
                        new DiscordBotConfiguration
                        {
                            MessageCache = null,
                            ProviderFactory = _ => provider,
                            Activity = new LocalActivity("Hollow Knight: Silksong", ActivityType.Playing),
                            CommandServiceConfiguration = new CommandServiceConfiguration
                            {
                                IgnoresExtraArguments = true,
                                DefaultRunMode = RunMode.Parallel
                            },
                            Logger = new Optional<Disqord.Logging.ILogger>(new VLogger(logger))
                        }
                    );
                })
                .BuildServiceProvider()
                .GetService<VerificationBot>()
                .RunAsync();
    }
}
