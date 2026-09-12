using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Infrastructure.Api
{
    // Reuses Unity Localization's installed Newtonsoft package; no coercion of wallet amounts.
    public sealed class UnityApiJsonCodec : IApiJsonCodec
    {
        public string Serialize(PlayerBootstrapRequestDto request) =>
            new JObject { ["language"] = request.language }.ToString(Formatting.None);
        public PlayerBootstrapResponseDto ReadSuccess(string json)
        {
            var root = Parse(json);
            var player = root["player"] as JObject;
            var coins = (root["wallet"] as JObject)?["coins"];
            if (player == null || coins?.Type != JTokenType.Integer) throw new FormatException();
            return new PlayerBootstrapResponseDto {
                player = new PlayerResponseDto {
                    uid = Text(player, "uid"), accountType = Text(player, "accountType"), displayName = Text(player, "displayName"),
                    language = Text(player, "language"), status = Text(player, "status") },
                wallet = new WalletResponseDto { coins = coins.Value<long>() }
            };
        }
        public ApiErrorDto ReadError(string json)
        {
            var root = Parse(json);
            return new ApiErrorDto { code = Text(root, "code"), requestId = Text(root, "requestId") };
        }
        static string Text(JObject obj, string key)
        {
            if (obj[key]?.Type != JTokenType.String) throw new FormatException();
            return obj[key].Value<string>();
        }
        static JObject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) throw new FormatException();
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new FormatException();
            return root;
        }
    }
}
