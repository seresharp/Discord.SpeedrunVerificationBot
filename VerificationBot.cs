using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using VerificationBot.BackgroundTasks;
using VerificationBot.Services;

using Timer = System.Timers.Timer;

namespace VerificationBot
{
    public class VerificationBot : DiscordBot, IDisposable
    {
        public Config Config { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> AllCommandNames { get; }

        private readonly Timer UpdateTimer;
        private readonly BackgroundTask[] BackgroundTasks;

        public VerificationBot
        (
            IOptions<DiscordBotConfiguration> options,
            ILogger<DiscordBot> logger,
            IPrefixProvider prefixes,
            ICommandQueue queue,
            CommandService commands,
            IServiceProvider services,
            DiscordClient client
        ) : base(options, logger, prefixes, queue, commands, services, client)
        {
            Config = services.GetService(typeof(Config)) as Config;

            // Create command name list
            Dictionary<string, IReadOnlyList<string>> modules = new();
            foreach (Module m in commands.GetAllModules())
            {
                List<string> commandNames = new(m.Commands.Select(c => c.Name));
                modules[m.Name] = commandNames.AsReadOnly();
            }

            AllCommandNames = modules;

            // Setup background tasks
            BackgroundTasks = new BackgroundTask[]
            {
                new GenericTask(TimeSpan.FromSeconds(5), static bot => MuteService.CheckUnmutes(bot.Config, bot.GetGuilds())),
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
            commands.CommandExecutionFailed += OnCommandFailed;
            ReactionAdded += OnReactionAdded;
        }

        private void UpdateBackgroundTasks(object sender, System.Timers.ElapsedEventArgs e)
        {
            foreach (BackgroundTask task in BackgroundTasks)
            {
                task.TryStartTask(this);
            }
        }

        private async Task OnReactionAdded(object sender, ReactionAddedEventArgs e)
        {
            IMember member = e?.Member;
            IMessage message = e?.Message ?? await this.FetchMessageAsync(e.ChannelId, e.MessageId);

            if (member == null || message == null
                || await message.FetchChannelAsync() is not IMessageChannel channel
                || member.Id == CurrentUser.Id
                || message.Author.Id != CurrentUser.Id)
            {
                return;
            }

            if (e.Emoji is not CustomEmoji emoji)
            {
                return;
            }

            foreach (ConfigRun run in GetConfigRuns())
            {
                if (run.MsgId != message.Id)
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
                        run.ClaimedBy = member.Id;
                        await (await this.FetchMessageAsync(message.ChannelId, message.Id) as IUserMessage)
                            .ModifyAsync(m => m.Content = $"{message.Content}\n**Claimed by {member.Name}**");

                        await message.ClearReactionsAsync(emoji);
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
            await ((VCommandContext)e.Context).Message.GetChannel().SendMessageAsync
            (
                new LocalMessageBuilder()
                    .AddAttachment(new LocalAttachment(Encoding.ASCII.GetBytes(e.Result.Exception.ToString()), "error.txt"))
                    .WithContent(msg)
                    .Build()
            );
        }

        protected override DiscordCommandContext CreateCommandContext(IPrefix prefix, IGatewayUserMessage message, CachedTextChannel channel)
            => new VCommandContext(this, prefix, message, channel, Services);

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimer.Start();

            await base.RunAsync(cancellationToken);
        }

        public new void Dispose()
        {
            if (UpdateTimer != null)
            {
                UpdateTimer.Elapsed -= UpdateBackgroundTasks;
                UpdateTimer.Dispose();
            }

            base.Dispose();
        }
    }
}
