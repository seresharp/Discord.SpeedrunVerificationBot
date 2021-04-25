using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Rest;
using VerificationBot.SpeedrunCom;

namespace VerificationBot.BackgroundTasks
{
    public class CheckRunsTask : BackgroundTask
    {
        private TimeSpan _delay;
        public override TimeSpan Delay => _delay;

        public CheckRunsTask(TimeSpan delay)
            => _delay = delay;

        protected override async Task Run(VerificationBot bot)
        {
            foreach ((_, ConfigGuild confGuild) in bot.Config.Guilds)
            {
                foreach ((ulong channelId, ConfigChannel confChannel) in confGuild.Channels)
                {
                    if (await bot.GetChannelAsync(confGuild.Id, channelId) is not ITextChannel channel)
                    {
                        continue;
                    }

                    // This is reused between games within channels, so needs to be declared in this scope
                    // But no point setting it before it's needed, might result in unnecessary api calls
                    // This is necessary because disqord doesn't ever cache messages from before the bot started
                    Dictionary<ulong, IMessage> messages = null;

                    foreach (string gameId in confChannel.TrackedGames)
                    {
                        if (await Game.Find(gameId) is not Game game)
                        {
                            continue;
                        }

                        Dictionary<string, ConfigRun> oldMessages = new(confChannel.RunMessages);

                        await foreach (Run run in game.GetRunsAsync(RunStatus.New))
                        {
                            // Found a game with runs, message cache is now needed
                            if (messages == null)
                            {
                                messages = (await channel.FetchMessagesAsync()).ToDictionary(m => m.Id.RawValue);
                            }

                            // Check for already existing message
                            if (oldMessages.TryGetValue(run.Id, out ConfigRun confRun))
                            {
                                if (!messages.TryGetValue(confRun.MsgId, out IMessage existingMsg))
                                {
                                    existingMsg = await bot.GetMessageAsync(channel.Id, confRun.MsgId);
                                }

                                if (existingMsg == null)
                                {
                                    // Not removing null messages causes manually deleted ones to be immediately forgotten on repost
                                    oldMessages.Remove(run.Id);
                                }
                                else if (existingMsg.Author.Id == bot.CurrentUser.Id)
                                {
                                    // If there's already a message, no need to continue on to posting a new one
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

                            // Turns ugly leading 00: into 0:
                            if (time.StartsWith("0"))
                            {
                                time = time[1..];
                            }

                            List<string> players = new();
                            await foreach (ISpeedrunUser user in run.GetPlayersAsync())
                            {
                                players.Add(user.Name);
                            }

                            IMessage msg = await channel.SendMessageAsync
                            (
                                new LocalMessageBuilder()
                                    .WithContent($"{game.Name}: {run.GetFullCategory()} in {time} by {string.Join(", ", players)}\n<{run.Link}>")
                                    .Build()
                            );

                            // TODO: better system than hard-coded react
                            await msg.AddReactionAsync(new LocalCustomEmoji(774026811797405707, "claim_run"));

                            confChannel.RunMessages[run.Id] = new ConfigRun
                            {
                                MsgId = msg.Id,
                                RunId = run.Id
                            };
                        }

                        // Delete old messages that weren't handled in the above loop
                        // Only runs that aren't in queue anymore should make it here
                        foreach ((string runId, ConfigRun confRun) in oldMessages)
                        {
                            confChannel.RunMessages.Remove(runId, out _);

                            if (!messages.TryGetValue(confRun.MsgId, out IMessage msg))
                            {
                                msg = await bot.GetMessageAsync(channel.Id, confRun.MsgId);
                            }

                            if (msg == null || msg.Author.Id != bot.CurrentUser.Id)
                            {
                                continue;
                            }

                            await msg.DeleteAsync();
                        }
                    }
                }
            }
        }
    }
}
