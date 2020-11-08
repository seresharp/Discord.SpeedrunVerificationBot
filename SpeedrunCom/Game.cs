using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class Game
    {
        private static readonly HttpClient Http;
        private static readonly Random Rnd = new Random();

        static Game()
        {
            Http = new HttpClient();
            Http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };
        }

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

        private string _runsUrl;
        private string RunsUrl
        {
            get
            {
                if (_runsUrl != null)
                {
                    return _runsUrl;
                }

                JArray links = Data.TryGet()["links"]?.As<JArray>();

                if (links == null)
                {
                    return _runsUrl = string.Empty;
                }

                return _runsUrl ??= links
                    .FirstOrDefault(tok => 
                        tok is JObject obj
                        && obj.TryGet()["rel"]?.ToString() == "runs"
                        && obj.ContainsKey("uri")
                    )?["uri"]?.ToString() ?? string.Empty;
            }
        }

        protected Game(JObject data) => Data = data;

        public static async Task<Game> Find(string name)
        {
            // Attempt to pull id out of urls
            if (Uri.TryCreate(name, UriKind.Absolute, out Uri uri))
            {
                name = uri.AbsolutePath;
                if (name.Contains('/'))
                {
                    name = name.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }
            }

            if (name == null)
            {
                return null;
            }

            // Attempt to fetch game data
            HttpResponseMessage resp = await Http.GetAsync(ApiUrls.Games + name);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            JObject obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            if (!obj.TryGetValue("data", out JObject data))
            {
                return null;
            }

            return new Game(data);
        }

        public async Task<List<Run>> GetRuns(RunStatus status = RunStatus.Any)
        {
            string url = RunsUrl + "&embed=category.variables,players,level&max=200&nocache=" + Rnd.Next(int.MaxValue);
            switch (status)
            {
                case RunStatus.New:
                    url += "&status=new";
                    break;
                case RunStatus.Verified:
                    url += "&status=verified";
                    break;
                case RunStatus.Rejected:
                    url += "&status=rejected";
                    break;
                default:
                    break;
            }

            return await GetRuns(url);
        }

        private async Task<List<Run>> GetRuns(string url)
        {
            List<Run> runs = new List<Run>();
            HttpResponseMessage resp = await Http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            // Add data from next page recursively
            JObject contentObj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            if (contentObj.TryGetValue("pagination", out JObject pagination)
                && pagination.TryGetValue("links", out JArray links))
            {
                string nextUrl = links
                    .FirstOrDefault(tok =>
                        tok is JObject obj
                        && obj.TryGet()["rel"]?.ToString() == "next"
                        && obj.ContainsKey("uri")
                    )?["uri"]?.ToString();

                if (nextUrl != null)
                {
                    runs.AddRange(await GetRuns(nextUrl));
                }
            }

            // Check for data on current page
            // This should always exist unless something goes wrong
            if (!contentObj.TryGetValue("data", out JArray data))
            {
                throw new InvalidDataException();
            }

            // Reverse each page + recursion for deepest first = most recent runs first
            foreach (JObject obj in data.Reverse())
            {
                runs.Add(new Run(obj));
            }

            return runs;
        }
    }
}
