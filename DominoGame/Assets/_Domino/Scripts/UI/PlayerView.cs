using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class PlayerView : MonoBehaviour
    {
        Image halo;
        Image glow;
        Text count;
        bool active;
        float intensity;
        public void Initialize(string playerName, string initials, Color accent, bool horizontal = false)
        {
            var avatar = horizontal ? new Vector2(-60, 0) : Vector2.zero;
            UiKit.Disc("Avatar shadow", transform, 70, avatar + Vector2.down * 4, new Color(0, 0, 0, .22f));
            glow = UiKit.Disc("Turn glow", transform, 78, avatar, Color.clear);
            halo = UiKit.Disc("Turn halo", transform, 68, avatar, Color.clear);
            UiKit.Disc("Avatar rim", transform, 61, avatar, DominoVisualTheme.Background);
            UiKit.Disc("Avatar", transform, 56, avatar, Color.Lerp(accent, DominoVisualTheme.Background, .25f));
            UiKit.Label("Initials", transform, initials, new Vector2(48, 48), avatar, 25, UiKit.Cream);
            UiKit.Disc("Team accent", transform, 8, avatar + new Vector2(21, -23), horizontal ? DominoVisualTheme.TeamA : DominoVisualTheme.TeamB);
            UiKit.Label("Name", transform, playerName, new Vector2(110, 26), horizontal ? new Vector2(23, 12) : new Vector2(0, -49), 20, UiKit.Cream);
            count = UiKit.Label("Count", transform, "0 fichas", new Vector2(horizontal ? 150 : 124, 28), horizontal ? new Vector2(23, -13) : new Vector2(0, -73), 13, UiKit.Muted);
        }
        public void SetCount(int remaining) => count.text = $"{remaining} fichas";
        public void ShowPoints(int remaining, int points) => count.text = $"{remaining} fichas · {points} pts";
        public void SetTurn(bool value)
        {
            active = value;
            intensity = value ? Mathf.Max(intensity, .8f) : 0;
            ApplyHalo();
        }
        void Update()
        {
            if (!halo) return;
            intensity = Mathf.MoveTowards(intensity, active ? 1 : 0, Time.unscaledDeltaTime * 5);
            ApplyHalo();
        }
        void ApplyHalo()
        {
            if (!halo) return;
            halo.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, intensity * (.8f + .2f * Mathf.Sin(Time.unscaledTime * 3)));
            glow.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, intensity * .1f);
        }
    }
}

