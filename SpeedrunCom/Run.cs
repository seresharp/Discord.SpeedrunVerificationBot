using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class Run
    {
        public static HttpClient Http { get; set; } = new HttpClient();

        private readonly JObject Data;
        public readonly Game Game;

        private string _id;
        public string Id
            => _id ??= Data.TryGet()["id"]?.ToString() ?? string.Empty;

        private string _link;
        public string Link
            => _link ??= Data.TryGet()["weblink"]?.ToString() ?? string.Empty;

        private string _level;
        public string Level
            => _level ??= Data.TryGet()["level"]?["data"]?["name"]?.ToString() ?? string.Empty;

        private string _category;
        public string Category
            => _category ??= Data.TryGet()["category"]?["data"]?["name"]?.ToString() ?? string.Empty;

        private TimeSpan? _time;
        public TimeSpan Time
            => _time ??= TimeSpan.FromSeconds(Convert.ToDouble(Data.TryGet()["times"]?["primary_t"]?.As<JValue>()?.Value ?? 0.0));

        private RunStatus? _status;
        public RunStatus Status
            => _status ??= Enum.TryParse(Data.TryGet()["status"]?["status"]?.ToString(), out RunStatus s)
                ? s
                : RunStatus.New;

        private DateTime _submitted;
        public DateTime Submitted
            => _submitted == default
                ? _submitted = DateTime.TryParse(Data.TryGet()["submitted"]?.ToString(), out DateTime d)
                    ? d
                    : DateTime.UnixEpoch
                : _submitted;

        private DateTime _verified;
        public DateTime Verified
            => _verified == default
                ? _verified = DateTime.TryParse(Data.TryGet()["status"]?["verify-date"]?.ToString(), out DateTime d)
                    ? d
                    : DateTime.UnixEpoch
                : _verified;

        private string _video;
        public string Video
        {
            get
            {
                if (_video != null)
                {
                    return _video;
                }

                JObject videos = Data.TryGet()["videos"]?.As<JObject>();
                if ((bool)videos?.ContainsKey("text"))
                {
                    _video = videos["text"].ToString();
                }
                else if ((bool)videos?.ContainsKey("links"))
                {
                    List<string> videoLinks = new List<string>();
                    foreach (JObject obj in (videos["links"] as JArray) ?? new JArray())
                    {
                        string uri = obj.TryGet()["uri"]?.ToString();
                        if (uri != null)
                        {
                            videoLinks.Add(uri);
                        }
                    }

                    _video = string.Join("\n", videoLinks);
                }

                return _video;
            }
        }

        private IList<VariableValue> _variables;
        public IList<VariableValue> Variables
        {
            get
            {
                if (_variables != null)
                {
                    return _variables;
                }

                List<VariableValue> vars = new List<VariableValue>();

                JArray catVars = Data.TryGet()["category"]?["data"]?["variables"]?["data"]?.As<JArray>();
                JObject runVars = Data.TryGet()["values"]?.As<JObject>();

                if (catVars == null || runVars == null)
                {
                    return _variables = vars.AsReadOnly();
                }

                foreach (JProperty runVar in runVars.Properties())
                {
                    foreach (JObject catVar in catVars)
                    {
                        string catVarName = catVar.TryGet()["name"]?.ToString();
                        string catVarId = catVar.TryGet()["id"]?.ToString();
                        string catVarValue = catVar.TryGet()["values"]?["values"]?[runVar.Value.ToString()]?["label"]?.ToString();
                        bool? subcat = catVar.TryGet()["is-subcategory"]?.As<JValue>()?.Value as bool?;

                        if (catVarId != runVar.Name || catVarValue == null || catVarName == null || !subcat.HasValue)
                        {
                            continue;
                        }

                        vars.Add(new VariableValue(catVarName, catVarValue, subcat.Value));
                        break;
                    }
                }

                return _variables = vars.AsReadOnly();
            }
        }

        public Run(Game game, JObject data)
        {
            Game = game;
            Data = data;
        }

        private IList<ISpeedrunUser> _players;
        public async IAsyncEnumerable<ISpeedrunUser> GetPlayersAsync()
        {
            if (_players != null)
            {
                foreach (ISpeedrunUser user in _players)
                {
                    yield return user;
                }

                yield break;
            }

            List<ISpeedrunUser> players = new();

            JArray jPlayers = Data.TryGet()["players"]?.As<JArray>();
            if (jPlayers == null)
            {
                _players = players.AsReadOnly();
                yield break;
            }

            foreach (JObject p in jPlayers)
            {
                string rel = p.TryGet()["rel"]?.ToString();
                if (rel == "guest" && p.TryGet()["name"]?.ToString() is string name)
                {
                    players.Add(new GuestUser(name));
                }
                else if (rel == "user" && p.TryGet()["id"]?.ToString() is string id
                    && await User.FindById(id) is User user)
                {
                    yield return user;
                    players.Add(user);
                }
            }

            _players = players.AsReadOnly();
        }

        private User _examiner;
        public async Task<User> GetExaminer()
        {
            if (_examiner != null)
            {
                return _examiner;
            }

            string examinerId = Data.TryGet()["status"]?["examiner"]?.ToString();
            if (examinerId == null)
            {
                return null;
            }

            return _examiner = await User.FindById(examinerId);
        }

        public string GetFullCategory()
        {
            StringBuilder cat = new StringBuilder();
            if (Level != string.Empty)
            {
                cat.Append(Level);
                cat.Append(": ");
            }

            cat.Append(Category);

            VariableValue[] vars = Variables.Where(v => v.IsSubcategory).ToArray();
            if (vars.Length > 0)
            {
                cat.Append(" - ");
                cat.Append(string.Join(", ", vars.Select(v => v.Value)));
            }

            return cat.ToString();
        }

        public async Task UpdateStatus(RunStatus status, string reason = null)
        {
            if (status != RunStatus.Verified && status != RunStatus.Rejected)
            {
                throw new ArgumentException("Cannot update a run's status to " + status.ToString());
            }

            if (status == RunStatus.Rejected && reason == null)
            {
                throw new ArgumentNullException("Must supply a reason for rejection");
            }

            StringBuilder content = new StringBuilder();
            content.Append("{\"status\":{\"status\":\"");
            content.Append(status.ToString().ToLower());
            content.Append("\"");

            if (status == RunStatus.Rejected)
            {
                content.Append(",\"reason\":\"");
                content.Append(reason);
                content.Append("\"");
            }

            content.Append("}}");
            HttpResponseMessage resp = await Http.PutAsync("https://speedrun.com/api/v1/runs/" + Id + "/status", new StringContent(content.ToString()));
            resp.EnsureSuccessStatusCode();
        }
    }

    public enum RunStatus
    {
        Any,
        New,
        Verified,
        Rejected
    }
}
