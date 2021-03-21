﻿using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using VerificationBot.Attributes;
using VerificationBot.Services;
using Qmmands;

namespace VerificationBot.Modules
{
    [Name("Moderation")]
    [Description("Commands used for server moderation")]
    [RequireCustomPermissions(Permission.Administrator)]
    public class ModerationModule : DiscordModuleBase<VCommandContext>
    {
        [Command("warn")]
        [Description("Sends the given user a warning in dms")]
        public async Task WarnUserAsync(ulong userId, params string[] reason)
        {
            RestMember user = await Context.Guild.GetMemberAsync(userId);
            if (user == null)
            {
                await ReplyAsync("User not found");
                return;
            }

            string baseReason = $"You have been warned in the server '{Context.Guild.Name}':\n";
            string reasonStr = string.Join(' ', reason);

            if (baseReason.Length + reasonStr.Length > 2000)
            {
                await ReplyAsync($"Failed to send warning, reason is too long ({reasonStr.Length} / {2000 - baseReason.Length})");
                return;
            }

            await user.SendMessageAsync(baseReason + reasonStr);
            await ReplyAsync($"{user.Name}#{user.Discriminator} has been warned");
        }

        [Command("mute")]
        [Description("Mutes the given user for the given period of time, with an optional reason sent in dms")]
        [RequireBotGuildPermissions(Permission.ManageRoles)]
        [RequireBotGuildPermissions(Permission.ManageChannels)]
        public async Task MuteUserAsync(ulong userId, string time, params string[] reason)
        {
            RestMember user = await Context.Guild.GetMemberAsync(userId);
            if (user == null)
            {
                await ReplyAsync("User not found");
                return;
            }

            // Convert to/from TimeSpan rather than using user input to ensure pretty formatting
            time = MuteService.GetTimeString(MuteService.ParseTime(time));

            string baseReason = $"You have been muted for {time} in the server {Context.Guild.Name}:\n";
            string reasonStr = string.Join(' ', reason);

            if (baseReason.Length + reasonStr.Length > 2000)
            {
                await ReplyAsync($"Failed to mute user, reason is too long ({reasonStr.Length} / {2000 - baseReason.Length})");
                return;
            }

            (bool success, string failureReason) = await MuteService.MuteUserAsync(Context.Guild, user, time, Context.Bot.Config);
            if (!success)
            {
                await ReplyAsync(failureReason);
                return;
            }

            try
            {
                await user.SendMessageAsync(baseReason + reasonStr);
            }
            catch
            {
                await ReplyAsync("Failed to send mute reason to user, they likely have the bot blocked or dms from this server disabled");
            }

            await ReplyAsync($"{user.Name}#{user.Discriminator} has been muted for {time}");
        }

        [Command("unmute")]
        [Description("Unmutes the given user")]
        [RequireBotGuildPermissions(Permission.ManageRoles)]
        [RequireBotGuildPermissions(Permission.ManageChannels)]
        public async Task UnmuteUserAsync(ulong userId)
        {
            RestMember user = await Context.Guild.GetMemberAsync(userId);
            if (user == null)
            {
                await ReplyAsync("User not found");
                return;
            }

            (bool success, string failureReason) = await MuteService.UnmuteUserAsync(Context.Guild, user, Context.Bot.Config);
            if (!success)
            {
                await ReplyAsync(failureReason);
                return;
            }

            await ReplyAsync($"{user.Name}#{user.Discriminator} has been unmuted");
        }

        [Command("listmutes")]
        [Description("Returns a list of currently muted users")]
        public async Task ViewMutesAsync()
        {
            LocalEmbedBuilder embed = new() { Title = "Muted users" };

            foreach ((_, Mute mute) in Context.Bot.Config.GetOrAddGuild(Context.Guild.Id).CurrentMutes)
            {
                if (await Context.Guild.GetMemberAsync(mute.UserId) is not RestMember user)
                {
                    continue;
                }

                embed.AddField($"{user.Name}#{user.Discriminator}", $"Time remaining: {MuteService.GetTimeString(mute.UnmuteTime - DateTime.Now)}");
            }

            await ReplyAsync("", false, embed.Build());
        }
    }
}