using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace VerificationBot
{
    public class Config
    {
        public string Token;
        public string Prefix;
        public List<ConfigChannel> Channels = new List<ConfigChannel>();

        private Config() { }

        public static Config Load(string file)
        {
            if (!File.Exists(file))
            {
                return new Config();
            }

            string json = File.ReadAllText(file);
            Config c = JsonConvert.DeserializeObject<Config>(json);
            return c;
        }

        public void Save(string file)
        {
            if (File.Exists(file))
            {
                File.Move(file, file + ".bak", true);
            }

            File.WriteAllText(file, JsonConvert.SerializeObject(this));
        }
    }

    public class ConfigChannel
    {
        public ulong Id;
        public List<ConfigGame> Games = new List<ConfigGame>();
    }

    public class ConfigGame
    {
        public string Id;
        public List<ConfigRun> Runs = new List<ConfigRun>();
    }

    public class ConfigRun
    {
        public ulong MsgId;
        public string RunId;
        public ulong ClaimedBy;
    }
}
