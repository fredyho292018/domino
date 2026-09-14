using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Domino.Infrastructure.Api;

namespace Domino.Realtime
{
    public enum RealtimeConnectionState { DISCONNECTED, CONNECTING, AUTHENTICATING, CONNECTED, RECONNECTING }
    public sealed class GlobalActivitySnapshot
    {
        public long OnlinePlayers { get; }
        public long ActiveMatches { get; }
        public long WaitingPlayers { get; }
        public long OpenRooms { get; }
        public DateTime ReceivedAt { get; } = DateTime.UtcNow;
        public GlobalActivitySnapshot(long online, long matches, long waiting, long rooms)
        { OnlinePlayers = online; ActiveMatches = matches; WaitingPlayers = waiting; OpenRooms = rooms; }
    }
    public sealed class RealtimeConfiguration
    {
        public Uri Endpoint { get; }
        // Reuse the validated REST policy, including literal loopback and development restrictions.
        public RealtimeConfiguration(DominoApiConfiguration api)
        {
            if (api?.Endpoint == null) return;
            var uri = new UriBuilder(new Uri(api.Endpoint, "/ws/v1/realtime"));
            uri.Scheme = uri.Scheme == "https" ? "wss" : "ws";
            Endpoint = uri.Uri;
        }
    }
    internal sealed class RealtimeFailure : Exception
    {
        public string Code { get; }
        public bool Retryable { get; }
        public RealtimeFailure(string code, bool retryable = false) : base("Realtime unavailable") { Code = code; Retryable = retryable; }
    }
    internal static class RealtimeProtocol
    {
        public const int MaxBytes = 32768;
        public static string Write(string type, long sequence, JObject payload) => new JObject {
            ["type"] = type, ["version"] = 1, ["sequence"] = sequence,
            ["timestamp"] = DateTime.UtcNow.ToString("o"), ["payload"] = payload
        }.ToString(Formatting.None);
        public static JObject Read(string text, ref long previous)
        {
            try {
                // Match event batches include event -> payload -> hand -> tile (nine containers).
                // Keep G3's original depth bound for every other message family.
                using var reader = new JsonTextReader(new StringReader(text)) { MaxDepth = 12, DateParseHandling = DateParseHandling.None };
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || root.Count != 5 || root["type"]?.Type != JTokenType.String || root["version"]?.Type != JTokenType.Integer || (int)root["version"] != 1 ||
                    root["sequence"]?.Type != JTokenType.Integer || (long)root["sequence"] <= previous || root["payload"]?.Type != JTokenType.Object ||
                    root["timestamp"]?.Type != JTokenType.String || !DateTimeOffset.TryParse((string)root["timestamp"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    throw new RealtimeFailure("PROTOCOL");
                if((string)root["type"]!="MATCH_UPDATE" && ContainerDepth(root)>8)throw new RealtimeFailure("PROTOCOL");
                previous = (long)root["sequence"]; return root;
            } catch (RealtimeFailure) { throw; } catch { throw new RealtimeFailure("PROTOCOL"); }
        }
        static int ContainerDepth(JToken token)
        {
            int depth=0;
            if(token is JObject obj) {foreach(var p in obj.Properties())depth=Math.Max(depth,ContainerDepth(p.Value));return depth+1;}
            if(token is JArray array) {foreach(var item in array)depth=Math.Max(depth,ContainerDepth(item));return depth+1;}
            return 0;
        }
        public static GlobalActivitySnapshot Activity(JObject payload)
        {
            long Count(string key) {
                if (payload[key]?.Type != JTokenType.Integer || (long)payload[key] < 0) throw new RealtimeFailure("PROTOCOL");
                return (long)payload[key];
            }
            return new GlobalActivitySnapshot(Count("onlinePlayers"), Count("activeMatches"), Count("waitingPlayers"), Count("openRooms"));
        }
    }
}
