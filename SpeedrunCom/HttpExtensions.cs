using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace System.Net.Http
{
    public static class HttpExtensions
    {
        private static readonly ConcurrentDictionary<long, byte> RecentRequests = new();

        public static async Task<HttpResponseMessage> GetRateLimitedAsync(this HttpClient http, string requestUri)
        {
            if (RecentRequests.Count >= 100)
            {
                while (true)
                {
                    long currTime = Environment.TickCount64;
                    foreach ((long time, _) in RecentRequests)
                    {
                        if (currTime - time >= 60000)
                        {
                            RecentRequests.TryRemove(time, out _);
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
            RecentRequests.TryAdd(Environment.TickCount64, default);

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
