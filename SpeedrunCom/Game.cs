﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class Game
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

        public Game(JObject data) => Data = data;

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
            HttpResponseMessage resp = await Http.GetAsync("https://www.speedrun.com/api/v1/games/" + name);
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

        public async IAsyncEnumerable<Run> GetRuns(RunStatus status = RunStatus.Any)
        {
            string url = RunsUrl + "&orderby=submitted&direction=desc&embed=category.variables,players,level&max=200";
            // Runs are fetched suspiciously quickly after first call
            // Don't think NoCache header works properly, adding some garbage on the url instead
            url += "&requestTime=" + DateTime.UtcNow.Ticks;

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

            await foreach (Run run in GetRuns(url))
            {
                yield return run;
            }
        }

        private async IAsyncEnumerable<Run> GetRuns(string url)
        {
            JObject contentObj;
            using (HttpResponseMessage resp = await Http.GetAsync(url))
            {
                resp.EnsureSuccessStatusCode();

                // Check for data on current page
                // This should always exist unless something goes wrong
                contentObj = JObject.Parse(await resp.Content.ReadAsStringAsync());
            }

            if (!contentObj.TryGetValue("data", out JArray data))
            {
                throw new InvalidDataException();
            }

            foreach (JObject obj in data)
            {
                yield return new Run(this, obj);
            }

            // Add data from next page recursively
            if (contentObj.TryGetValue("pagination", out JObject pagination)
                && pagination.TryGetValue("links", out JArray links))
            {
                string nextUrl = links
                    .FirstOrDefault(tok =>
                        tok is JObject obj
                        && obj.TryGet()["rel"]?.ToString() == "next"
                        && obj.ContainsKey("uri")
                    )?["uri"]?.ToString();

                // Set stuff null to mark it for GC before recursing
                contentObj = null;
                data = null;
                pagination = null;
                links = null;

                if (nextUrl != null)
                {
                    await foreach (Run run in GetRuns(nextUrl))
                    {
                        yield return run;
                    }
                }
            }
        }
    }
}
