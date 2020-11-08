using System;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    internal static class JsonExtensions
    {
        public static bool TryGetValue<T>(this JObject self, string key, out T value) where T : JToken
        {
            if (!self.TryGetValue(key, out JToken tok) || (value = tok as T) == default)
            {
                value = default;
                return false;
            }

            return true;
        }

        public static JTryGet TryGet(this JObject self)
            => new JTryGet(self);

        public class JTryGet
        {
            private readonly JToken Tok;
            
            public JTryGet(JToken obj)
            {
                Tok = obj ?? throw new ArgumentNullException(nameof(obj));
            }

            public T As<T>() where T : JToken
                => Tok as T;

            public JTryGet this[string name]
            {
                get
                {
                    if (!(Tok is JObject obj) || !obj.TryGetValue(name, out JToken childTok))
                    {
                        return null;
                    }

                    return new JTryGet(childTok);
                }
            }

            public override string ToString() => Tok.ToString();

            public static implicit operator JToken(JTryGet j) => j.Tok;
        }
    }
}
