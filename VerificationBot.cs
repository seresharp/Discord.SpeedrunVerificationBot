using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Prefixes;
using Disqord.Events;
using Disqord.Rest;
using Microsoft.Extensions.Logging;
using Qmmands;
using VerificationBot.BackgroundTasks;

using Timer = System.Timers.Timer;

namespace VerificationBot
{
    public class VerificationBot : DiscordBot
    {
        public Config Config { get; }
        public new ILogger Logger { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> AllCommandNames { get; }

        private readonly Timer UpdateTimer;
        private readonly BackgroundTask[] BackgroundTasks;

        public VerificationBot(TokenType tokenType, Config config,
            ILogger logger, DiscordBotConfiguration configuration)
            : base(tokenType, config.Token, new VPrefixProvider(config), configuration)
        {
            Config = config;
            Logger = logger;
            AddModules(GetType().Assembly);

            // Create command name list
            Dictionary<string, IReadOnlyList<string>> modules = new();
            foreach (Module m in GetAllModules())
            {
                List<string> commands = new(m.Commands.Select(c => c.Name));
                modules[m.Name] = commands.AsReadOnly();
            }

            AllCommandNames = modules;

            // Setup background tasks
            BackgroundTasks = new BackgroundTask[]
            {
                new GenericTask(TimeSpan.FromSeconds(5), static bot => Services.MuteService.CheckUnmutes(bot.Config, bot.Guilds)),
                new CheckRunsTask(TimeSpan.FromMinutes(1))
            };

            // Setup update timer
            UpdateTimer = new Timer
            {
                AutoReset = true,
                Interval = TimeSpan.FromSeconds(5).TotalMilliseconds
            };

            UpdateTimer.Elapsed += UpdateBackgroundTasks;

            // Hook bot events
            CommandExecutionFailed += OnCommandFailed;
            ReactionAdded += OnReactionAdded;
        }

        private void UpdateBackgroundTasks(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (BackgroundTask task in BackgroundTasks)
            {
                task.TryStartTask(this);
            }
        }

        private async Task OnReactionAdded(ReactionAddedEventArgs e)
        {
            IUser user = await e.User.GetAsync();
            IMessage msg = await e.Message.GetAsync();

            if (user.Id == CurrentUser.Id || msg.Author.Id != CurrentUser.Id || e.Emoji is not CustomEmoji emoji)
            {
                return;
            }

            foreach (ConfigRun run in GetConfigRuns())
            {
                if (run.MsgId != e.Message.Id)
                {
                    continue;
                }

                if (run.ClaimedBy != default)
                {
                    break;
                }

                switch (emoji.Id)
                {
                    // Claim
                    case 774026811797405707:
                        run.ClaimedBy = user.Id;
                        await (await GetMessageAsync(msg.ChannelId, msg.Id) as RestUserMessage)
                            .ModifyAsync(m => m.Content = $"{msg.Content}\n**Claimed by {user.Name}**");

                        await msg.ClearReactionsAsync(e.Emoji);
                        break;
                }

                break;
            }
        }

        private IEnumerable<ConfigRun> GetConfigRuns()
        {
            foreach ((_, ConfigGuild guild) in Config.Guilds)
            {
                foreach ((_, ConfigRun run) in guild.RunMessages)
                {
                    yield return run;
                }
            }
        }

        private async Task OnCommandFailed(CommandExecutionFailedEventArgs e)
        {
            string msg = $"Failed executing command '{e.Result.Command.Name}'";

            Logger.LogWarning(msg + "\n" + e.Result.Exception.ToString());
            await ((DiscordCommandContext)e.Context).Channel.SendMessageAsync
            (
                new LocalAttachment(Encoding.ASCII.GetBytes(e.Result.Exception.ToString()), "error.txt"),
                msg
            );
        }

        protected override ValueTask<DiscordCommandContext> GetCommandContextAsync(CachedUserMessage message, IPrefix prefix)
            => new(new VCommandContext(this, prefix, message));

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimer.Start();

            await base.RunAsync(cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            UpdateTimer.Elapsed -= UpdateBackgroundTasks;
            UpdateTimer.Dispose();

            return base.DisposeAsync();
        }
    }
}
