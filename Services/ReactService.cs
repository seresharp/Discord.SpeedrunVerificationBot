using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.Extensions.Logging;

namespace VerificationBot.Services
{
    public static class ReactService
    {
        public static async Task HandleReactAddedAsync(VerificationBot bot, ReactionAddedEventArgs e)
        {
            // Guild id is null if the bot has no channel access (why does the api even send that?)
            if (e.GuildId == null || e.Member.Id == bot.CurrentUser.Id)
            {
                return;
            }

            ulong guildId = e.GuildId.Value;

            if (e.Emoji is CustomEmoji { Id: { RawValue: 774026811797405707 } })
            {
                ConfigRun run = bot.Config
                    .GetOrAddGuild(guildId)
                    .GetOrAddChannel(e.ChannelId)
                    .RunMessages.FirstOrDefault(r => r.Value.MsgId == e.MessageId).Value;

                if (run == null || run.ClaimedBy != default
                    || (e.Message ?? await bot.GetMessageAsync(e.ChannelId, e.MessageId)) is not IUserMessage msg
                    || msg.Author.Id != bot.CurrentUser.Id)
                {
                    return;
                }

                await msg.ModifyAsync(m => m.Content = $"{msg.Content}\n**Claimed by {e.Member.Name}**");
                await msg.ClearReactionsAsync(e.Emoji);
            }
            else if (bot.Config
                .GetOrAddGuild(guildId)
                .GetOrAddChannel(e.ChannelId)
                .TryGetRoleForReact(e.MessageId, e.Emoji, out ulong roleId))
            {
                try
                {
                    await e.Member.GrantRoleAsync(roleId);
                }
                catch
                {
                    // Kill the react if role adding fails
                    IMessage msg = e.Message ?? await bot.GetMessageAsync(e.ChannelId, e.MessageId);
                    if (msg == null)
                    {
                        return;
                    }

                    await msg.RemoveReactionAsync(e.Emoji, e.Member.Id);
                }
            }
        }

        public static async Task HandleReactRemovedAsync(VerificationBot bot, ReactionRemovedEventArgs e)
        {
            // Guild id is null if the bot has no channel access (why does the api even send that?)
            if (e.GuildId == null)
            {
                return;
            }

            ulong guildId = e.GuildId.Value;

            if (bot.Config
                .GetOrAddGuild(guildId)
                .GetOrAddChannel(e.ChannelId)
                .TryGetRoleForReact(e.MessageId, e.Emoji, out ulong roleId))
            {
                try
                {
                    IGuild guild = await bot.GetGuildAsync(guildId);
                    IMember member = await guild.FetchMemberAsync(e.UserId);
                    await member.RevokeRoleAsync(roleId);
                }
                catch (Exception ex)
                {
                    // Can't add the react back, so just log the error and silently fail on the user side I guess
                    bot.Logger.LogError(ex.ToString());
                }
            }
        }

        public static IEmoji ParseEmojiString(string str)
        {
            Match match = Regex.Match(str, "<:(?<name>.+):(?<id>.+)>");
            return match.Success && ulong.TryParse(match.Groups["id"].Value, out ulong id)
                ? new LocalCustomEmoji(id, match.Groups["name"].Value)
                : new LocalEmoji(str);
        }
    }
}
