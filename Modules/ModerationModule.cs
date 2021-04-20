using System;
using System.Linq;
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
            IMember user = await Context.Guild.FetchMemberAsync(userId);
            if (user == null)
            {
                await Response("User not found");
                return;
            }

            string baseReason = $"You have been warned in the server '{Context.Guild.Name}':\n";
            string reasonStr = string.Join(' ', reason);

            if (baseReason.Length + reasonStr.Length > 2000)
            {
                await Response($"Failed to send warning, reason is too long ({reasonStr.Length} / {2000 - baseReason.Length})");
                return;
            }

            await user.SendMessageAsync
            (
                new LocalMessageBuilder()
                .WithContent(baseReason + reasonStr)
                .Build()
            );

            await Response($"{user.Name}#{user.Discriminator} has been warned");
        }

        [Command("mute")]
        [Description("Mutes the given user for the given period of time, with an optional reason sent in dms")]
        [RequireBotGuildPermissions(Permission.ManageRoles)]
        [RequireBotGuildPermissions(Permission.ManageChannels)]
        public async Task MuteUserAsync(ulong userId, string time, params string[] reason)
        {
            IMember user = await Context.Guild.FetchMemberAsync(userId);
            if (user == null)
            {
                await Response("User not found");
                return;
            }

            // Convert to/from TimeSpan rather than using user input to ensure pretty formatting
            time = MuteService.GetTimeString(MuteService.ParseTime(time));

            string baseReason = $"You have been muted for {time} in the server {Context.Guild.Name}:\n";
            string reasonStr = string.Join(' ', reason);

            if (baseReason.Length + reasonStr.Length > 2000)
            {
                await Response($"Failed to mute user, reason is too long ({reasonStr.Length} / {2000 - baseReason.Length})");
                return;
            }

            (bool success, string failureReason) = await MuteService.MuteUserAsync(Context.Guild, user, time, Context.Bot.Config);
            if (!success)
            {
                await Response(failureReason);
                return;
            }

            try
            {
                await user.SendMessageAsync
                (
                    new LocalMessageBuilder()
                    .WithContent(baseReason + reasonStr)
                    .Build()
                );
            }
            catch
            {
                await Response("Failed to send mute reason to user, they likely have the bot blocked or dms from this server disabled");
            }

            await Response($"{user.Name}#{user.Discriminator} has been muted for {time}");
        }

        [Command("unmute")]
        [Description("Unmutes the given user")]
        [RequireBotGuildPermissions(Permission.ManageRoles)]
        [RequireBotGuildPermissions(Permission.ManageChannels)]
        public async Task UnmuteUserAsync(ulong userId)
        {
            IMember user = await Context.Guild.FetchMemberAsync(userId);
            if (user == null)
            {
                await Response("User not found");
                return;
            }

            (bool success, string failureReason) = await MuteService.UnmuteUserAsync(Context.Guild, user, Context.Bot.Config);
            if (!success)
            {
                await Response(failureReason);
                return;
            }

            await Response($"{user.Name}#{user.Discriminator} has been unmuted");
        }

        [Command("listmutes")]
        [Description("Returns a list of currently muted users")]
        public async Task ViewMutesAsync()
        {
            LocalEmbedBuilder embed = new() { Title = "Muted users" };

            foreach ((_, Mute mute) in Context.Bot.Config.GetOrAddGuild(Context.Guild.Id).CurrentMutes)
            {
                if (await Context.Guild.FetchMemberAsync(mute.UserId) is not IMember user)
                {
                    continue;
                }

                embed.AddField($"{user.Name}#{user.Discriminator}", $"Time remaining: {MuteService.GetTimeString(mute.UnmuteTime - DateTime.Now)}");
            }

            await Response(embed);
        }

        [Command("relay")]
        [Description("Relays a message to the given channel")]
        public async Task RelayMessageAsync(ulong channelId, params string[] message)
        {
            if (!Context.Guild.Channels.TryGetValue(channelId, out IGuildChannel channel))
            {
                channel = (await Context.Guild.FetchChannelsAsync()).FirstOrDefault(c => c.Id == channelId);
            }

            if (channel is not ITextChannel textChannel)
            {
                await Response("Could not find given channel, or it's a voice channel");
                return;
            }

            await textChannel.SendMessageAsync
            (
                new LocalMessageBuilder()
                .WithContent(string.Join(' ', message))
                .Build()
            );

            await Response("Message relayed");
        }

        [Command("react")]
        [Description("Adds the given react to the given message")]
        public async Task AddReactAsync(ulong channelId, ulong messageId, string emoji)
        {
            IMessage msg = await Context.Bot.FetchMessageAsync(channelId, messageId);
            if (msg == null)
            {
                await Response("Message not found");
                return;
            }

            try
            {
                await msg.AddReactionAsync(ReactService.ParseEmojiString(emoji));
                await Response("React added");
            }
            catch
            {
                await Response("Failed adding react to message");
            }
        }
    }
}
