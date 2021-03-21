using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VerificationBot
{
    public class Config
    {
        private static readonly SemaphoreSlim IOSemaphore = new(1, 1);

        public string Token;
        public ConcurrentDictionary<ulong, ConfigGuild> Guilds = new();

        private Config() { }

        public static async Task<Config> Load(string file)
        {
            await IOSemaphore.WaitAsync();

            try
            {
                if (!File.Exists(file))
                {
                    return new Config();
                }

                string json = File.ReadAllText(file);
                Config c = JsonConvert.DeserializeObject<Config>(json);
                return c;
            }
            finally
            {
                IOSemaphore.Release();
            }
        }

        public async Task Save(string file)
        {
            await IOSemaphore.WaitAsync();

            try
            {
                if (File.Exists(file))
                {
                    File.Move(file, file + ".bak", true);
                }

                File.WriteAllText(file, JsonConvert.SerializeObject(this));
            }
            finally
            {
                IOSemaphore.Release();
            }
        }

        public ConfigGuild GetOrAddGuild(ulong guildId)
            => Guilds.GetOrAdd(guildId, id => new() { Id = id });
    }

    public class ConfigGuild
    {
        public ulong Id;
        public string Prefix = ";";

        public ConcurrentDictionary<ulong, ConcurrentSet<string>> TrackedGames = new();
        public ConcurrentDictionary<string, ConfigRun> RunMessages = new();

        public ConcurrentDictionary<ulong, Mute> CurrentMutes = new();
        public ConcurrentDictionary<ulong, ConcurrentSet<string>> UserPermOverrides = new();
        public ConcurrentDictionary<ulong, ConcurrentSet<string>> RolePermOverrides = new();

        public ConcurrentSet<string> GetOrAddGameList(ulong channelId)
            => TrackedGames.GetOrAdd(channelId, _ => new());

        public ConcurrentSet<string> GetOrAddUserPerms(ulong userId)
            => UserPermOverrides.GetOrAdd(userId, _ => new());

        public ConcurrentSet<string> GetOrAddRolePerms(ulong roleId)
            => RolePermOverrides.GetOrAdd(roleId, _ => new());
    }

    public class ConfigRun
    {
        public ulong MsgId;
        public string RunId;
        public ulong ClaimedBy;
    }

    public class Mute
    {
        public ulong GuildId;
        public ulong UserId;
        public ulong[] RoleIds;
        public DateTime UnmuteTime;
    }
}
