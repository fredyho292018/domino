using System.Collections;
using Domino.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // Application lifetime feedback also covers a reward completed after leaving the round panel.
    public sealed class RewardConfirmationToast : MonoBehaviour
    {
        RoundRewardFlow flow;
        GameObject overlay;
        public void Initialize(RoundRewardFlow service) { flow = service; flow.Confirmed += Show; }
        void Show(long amount) { StopAllCoroutines(); if (overlay) Destroy(overlay); StartCoroutine(Present(amount)); }
        IEnumerator Present(long amount)
        {
            while (!DominoLocalization.Ready) yield return null;
            overlay = new GameObject("Confirmed reward", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0;
            var safe = UiKit.Rect("Safe area", overlay.transform, Vector2.zero, Vector2.zero); safe.gameObject.AddComponent<SafeArea>();
            var panel = UiKit.Panel("Reward confirmation", safe, new Vector2(750,100), new Vector2(0,-90), UiKit.Hex("183E37")).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f,1);
            var group = panel.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var label = UiKit.Label("Confirmed amount", panel, "", new Vector2(700,80), Vector2.zero, 28, UiKit.Gold);
            DominoLocalization.Set(label, "reward.toast", amount);
            for (float t=0;t<2.4f;t+=Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Min(t/.18f, (2.4f-t)/.3f);
                panel.localScale = Vector3.one * (1 + .04f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t/.3f)));
                yield return null;
            }
            Destroy(overlay);
        }
        void OnDestroy() { if (flow != null) flow.Confirmed -= Show; if (overlay) Destroy(overlay); }
    }
}
