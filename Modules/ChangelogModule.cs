using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using VerificationBot.Attributes;
using Qmmands;

namespace VerificationBot.Modules
{
    [Name("Changelog")]
    [Description("Commands used for tracking message edits/deletes")]
    [RequireCustomPermissions(Permission.Administrator)]
    public class ChangelogModule : DiscordModuleBase<VCommandContext>
    {
        [Command("addchangelog")]
        [Description("Begins tracking of message edits/deletes in the current channel")]
        public async Task AddChangelogAsync(params ulong[] channelIds)
        {
            if (channelIds.Length == 0)
            {
                channelIds = Context.Guild.Channels
                    .Where(p => p.Value is ITextChannel)
                    .Select(p => p.Key.RawValue)
                    .ToArray();
            }

            Context.Bot.Config
                .GetOrAddGuild(Context.GuildId)
                .GetOrAddChannel(Context.ChannelId)
                .ChangelogChannels
                .AddRange(channelIds);

            await Response($"Added tracking for {channelIds.Length} channels");
        }

        [Command("removechangelog")]
        [Description("Removes tracking of message edits/deletes in the current channel")]
        public async Task RemoveChangelogAsync(params ulong[] channelIds)
        {
            if (channelIds.Length == 0)
            {
                channelIds = Context.Guild.Channels
                    .Where(p => p.Value is ITextChannel)
                    .Select(p => p.Key.RawValue)
                    .ToArray();
            }

            Context.Bot.Config
                .GetOrAddGuild(Context.GuildId)
                .GetOrAddChannel(Context.ChannelId)
                .ChangelogChannels
                .RemoveRange(channelIds);

            await Response($"Removed tracking for {channelIds.Length} channels");
        }
    }
}
