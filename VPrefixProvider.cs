using System.Collections.Generic;
using System.Threading.Tasks;
using Disqord.Bot;
using Disqord.Gateway;

namespace VerificationBot
{
    public class VPrefixProvider : IPrefixProvider
    {
        private readonly Config Config;

        public VPrefixProvider(Config config)
            => Config = config;

        public ValueTask<IEnumerable<IPrefix>> GetPrefixesAsync(IGatewayUserMessage message)
            => new ValueTask<IEnumerable<IPrefix>>(new IPrefix[]
                {
                    new StringPrefix(Config.GetOrAddGuild(message.GuildId?.RawValue ?? default).Prefix)
                });
    }
}
