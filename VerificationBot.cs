using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Prefixes;
using Disqord.Events;
using Disqord.Rest;
using Microsoft.Extensions.Logging;
using Qmmands;
using VerificationBot.SpeedrunCom;

using Timer = System.Timers.Timer;

namespace VerificationBot
{
    public class VerificationBot : DiscordBot
    {
        public Config Config { get; }
        public new ILogger Logger { get; }

        private readonly Timer CheckRunsTimer;

        public VerificationBot(TokenType tokenType, Config config,
            ILogger logger, DiscordBotConfiguration configuration)
            : base(tokenType, config.Token, new VPrefixProvider(config), configuration)
        {
            Config = config;
            Logger = logger;
            AddModules(Assembly.GetExecutingAssembly());

            CheckRunsTimer = new Timer
            {
                AutoReset = true,
                Interval = TimeSpan.FromSeconds(20).TotalMilliseconds
            };

            CheckRunsTimer.Elapsed += CheckRunsWrapper;

            CommandExecutionFailed += OnCommandFailed;
            ReactionAdded += OnReactionAdded;
        }

        private async Task OnReactionAdded(ReactionAddedEventArgs e)
        {
            IUser user = await e.User.GetAsync();
            IMessage msg = await e.Message.GetAsync();

            if (user.Id == CurrentUser.Id || msg.Author.Id != CurrentUser.Id || !(e.Emoji is CustomEmoji emoji))
            {
                return;
            }

            foreach (ConfigRun run in GetRuns())
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

        private async void CheckRunsWrapper(object sender, ElapsedEventArgs e)
        {
            try
            {
                await CheckRuns();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed checking runs: " + ex.ToString());
            }
        }

        private async Task CheckRuns()
        {
            foreach (ConfigChannel confChannel in Config.Channels)
            {
                RestTextChannel channel = await GetChannelAsync(confChannel.Id) as RestTextChannel;
                if (channel == null)
                {
                    continue;
                }

                foreach (ConfigGame confGame in confChannel.Games)
                {
                    Game game = await Game.Find(confGame.Id);
                    if (game == null)
                    {
                        continue;
                    }

                    List<Run> runs = await game.GetRuns(RunStatus.New);

                    foreach (ConfigRun confRun in confGame.Runs.ToArray())
                    {
                        RestMessage msg = await channel.GetMessageAsync(confRun.MsgId);
                        if (msg == null || msg.Author.Id != CurrentUser.Id)
                        {
                            confGame.Runs.Remove(confRun);
                            continue;
                        }

                        Run run = runs.FirstOrDefault(r => r.Id == confRun.RunId);
                        if (run == null)
                        {
                            await msg.DeleteAsync();
                            confGame.Runs.Remove(confRun);
                            continue;
                        }
                    }

                    foreach (Run run in runs)
                    {
                        if (confGame.Runs.Any(cr => cr.RunId == run.Id))
                        {
                            continue;
                        }

                        string time = run.Time.ToString(@"d\d\ hh\:mm\:ss\.fff")
                            .Trim('0', 'd', ' ')
                            .TrimEnd('.')
                            .TrimStart(':');

                        if (time.StartsWith("00"))
                        {
                            time = time.Substring(1);
                        }

                        RestMessage msg = await channel.SendMessageAsync(
                            $"{game.Name}: {run.GetFullCategory()} in {time} by {string.Join(", ", run.Players)}\n<{run.Link}>");
                        await msg.AddReactionAsync(new LocalCustomEmoji(774026811797405707, "claim_run"));

                        confGame.Runs.Add(new ConfigRun
                        {
                            MsgId = msg.Id,
                            RunId = run.Id
                        });
                    }
                }
            }

            Config.Save("config.json");
        }

        private IEnumerable<ConfigRun> GetRuns()
        {
            foreach (ConfigChannel channel in Config.Channels)
            {
                foreach (ConfigGame game in channel.Games)
                {
                    foreach (ConfigRun run in game.Runs)
                    {
                        yield return run;
                    }
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
            => new ValueTask<DiscordCommandContext>(new VCommandContext(this, prefix, message));

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            await ApiUrls.Fill();
            CheckRunsTimer.Start();

            await base.RunAsync(cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            CheckRunsTimer.Dispose();
            return base.DisposeAsync();
        }
    }
}
