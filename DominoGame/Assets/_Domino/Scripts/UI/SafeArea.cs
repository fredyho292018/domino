using UnityEngine;

namespace Domino.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect previous;
        Vector2 screen;
        void OnEnable() => Apply();
        void Update() { if (previous != Screen.safeArea || screen != new Vector2(Screen.width, Screen.height)) Apply(); }
        void Apply()
        {
            if (Screen.width == 0 || Screen.height == 0) return;
            previous = Screen.safeArea; screen = new Vector2(Screen.width, Screen.height);
            var r = (RectTransform)transform;
            r.anchorMin = new Vector2(previous.xMin / Screen.width, previous.yMin / Screen.height);
            r.anchorMax = new Vector2(previous.xMax / Screen.width, previous.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
