using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VerificationBot
{
    public static class Program
    {
        public const string CONFIG_FILE = "config.json";
        public const ulong EMOTE_GUILD = 773986072945360938;

        public static async Task Main()
        {
            Config config = await Config.Load(CONFIG_FILE);

            await new HostBuilder()
                .ConfigureLogging(x => x.AddConsole().AddDebug())
                .ConfigureServices(services =>
                {
                    services.AddSingleton(config);
                    services.AddSingleton<IPrefixProvider>(new VPrefixProvider(config));
                })
                .ConfigureDiscordBot<VerificationBot>((context, bot) =>
                {
                    bot.Token = config.Token;
                    bot.Intents = new GatewayIntents(GatewayIntent.Guilds | GatewayIntent.Members
                        | GatewayIntent.Bans | GatewayIntent.Emojis | GatewayIntent.GuildMessages
                        | GatewayIntent.GuildReactions | GatewayIntent.DirectMessages);
                    bot.Activities = new[] { new LocalActivity("Hollow Knight: Silksong", ActivityType.Playing) };
                })
                .Build()
                .RunAsync();
        }
    }
}
