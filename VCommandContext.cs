using System;
using Disqord.Bot;
using Disqord.Gateway;

namespace VerificationBot
{
    public class VCommandContext : DiscordGuildCommandContext
    {
        public new VerificationBot Bot { get; }

        public VCommandContext(VerificationBot bot, IPrefix prefix, IGatewayUserMessage message, CachedTextChannel channel, IServiceProvider services)
            : base(bot, prefix, message, channel, services)
            => Bot = bot;
    }
}
