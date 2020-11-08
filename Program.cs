using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qmmands;
using VerificationBot.SpeedrunCom;

namespace VerificationBot
{
    public static class Program
    {
        public static async Task Main()
            => await new ServiceCollection()
                .AddSingleton(Config.Load("config.json"))
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

    public class Commands : DiscordModuleBase<VCommandContext>
    {
        [Command("addgame")]
        public async Task AddGameAsync(string gameName)
        {
            Game game = await Game.Find(gameName);
            if (game == null)
            {
                await ReplyAsync("Couldn't locate game, please supply a valid speedrun.com url or game id");
                return;
            }

            ConfigChannel channel = Context.Bot.Config.Channels.FirstOrDefault(c => c.Id == Context.Channel.Id);
            if (channel == null)
            {
                channel = new ConfigChannel
                {
                    Id = Context.Channel.Id
                };

                Context.Bot.Config.Channels.Add(channel);
            }

            if (channel.Games.Any(g => g.Id == game.Id))
            {
                await ReplyAsync($"Game '{game.Name} is already being tracked in this channel'");
                return;
            }

            channel.Games.Add(new ConfigGame { Id = game.Id });
            Context.Bot.Config.Save("config.json");

            await ReplyAsync($"Now tracking game '{game.Name}'");
        }

        [Command("removegame", "deletegame", "remgame", "delgame")]
        public async Task RemoveGameAsync(string gameName)
        {
            Game game = await Game.Find(gameName);
            if (game == null)
            {
                await ReplyAsync("Couldn't locate game, please supply a valid speedrun.com url or game id");
                return;
            }

            ConfigChannel confChannel = Context.Bot.Config.Channels.FirstOrDefault(c => c.Id == Context.Channel.Id);
            ConfigGame confGame = confChannel?.Games?.FirstOrDefault(g => g.Id == game.Id);
            if (confGame == null)
            {
                await ReplyAsync($"Game '{game.Name} is not being tracked in this channel'");
                return;
            }

            foreach (ConfigRun run in confGame.Runs)
            {
                RestMessage msg = await Context.Channel.GetMessageAsync(run.MsgId);
                if (msg?.Author?.Id != Context.Bot.CurrentUser.Id)
                {
                    continue;
                }

                await msg.DeleteAsync();
            }

            confChannel.Games.Remove(confGame);
            Context.Bot.Config.Save("config.json");

            await ReplyAsync($"No longer tracking game '{game.Name}' in this channel");
        }

        [Command("ping")]
        public Task PingAsync()
            => ReplyAsync("pong");
    }
}
