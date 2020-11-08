using System;
using System.Dynamic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VerificationBot.SpeedrunCom
{
    public static class ApiUrls
    {
        private const string BASE = "https://www.speedrun.com/api/v1/";

        public static string Series { get; private set; }
        public static string Games { get; private set; }
        public static string Platforms { get; private set; }
        public static string Regions { get; private set; }
        public static string Runs { get; private set; }
        public static string Users { get; private set; }

        public static async Task Fill()
        {
            using HttpClient http = new HttpClient();
            HttpResponseMessage resp = null;

            while (resp == null || !resp.IsSuccessStatusCode)
            {
                resp = await http.GetAsync(BASE);
            }

            dynamic content = JsonConvert.DeserializeObject<ExpandoObject>(await resp.Content.ReadAsStringAsync());
            foreach (dynamic link in content.data.links)
            {
                string rel = char.ToUpper(link.rel[0]) + link.rel.Substring(1).ToLower();
                string uri = link.uri;

                PropertyInfo prop = typeof(ApiUrls).GetProperty(rel, BindingFlags.Public | BindingFlags.Static);
                if (prop == null)
                {
                    Console.WriteLine($"Found unsupported API endpoint \"{uri}\"");
                    continue;
                }

                if (!uri.EndsWith('/'))
                {
                    uri += '/';
                }

                prop.SetValue(null, uri);
            }
        }
    }
}
