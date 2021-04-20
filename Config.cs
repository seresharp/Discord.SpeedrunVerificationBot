using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
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

        public ConcurrentDictionary<ulong, ConfigChannel> Channels = new();

        // TODO: move these two into channel config (breaking changes yay)
        public ConcurrentDictionary<ulong, ConcurrentSet<string>> TrackedGames = new();
        public ConcurrentDictionary<string, ConfigRun> RunMessages = new();

        public ConcurrentDictionary<ulong, Mute> CurrentMutes = new();
        public ConcurrentDictionary<ulong, ConcurrentSet<string>> UserPermOverrides = new();
        public ConcurrentDictionary<ulong, ConcurrentSet<string>> RolePermOverrides = new();

        public ConfigChannel GetOrAddChannel(ulong channelId)
            => Channels.GetOrAdd(channelId, _ => new ConfigChannel { Id = channelId, GuildId = Id });

        public ConcurrentSet<string> GetOrAddGameList(ulong channelId)
            => TrackedGames.GetOrAdd(channelId, _ => new());

        public ConcurrentSet<string> GetOrAddUserPerms(ulong userId)
            => UserPermOverrides.GetOrAdd(userId, _ => new());

        public ConcurrentSet<string> GetOrAddRolePerms(ulong roleId)
            => RolePermOverrides.GetOrAdd(roleId, _ => new());
    }

    public class ConfigChannel
    {
        public ulong Id;
        public ulong GuildId;

        public ConcurrentSet<ulong> ChangelogChannels = new();

        [JsonProperty]
        private ConcurrentDictionary<ulong, ConcurrentDictionary<string, ulong>> ReactRoles = new();

        public void AddReactRole(ulong msgId, IEmoji emoji, ulong roleId)
        {
            if (emoji == null)
            {
                return;
            }

            if (!ReactRoles.TryGetValue(msgId, out var reacts))
            {
                reacts = new ConcurrentDictionary<string, ulong>();
                ReactRoles[msgId] = reacts;
            }

            reacts[emoji.ToString()] = roleId;
        }

        public bool TryRemoveReactRole(ulong msgId, IEmoji emoji)
        {
            if (!ReactRoles.TryGetValue(msgId, out var reacts)
                || !reacts.ContainsKey(emoji.ToString()))
            {
                return false;
            }

            return reacts.TryRemove(emoji.ToString(), out _);
        }

        public bool TryGetRoleForReact(ulong msgId, IEmoji emoji, out ulong roleId)
        {
            if (emoji == null)
            {
                roleId = 0;
                return false;
            }

            if (ReactRoles.TryGetValue(msgId, out var reacts)
                && reacts.TryGetValue(emoji.ToString(), out roleId))
            {
                return true;
            }

            roleId = 0;
            return false;
        }
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
