using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public static class UiKit
    {
        public static readonly Color Ink = Hex("142B2C");
        public static readonly Color Cream = Hex("F3EEDD");
        public static readonly Color Gold = Hex("D7B879");
        public static readonly Color Muted = Hex("9DBAB4");
        static Sprite rounded;
        static Font font;
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Sprite Rounded
        {
            get
            {
                if (rounded) return rounded;
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Domino rounded UI", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var q = new Vector2(Mathf.Abs(x - 31.5f) - 16, Mathf.Abs(y - 31.5f) - 16);
                    float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude - 15;
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d));
                }
                t.SetPixels(pixels); t.Apply();
                rounded = Sprite.Create(t, new Rect(0, 0, 64, 64), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(25, 25, 25, 25));
                return rounded;
            }
        }
        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = pos;
            return r;
        }
        public static Image Panel(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
        {
            var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
            image.sprite = Rounded; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
            return image;
        }
        public static Text Label(string name, Transform parent, string value, Vector2 size, Vector2 pos, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var text = Rect(name, parent, size, pos).gameObject.AddComponent<Text>();
            text.font = Font; text.text = value; text.fontSize = fontSize; text.color = color;
            text.alignment = align; text.raycastTarget = false;
            return text;
        }
        public static Button Button(string name, Transform parent, string label, Vector2 size, Vector2 pos, Color bg, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(name, parent, size, pos, bg); panel.raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel;
            button.onClick.AddListener(action);
            Label("Label", panel.transform, label, size, Vector2.zero, 19, Cream);
            return button;
        }
    }
}
