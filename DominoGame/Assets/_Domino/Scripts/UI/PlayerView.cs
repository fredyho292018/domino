using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class PlayerView : MonoBehaviour
    {
        Image halo;
        Text count;
        bool active;
        float intensity;
        public void Initialize(string playerName, string initials, Color accent, bool horizontal = false)
        {
            var avatar = horizontal ? new Vector2(-60, 0) : Vector2.zero;
            halo = UiKit.Panel("Turn halo", transform, new Vector2(54, 54), avatar, UiKit.Gold);
            UiKit.Panel("Avatar", transform, new Vector2(46, 46), avatar, accent);
            UiKit.Label("Initials", transform, initials, new Vector2(44, 44), avatar, 21, UiKit.Cream);
            UiKit.Label("Name", transform, playerName, new Vector2(110, 26), horizontal ? new Vector2(23, 12) : new Vector2(0, -42), 20, UiKit.Cream);
            count = UiKit.Label("Count", transform, "0 fichas", new Vector2(horizontal ? 150 : 124, 28), horizontal ? new Vector2(23, -13) : new Vector2(0, -67), 13, UiKit.Muted);
        }
        public void SetCount(int remaining) => count.text = $"{remaining} fichas";
        public void ShowPoints(int remaining, int points) => count.text = $"{remaining} fichas · {points} pts";
        public void SetTurn(bool value) => active = value;
        void Update()
        {
            if (!halo) return;
            intensity = Mathf.MoveTowards(intensity, active ? 1 : 0, Time.unscaledDeltaTime * 5);
            halo.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, intensity * (.8f + .2f * Mathf.Sin(Time.unscaledTime * 3)));
        }
    }
}

