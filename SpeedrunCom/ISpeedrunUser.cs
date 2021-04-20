using System.Collections.Generic;
using System.Threading.Tasks;

namespace VerificationBot.SpeedrunCom
{
    public interface ISpeedrunUser
    {
        string Id { get; }
        string Name { get; }
        string Link { get; }

        Task<IList<Game>> GetModeratedGamesAsync();
        Task<string> GetDiscordAsync();
    }
}
