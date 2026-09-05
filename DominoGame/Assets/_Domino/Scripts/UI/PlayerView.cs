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
        public void Initialize(string playerName, string initials, Color accent)
        {
            halo = UiKit.Panel("Turn halo", transform, new Vector2(68, 68), Vector2.zero, UiKit.Gold);
            UiKit.Panel("Avatar", transform, new Vector2(60, 60), Vector2.zero, accent);
            UiKit.Label("Initials", transform, initials, new Vector2(56, 56), Vector2.zero, 25, UiKit.Cream);
            UiKit.Label("Name", transform, playerName, new Vector2(150, 30), new Vector2(0, -52), 23, UiKit.Cream);
            count = UiKit.Label("Count", transform, "0 fichas", new Vector2(150, 26), new Vector2(0, -81), 16, UiKit.Muted);
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

