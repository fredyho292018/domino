using Domino.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class RealtimeStatusView : MonoBehaviour
    {
        IRealtimeConnectionService service;
        Text label;
        public string DisplayedStatus => label ? label.text : "";
        public void Initialize(IRealtimeConnectionService realtime, Transform parent)
        {
            label = UiKit.Label("Realtime status", parent, "", new Vector2(870,65), new Vector2(0,-335), 19, UiKit.Muted);
            DominoLocalization.Bind(label, Display);
            Bind(realtime);
        }
        public void Bind(IRealtimeConnectionService realtime)
        {
            if (service != null) service.Changed -= Refresh;
            service = realtime;
            if (service != null) service.Changed += Refresh;
            Refresh();
        }
        string Display()
        {
            var key = service?.State switch {
                RealtimeConnectionState.CONNECTED => "realtime.connected",
                RealtimeConnectionState.CONNECTING => "realtime.connecting",
                RealtimeConnectionState.AUTHENTICATING => "realtime.connecting",
                RealtimeConnectionState.RECONNECTING => "realtime.reconnecting",
                _ => "realtime.disconnected"
            };
            var activity = service?.Activity;
            return DominoLocalization.Get(key) + (activity == null ? "" : "\n" + DominoLocalization.Get("realtime.activity", activity.OnlinePlayers, activity.ActiveMatches, activity.WaitingPlayers));
        }
        void Refresh() { if (label) label.text = Display(); }
        void OnDestroy() { if (service != null) service.Changed -= Refresh; }
    }
}
