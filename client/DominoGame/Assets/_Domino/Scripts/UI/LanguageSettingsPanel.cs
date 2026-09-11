using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed class LanguageSettingsPanel : MonoBehaviour
    {
        public Button English { get; private set; }
        public Button Spanish { get; private set; }
        public static LanguageSettingsPanel Create(Transform parent)
        {
            var overlay = UiKit.Panel("Language settings", parent, new Vector2(2400,1400), Vector2.zero, new Color(0,0,0,.72f));
            overlay.raycastTarget = true;
            var panel = overlay.gameObject.AddComponent<LanguageSettingsPanel>();
            var card = UiKit.Panel("Settings card", overlay.transform, new Vector2(430,300), Vector2.zero, UiKit.Hex("1D403E")).transform;
            UiKit.LLabel("Heading", card, "menu.settings", new Vector2(380,40), new Vector2(0,111), 26, UiKit.Cream);
            UiKit.LLabel("Language", card, "settings.language", new Vector2(380,30), new Vector2(0,65), 19, UiKit.Muted);
            panel.English = UiKit.LButton("English", card, "settings.english", new Vector2(180,52), new Vector2(-99,0), UiKit.Hex("397566"), () => DominoLocalization.Select("en"));
            panel.Spanish = UiKit.LButton("Spanish", card, "settings.spanish", new Vector2(180,52), new Vector2(99,0), UiKit.Hex("397566"), () => DominoLocalization.Select("es"));
            UiKit.LButton("Back", card, "menu.back", new Vector2(220,44), new Vector2(0,-98), Color.clear, () => panel.gameObject.SetActive(false));
            overlay.gameObject.SetActive(false);
            return panel;
        }
        public void Open() { transform.SetAsLastSibling(); gameObject.SetActive(true); }
        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent) transform.Find("Settings card").localScale = Vector3.one * (parent.rect.height > parent.rect.width ? 1.65f : 1f);
        }
    }
}
