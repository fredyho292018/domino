using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Ads
{
    public sealed class UnityRewardIntentCodec : IRewardIntentCodec
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
                reward["previewAmount"].Value<long>() != 10) throw new FormatException();
            if (!DateTimeOffset.TryParse(Text("expiresAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var expires)) throw new FormatException();
            return new RewardIntentReceipt(Text("intentId"), Text("status"), expires);
        }
    }
}
