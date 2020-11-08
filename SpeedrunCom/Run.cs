using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class Run
    {
        private readonly JObject Data;

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

        private IList<string> _players;
        public IList<string> Players
        {
            get
            {
                if (_players != null)
                {
                    return _players;
                }

                List<string> players = new List<string>();

                JArray jPlayers = Data.TryGet()["players"]?["data"]?.As<JArray>();
                if (jPlayers == null)
                {
                    return _players = players.AsReadOnly();
                }

                foreach (JObject p in jPlayers)
                {
                    string name = p.TryGet()["names"]?["international"]?.ToString();
                    if (name != null)
                    {
                        players.Add(name);
                    }
                }

                return _players = players.AsReadOnly();
            }
        }

        private IList<Variable> _variables;
        public IList<Variable> Variables
        {
            get
            {
                if (_variables != null)
                {
                    return _variables;
                }

                List<Variable> vars = new List<Variable>();

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

                        vars.Add(new Variable(catVarName, catVarValue, subcat.Value));
                        break;
                    }
                }

                return _variables = vars.AsReadOnly();
            }
        }

        internal Run(JObject data)
        {
            Data = data;
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

            Variable[] vars = Variables.Where(v => v.IsSubcategory).ToArray();
            if (vars.Length > 0)
            {
                cat.Append(" - ");
                cat.Append(string.Join(", ", vars.Select(v => v.Value)));
            }

            return cat.ToString();
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
