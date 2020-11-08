using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Prefixes;

namespace VerificationBot
{
    public class VPrefixProvider : IPrefixProvider
    {
        private readonly Config Config;

        public VPrefixProvider(Config config)
            => Config = config;

        public ValueTask<IEnumerable<IPrefix>> GetPrefixesAsync(CachedUserMessage message)
            => new ValueTask<IEnumerable<IPrefix>>(new IPrefix[]
                {
                    MentionPrefix.Instance,
                    new StringPrefix(Config.Prefix)
                });
    }
}
