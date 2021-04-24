using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class User : ISpeedrunUser
    {
        public static HttpClient Http { get; set; } = new HttpClient();

        private readonly JObject Data;

        private string _id;
        public string Id
            => _id ??= Data.TryGet()["id"]?.ToString() ?? string.Empty;

        private string _name;
        public string Name
            => _name ??= Data.TryGet()["names"]?["international"]?.ToString() ?? string.Empty;

        private string _link;
        public string Link
            => _link ??= Data.TryGet()["weblink"]?.ToString() ?? string.Empty;

        private string _gamesUrl;
        private string GamesUrl
        {
            get
            {
                if (_gamesUrl != null)
                {
                    return _gamesUrl;
                }

                JArray links = Data.TryGet()["links"]?.As<JArray>();

                if (links == null)
                {
                    return _gamesUrl = string.Empty;
                }

                return _gamesUrl ??= links
                    .FirstOrDefault(tok =>
                        tok is JObject obj
                        && obj.TryGet()["rel"]?.ToString() == "games"
                        && obj.ContainsKey("uri")
                    )?["uri"]?.ToString() ?? string.Empty;
            }
        }

        private List<Game> _games;
        public async Task<IList<Game>> GetModeratedGamesAsync()
        {
            if (_games != null)
            {
                return _games.AsReadOnly();
            }

            HttpResponseMessage resp = await Http.GetRateLimitedAsync(GamesUrl + "?requestTime=" + DateTime.UtcNow.Ticks);
            resp.EnsureSuccessStatusCode();

            JArray games = JObject.Parse(await resp.Content.ReadAsStringAsync()).TryGet()["data"].As<JArray>();

            _games = new List<Game>();
            foreach (JObject obj in games)
            {
                _games.Add(new Game(obj));
            }

            return _games.AsReadOnly();
        }

        private string _discord;
        public async Task<string> GetDiscordAsync()
        {
            const string DISCORD_SEARCH = "src=\"/images/socialmedia/discord.png\" data-id=\"";

            if (_discord != null)
            {
                return _discord;
            }

            HttpResponseMessage resp = await Http.GetAsync(Link + "?requestTime=" + DateTime.UtcNow.Ticks);
            resp.EnsureSuccessStatusCode();

            string html = resp.Content.ReadAsStringAsync().Result;
            int idx = html.IndexOf(DISCORD_SEARCH);
            if (idx == -1)
            {
                return _discord = string.Empty;
            }

            idx += DISCORD_SEARCH.Length;
            int nextQuote = html.IndexOf('"', idx);

            if (nextQuote == -1)
            {
                return _discord = string.Empty;
            }

            // Max name length is 32 chars + 5 for discriminator
            // Assuming it matched incorrect if length is too long
            string discordUnchecked = WebUtility.HtmlDecode(html[idx..nextQuote]);
            if (discordUnchecked.Length < 6 || discordUnchecked.Length > 37
                || discordUnchecked[discordUnchecked.Length - 5] != '#'
                || !int.TryParse(discordUnchecked[(discordUnchecked.Length - 4)..], out _))
            {
                return _discord = string.Empty;
            }

            return _discord = discordUnchecked;
        }

        public static async Task<User> FindById(string id)
        {
            HttpResponseMessage resp = await Http.GetRateLimitedAsync("https://www.speedrun.com/api/v1/users/" + id + "?requestTime=" + DateTime.UtcNow.Ticks);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            JObject obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            if (!obj.TryGetValue("data", out JObject data))
            {
                return null;
            }

            return new User(data);
        }

        public User(JObject data) => Data = data;
    }
}
