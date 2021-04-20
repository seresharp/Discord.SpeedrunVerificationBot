﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                foreach ((ulong channelId, ConcurrentSet<string> gameIds) in confGuild.TrackedGames)
                {
                    if (await bot.FetchChannelAsync(channelId) is not ITextChannel channel)
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

                        await foreach (Run run in game.GetRunsAsync(RunStatus.New))
                        {
                            // Check for already existing message
                            if (oldMessages.TryGetValue(run.Id, out ConfigRun confRun))
                            {
                                IMessage existingMsg = await channel.FetchMessageAsync(confRun.MsgId);
                                if (existingMsg?.Author?.Id == bot.CurrentUser.Id)
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
                            if (await channel.FetchMessageAsync(confRun.MsgId) is not IMessage msg
                                || msg.Author.Id != bot.CurrentUser.Id)
                            {
                                continue;
                            }

                            await msg.DeleteAsync();
                            confGuild.RunMessages.Remove(runId, out _);
                        }
                    }
                }
            }

            await bot.Config.Save(Program.CONFIG_FILE);
        }
    }
}
