using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Domino.Social
{
    public enum SocialPresenceState { ONLINE, OFFLINE, IN_MATCH, UNKNOWN }

    // Screen-owned desired state. No identity authority, durable storage, or polling.
    public sealed class SocialPresenceStore
    {
        readonly HashSet<string> desired = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, SocialPresenceState> states = new Dictionary<string, SocialPresenceState>();
        long generation, revision;
        public event Action Changed;
        public long Generation => generation;
        public SocialPresenceState Get(string id) => id != null && states.TryGetValue(id, out var value) ? value : SocialPresenceState.UNKNOWN;
        public JObject Replace(IEnumerable<string> ids)
        {
            var next = ids.Distinct(StringComparer.Ordinal).ToArray();
            if (next.Length > 50 || next.Any(id => id == null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{22}$")))
                throw new ArgumentException("Invalid presence targets");
            desired.Clear(); foreach (var id in next) desired.Add(id);
            generation++; revision = 0; Clear();
            return new JObject { ["generation"] = generation, ["publicPlayerIds"] = new JArray(next) };
        }
        public JObject Reconnect() => Replace(desired.ToArray());
        public void Clear() { states.Clear(); Changed?.Invoke(); }
        public void Apply(string type, JObject payload)
        {
            if ((long?)payload?["generation"] != generation) return;
            long incoming = (long?)payload["revision"] ?? 0;
            if (type == "SOCIAL_PRESENCE_INVALIDATED")
            {
                if (incoming <= revision) return;
                revision = incoming; Clear(); return;
            }
            if (type != "SOCIAL_PRESENCE_SNAPSHOT" && type != "SOCIAL_PRESENCE_UPDATED") return;
            if (incoming != revision || incoming <= 0) return;
            string id = (string)payload["publicPlayerId"], raw = (string)payload["state"];
            if (id == null || !desired.Contains(id) || !Enum.TryParse(raw, out SocialPresenceState state) || !Enum.IsDefined(typeof(SocialPresenceState), state)) return;
            states[id] = state; Changed?.Invoke();
        }
        public static string LocalizationKey(SocialPresenceState state)
        {
            switch (state) {
                case SocialPresenceState.ONLINE: return "social.presence.online";
                case SocialPresenceState.OFFLINE: return "social.presence.offline";
                case SocialPresenceState.IN_MATCH: return "social.presence.inMatch";
                default: return "social.presence.unknown";
            }
        }
    }
}
