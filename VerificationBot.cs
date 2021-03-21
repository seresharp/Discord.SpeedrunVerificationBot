using System;
using System.Collections.Concurrent;
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
using VerificationBot.SpeedrunCom;

using Timer = System.Timers.Timer;

namespace VerificationBot
{
    public class VerificationBot : DiscordBot
    {
        public Config Config { get; }
        public new ILogger Logger { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> AllCommandNames { get; }

        private readonly Timer UpdateTimer;

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

            // Setup update timer
            UpdateTimer = new Timer
            {
                AutoReset = true,
                Interval = TimeSpan.FromSeconds(5).TotalMilliseconds
            };

            UpdateTimer.Elapsed += UpdateBackgroundTasks;

            CommandExecutionFailed += OnCommandFailed;
            ReactionAdded += OnReactionAdded;
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

        private bool _checkingRuns = false;
        private bool _checkingUnmutes = false;
        private int _updateCount = 0;
        private void UpdateBackgroundTasks(object sender, System.Timers.ElapsedEventArgs e)
        {
            // 1 update = 5 seconds
            if (_updateCount % 12 == 0 && !_checkingRuns)
            {
                Task.Run(async () =>
                {
                    _checkingRuns = true;

                    try
                    {
                        await CheckRuns();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning("Failed checking runs: " + ex.ToString());
                    }

                    _checkingRuns = false;
                });
            }

            if (!_checkingUnmutes)
            {
                Task.Run(async () =>
                {
                    _checkingUnmutes = true;

                    try
                    {
                        await Services.MuteService.CheckUnmutes(Config, Guilds);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning("Failed checking unmutes: " + ex.ToString());
                    }

                    _checkingUnmutes = false;
                });
            }
        }

        private async Task CheckRuns()
        {
            foreach ((_, ConfigGuild confGuild) in Config.Guilds)
            {
                foreach ((ulong channelId, ConcurrentSet<string> gameIds) in confGuild.TrackedGames)
                {
                    if (await GetChannelAsync(channelId) is not RestTextChannel channel)
                    {
                        continue;
                    }

                    foreach (string gameId in gameIds)
                    {
                        if (await Game.Find(gameId) is not Game game)
                        {
                            continue;
                        }

                        Dictionary<string, ConfigRun> oldMessages = new(confGuild.RunMessages);

                        await foreach (Run run in game.GetRuns(RunStatus.New))
                        {
                            // Check for already existing message
                            if (oldMessages.TryGetValue(run.Id, out ConfigRun confRun))
                            {
                                RestMessage existingMsg = await channel.GetMessageAsync(confRun.MsgId);
                                if (existingMsg?.Author?.Id == CurrentUser.Id)
                                {
                                    oldMessages.Remove(run.Id);
                                    continue;
                                }
                            }

                            // Send new message
                            string time = run.Time.ToString(run.Time switch
                            {
                                { Days: > 0 } => "d'd 'hh':'mm':'ss'.'FFF",
                                { Hours: > 0 } => "hh':'mm':'ss'.'FFF",
                                _ => "mm':'ss'.'FFF"
                            }).TrimEnd('.');

                            if (time.StartsWith("0"))
                            {
                                time = time[1..];
                            }

                            RestMessage msg = await channel.SendMessageAsync(
                                $"{game.Name}: {run.GetFullCategory()} in {time} by {string.Join(", ", run.Players)}\n<{run.Link}>");
                            await msg.AddReactionAsync(new LocalCustomEmoji(774026811797405707, "claim_run"));

                            confGuild.RunMessages[run.Id] = new ConfigRun
                            {
                                MsgId = msg.Id,
                                RunId = run.Id
                            };
                        }

                        // Delete old messages that weren't handled in the above loop
                        // Only runs that aren't in queue anymore should make it here
                        foreach ((string runId, ConfigRun confRun) in oldMessages)
                        {
                            if (await channel.GetMessageAsync(confRun.MsgId) is not RestMessage msg
                                || msg.Author.Id != CurrentUser.Id)
                            {
                                continue;
                            }

                            await msg.DeleteAsync();
                            confGuild.RunMessages.Remove(runId, out _);
                        }
                    }
                }
            }

            await Config.Save(Program.CONFIG_FILE);
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
