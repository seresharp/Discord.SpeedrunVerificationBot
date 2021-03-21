using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class User
    {
        public static HttpClient Http { get; set; } = new HttpClient();

        private readonly JObject Data;

        private string _id;
        public string Id
            => _id ??= Data.TryGet()["id"]?.ToString() ?? string.Empty;

        private string _name;
        public string Name
            => _name ??= Data.TryGet()["names"]?["international"]?.ToString() ?? string.Empty;

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
        public IList<Game> Games
        {
            get
            {
                if (_games != null)
                {
                    return _games.AsReadOnly();
                }

                JArray games = JObject.Parse(Http.GetStringAsync(GamesUrl).Result)
                    .TryGet()["data"].As<JArray>();

                _games = new List<Game>();
                foreach (JObject obj in games)
                {
                    _games.Add(new Game(obj));
                }

                return _games.AsReadOnly();
            }
        }

        private static Dictionary<string, User> _userCache = new Dictionary<string, User>();
        public static async Task<User> FindById(string id)
        {
            if (_userCache.TryGetValue(id, out User user))
            {
                return user;
            }

            HttpResponseMessage resp = await Http.GetAsync("https://www.speedrun.com/api/v1/users/" + id);
            if (!resp.IsSuccessStatusCode)
            {
                return _userCache[id] = null;
            }

            JObject obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            if (!obj.TryGetValue("data", out JObject data))
            {
                return _userCache[id] = null;
            }

            return _userCache[id] = new User(data);
        }

        public User(JObject data) => Data = data;
    }
}
