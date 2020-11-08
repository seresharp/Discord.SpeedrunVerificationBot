using System;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Prefixes;

namespace VerificationBot
{
    public class VCommandContext : DiscordCommandContext
    {
        public new VerificationBot Bot { get; }

        public VCommandContext(VerificationBot bot, IPrefix prefix, CachedUserMessage message, IServiceProvider provider = null)
            : base(bot, prefix, message, provider)
            => Bot = bot;
    }
}
