using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class PlayerView : MonoBehaviour
    {
        Image halo;
        Image glow;
        Text count;
        Text role;
        RectTransform floating;
        Image plate, rim, shadow;
        public RectTransform PanelRect => plate ? plate.rectTransform : null;
        public string DisplayName { get; private set; }
        public void SetDisplayName(string value)
        {
            DisplayName = value;
            var label = transform.Find("Name");
            if (label) label.GetComponent<Text>().text = value;
        }
        bool active;
        float intensity;
        public void Initialize(string playerName, string initials, Color accent, bool horizontal = false)
        {
            DisplayName = playerName;
            var avatar = horizontal ? new Vector2(-60, 0) : Vector2.zero;
            UiKit.Disc("Avatar shadow", transform, 70, avatar + Vector2.down * 4, new Color(0, 0, 0, .22f));
            glow = UiKit.Disc("Turn glow", transform, 78, avatar, Color.clear);
            halo = UiKit.Disc("Turn halo", transform, 68, avatar, Color.clear);
            UiKit.Disc("Avatar rim", transform, 61, avatar, DominoVisualTheme.Background);
            UiKit.Disc("Avatar", transform, 56, avatar, Color.Lerp(accent, DominoVisualTheme.Background, .25f));
            UiKit.Label("Initials", transform, initials, new Vector2(48, 48), avatar, 25, UiKit.Cream);
            UiKit.Disc("Team accent", transform, 8, avatar + new Vector2(21, -23), horizontal ? DominoVisualTheme.TeamA : DominoVisualTheme.TeamB);
            UiKit.Label("Name", transform, playerName, new Vector2(110, 26), horizontal ? new Vector2(23, 12) : new Vector2(0, -49), 20, UiKit.Cream);
            count = UiKit.Label("Count", transform, "", new Vector2(horizontal ? 150 : 124, 28), horizontal ? new Vector2(23, -13) : new Vector2(0, -73), 13, UiKit.Muted);
        }
        public void SetCount(int remaining) => DominoLocalization.Set(count, "rules.tiles", remaining);
        public void SetPresentation(VisualSeat slot, bool portrait, string roleKey)
        {
            bool horizontal = slot == VisualSeat.Bottom || slot == VisualSeat.Top;
            bool top = slot == VisualSeat.Top;
            bool local = slot == VisualSeat.Bottom;
            if (!floating)
            {
                floating = UiKit.Rect("Floating player panel",transform,Vector2.zero,Vector2.zero);
                floating.SetAsFirstSibling();
                shadow = UiKit.Panel("Contact shadow",floating,Vector2.zero,Vector2.down*8,new Color(0,0,0,.2f));
                rim = UiKit.Panel("Soft edge",floating,Vector2.zero,Vector2.zero,new Color(.65f,.78f,.73f,.22f));
                plate = UiKit.Panel("Floating surface",floating,Vector2.zero,Vector2.zero,new Color(.04f,.11f,.14f,.92f));
                plate.raycastTarget = true;
            }
            floating.gameObject.SetActive(portrait && !local);
            var panelSize = top ? new Vector2(234,222) : new Vector2(176,290);
            floating.anchoredPosition = top ? new Vector2(0,-22) : Vector2.zero;
            shadow.rectTransform.sizeDelta = panelSize + new Vector2(8,8);
            rim.rectTransform.sizeDelta = panelSize;
            plate.rectTransform.sizeDelta = panelSize-Vector2.one*2;
            var avatar = portrait ? local ? new Vector2(-66,0) : new Vector2(0,top ? 40 : 80)
                : horizontal ? new Vector2(-60,0) : Vector2.zero;
            foreach (string name in new[] { "Avatar shadow", "Turn glow", "Turn halo", "Avatar rim", "Avatar", "Initials", "Team accent" })
            {
                var rect = (RectTransform)transform.Find(name);
                rect.anchoredPosition = avatar + (name == "Avatar shadow" ? Vector2.down*4 : name == "Team accent" ? new Vector2(25,-27) : Vector2.zero);
                if (name != "Team accent") rect.localScale = Vector3.one*(portrait ? 1.22f : 1);
            }
            var nameLabel = transform.Find("Name").GetComponent<Text>();
            nameLabel.rectTransform.anchoredPosition = portrait ? local ? new Vector2(38,15) : new Vector2(0,top ? -17 : 25)
                : horizontal ? new Vector2(23,12) : new Vector2(0,-49);
            nameLabel.fontSize = portrait ? 28 : 20;
            nameLabel.rectTransform.sizeDelta = new Vector2(portrait ? 160 : 110,portrait ? 36 : 26);
            count.rectTransform.anchoredPosition = portrait ? local ? new Vector2(38,-14) : new Vector2(0,top ? -46 : -6)
                : horizontal ? new Vector2(23,-13) : new Vector2(0,-73);
            count.fontSize = portrait ? 20 : 13; count.resizeTextMaxSize = count.fontSize;
            count.rectTransform.sizeDelta = new Vector2(portrait ? 164 : horizontal ? 150 : 124,28);
            if (!role) role = UiKit.Label("Role",transform,"",new Vector2(164,26),Vector2.zero,18,UiKit.Gold);
            role.rectTransform.anchoredPosition = portrait ? local ? new Vector2(38,-43) : new Vector2(0,top ? -73 : -34)
                : horizontal ? new Vector2(23,-36) : new Vector2(0,49);
            role.fontSize = portrait ? 18 : 14;
            role.gameObject.SetActive(portrait);
            DominoLocalization.Set(role,roleKey);
            transform.Find("Team accent").GetComponent<Image>().color = horizontal ? DominoVisualTheme.TeamA : DominoVisualTheme.TeamB;
        }
        public void ShowPoints(int remaining, int points) => DominoLocalization.Set(count, "game.hand_points", remaining, points);
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

