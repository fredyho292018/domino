using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Ads
{
    public sealed class UnityRewardIntentCodec : IRewardIntentCodec, IRewardConsumptionCodec
    {
        public RewardIntentReceipt Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 8192) throw new FormatException();
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new FormatException();
            string Text(string key) => root[key]?.Type == JTokenType.String ? root[key].Value<string>() : throw new FormatException();
            var reward = root["reward"] as JObject;
            if (reward?["type"]?.Value<string>() != "COINS" || reward["previewAmount"]?.Type != JTokenType.Integer ||
                reward["previewAmount"].Value<long>() < 1 || reward["previewAmount"].Value<long>() > 9007199254740991L) throw new FormatException();
            if (!DateTimeOffset.TryParse(Text("expiresAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var expires)) throw new FormatException();
            return new RewardIntentReceipt(Text("intentId"), Text("status"), expires, reward["previewAmount"].Value<long>());
        }
        static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 8192) throw new FormatException();
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new FormatException();
            return root;
        }
        public RewardConsumeReceipt ReadConsume(string json)
        {
            var root = Parse(json);
            if (root["intent"]?["status"]?.Type != JTokenType.String || (string)root["intent"]["status"] != "CONSUMED" ||
                root["intent"]?["intentId"]?.Type != JTokenType.String ||
                root["reward"]?["type"]?.Type != JTokenType.String || (string)root["reward"]["type"] != "COINS" ||
                root["reward"]?["amount"]?.Type != JTokenType.Integer || root["wallet"]?["coins"]?.Type != JTokenType.Integer)
                throw new FormatException();
            return new RewardConsumeReceipt((string)root["intent"]["intentId"], root["reward"]["amount"].Value<long>(), root["wallet"]["coins"].Value<long>());
        }
        public RewardIntentReceipt ReadPending(string json)
        {
            var root = Parse(json);
            if (!root.TryGetValue("intent", out var intent)) throw new FormatException();
            if (intent.Type == JTokenType.Null) return null;
            var value = Read(intent.ToString(Formatting.None));
            if (value.Status != "VERIFIED") throw new FormatException();
            return value;
        }
    }
}
