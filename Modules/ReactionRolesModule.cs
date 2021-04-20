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
    [Name("ReactionRoles")]
    [Description("Commands for configuring the react role system")]
    [RequireCustomPermissions(Permission.Administrator)]
    public class ReactionRolesModule : DiscordModuleBase<VCommandContext>
    {
        [Command("addreactrole")]
        [Description("Adds a reaction to the given message, that when clicked will add the given role")]
        [RequireBotGuildPermissions(Permission.ManageRoles)]
        public async Task AddReactRoleAsync(ulong channelId, ulong messageId, ulong roleId, string emoji)
        {
            if (!Context.Guild.Channels.TryGetValue(channelId, out IGuildChannel guildChannel))
            {
                guildChannel = (await Context.Guild.FetchChannelsAsync()).FirstOrDefault(c => c.Id == channelId);
                if (guildChannel == null)
                {
                    await Response($"Could not locate channel with id {channelId}");
                    return;
                }
            }

            if (guildChannel is not ITextChannel channel)
            {
                await Response($"Channel {guildChannel.Name} is not a text channel");
                return;
            }

            if (await channel.FetchMessageAsync(messageId) is not IMessage message)
            {
                await Response($"Could not locate message with id {messageId}");
                return;
            }

            IRole role = (await Context.Guild.FetchRolesAsync()).FirstOrDefault(r => r.Id == roleId);
            if (role == null)
            {
                await Response($"Could not locate role with id {roleId}");
                return;
            }

            IEmoji emote = ReactService.ParseEmojiString(emoji);

            try
            {
                await message.AddReactionAsync(emote);
            }
            catch
            {
                await Response("Failed adding react to message: " + emote.GetType().FullName + " " + emote.ToString());
                return;
            }

            Context.Bot.Config
                .GetOrAddGuild(Context.GuildId)
                .GetOrAddChannel(Context.ChannelId)
                .AddReactRole(messageId, emote, roleId);
        }

        [Command("removereactrole")]
        [Description("Stops tracking of reactions of the given emoji to the given message")]
        public async Task RemoveReactRoleAsync(ulong channelId, ulong messageId, string emoji)
        {
            if (Context.Bot.Config
                .GetOrAddGuild(Context.GuildId)
                .GetOrAddChannel(channelId)
                .TryRemoveReactRole(messageId, ReactService.ParseEmojiString(emoji)))
            {
                await Response("Reaction role granting removed");
            }
            else
            {
                await Response("Failed to remove reaction role. Maybe the parameters are incorrect?");
            }
        }
    }
}
