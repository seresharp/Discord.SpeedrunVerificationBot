using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public static class HttpExtensions
    {
        private static readonly ConcurrentSet<long> RecentRequests = new();

        public static async Task<HttpResponseMessage> GetRateLimitedAsync(this HttpClient http, string requestUri)
        {
            if (RecentRequests.Count >= 100)
            {
                while (true)
                {
                    long currTime = Environment.TickCount64;
                    foreach (long time in RecentRequests)
                    {
                        if (currTime - time >= 60000)
                        {
                            RecentRequests.Remove(time);
                        }
                    }

                    if (RecentRequests.Count < 100)
                    {
                        break;
                    }

                    await Task.Delay(500);
                }
            }

            HttpResponseMessage resp = await http.GetAsync(requestUri);
            RecentRequests.Add(Environment.TickCount64);

            if ((int)resp.StatusCode == 420)
            {
                Console.WriteLine("Over 100 requests! Delaying");
                await Task.Delay(500);
                return await GetRateLimitedAsync(http, requestUri);
            }

            return resp;
        }
    }
}
