using System.Collections.Generic;
using System.Threading.Tasks;

namespace VerificationBot.SpeedrunCom
{
    public class GuestUser : ISpeedrunUser
    {
        public string Name { get; }

        public GuestUser(string name)
            => Name = name;

        // These members can't be populated for a guest user
        public string Id => string.Empty;
        public string Link => string.Empty;

        public Task<string> GetDiscordAsync() => Task.FromResult(string.Empty);

        private static readonly IList<Game> EmptyList = new List<Game>().AsReadOnly();
        public Task<IList<Game>> GetModeratedGamesAsync() => Task.FromResult(EmptyList);
    }
}
