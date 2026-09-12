using System;
using System.Threading;
using Domino.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // Presentation only: confirmed snapshots and service events drive every displayed value.
    public sealed class PlayerProfileView : MonoBehaviour
    {
        PlayerService service;
        CancellationTokenSource lifetime;
        GameObject overlay;
        RectTransform panel;
        Text summary, balance, account, status, feedback;
        string feedbackKey;
        bool saving;
        public Button OpenButton { get; private set; }
        public Button SaveButton { get; private set; }
        public Button RetryButton { get; private set; }
        public InputField AliasInput { get; private set; }
        public bool IsOpen => overlay && overlay.activeSelf;
        public string DisplayedName => service?.Player == null ? DominoLocalization.Get("profile.title") :
            DisplayNameRules.IsGenerated(service.Player.DisplayName) ? DominoLocalization.Get("profile.choose") : service.Player.DisplayName;
        public string DisplayedCoins => service?.Wallet == null ? "--" : DominoLocalization.Get("profile.coins", service.Wallet.Coins);
        string StateKey => service == null || service.State == PlayerSyncState.NOT_SYNCED ? "profile.connecting" :
            service.State == PlayerSyncState.SYNCING ? "profile.syncing" :
            service.State == PlayerSyncState.SYNCED && service.Availability == BackendAvailability.AVAILABLE ? "profile.online" :
            service.HasConfirmedSnapshots ? "profile.offline_cached" : "profile.offline";

        public void Initialize(PlayerService playerService, Transform parent)
        {
            service = playerService;
            lifetime = new CancellationTokenSource();
            OpenButton = UiKit.Button("Profile", parent, "", new Vector2(720,110), new Vector2(0,400), UiKit.Hex("1D403E"), Open);
            summary = OpenButton.GetComponentInChildren<Text>();
            summary.rectTransform.sizeDelta = new Vector2(680,96); summary.fontSize = 24;
            DominoLocalization.Bind(summary, () => DisplayedName + "\n" + DominoLocalization.Get(StateKey));
            overlay = UiKit.Panel("Profile overlay", parent, new Vector2(2600,3400), Vector2.zero, new Color(0,0,0,.78f)).gameObject;
            overlay.GetComponent<Image>().raycastTarget = true;
            panel = UiKit.Panel("Player profile", overlay.transform, new Vector2(880,690), Vector2.zero, UiKit.Hex("1D403E")).rectTransform;
            UiKit.LLabel("Title", panel, "profile.title", new Vector2(770,55), new Vector2(0,285), 32, UiKit.Cream);
            balance = UiKit.Label("Coins", panel, "", new Vector2(750,45), new Vector2(0,218), 27, UiKit.Gold);
            DominoLocalization.Bind(balance, () => DisplayedCoins);
            account = UiKit.Label("Account", panel, "", new Vector2(750,40), new Vector2(0,170), 22, UiKit.Muted);
            DominoLocalization.Bind(account, () => service?.Player == null ? "" : DominoLocalization.Get(service.Player.AccountType == PlayerAccountType.Guest ? "profile.guest" : "profile.registered"));
            status = UiKit.Label("Status", panel, "", new Vector2(750,54), new Vector2(0,113), 23, UiKit.Muted);
            DominoLocalization.Bind(status, () => DominoLocalization.Get(StateKey));
            UiKit.LLabel("Alias label", panel, "profile.alias", new Vector2(750,40), new Vector2(0,45), 24, UiKit.Cream);
            var input = UiKit.Panel("Alias input", panel, new Vector2(740,76), new Vector2(0,-18), UiKit.Hex("102C2C"));
            input.raycastTarget = true;
            AliasInput = input.gameObject.AddComponent<InputField>(); AliasInput.targetGraphic = input;
            AliasInput.textComponent = UiKit.Label("Text", input.transform, "", new Vector2(690,65), Vector2.zero, 28, UiKit.Cream, TextAnchor.MiddleLeft);
            AliasInput.placeholder = UiKit.LLabel("Placeholder", input.transform, "profile.choose", new Vector2(690,65), Vector2.zero, 26, UiKit.Muted, TextAnchor.MiddleLeft);
            AliasInput.lineType = InputField.LineType.SingleLine;
            AliasInput.contentType = InputField.ContentType.Standard;
            AliasInput.onValueChanged.AddListener(_ => { feedbackKey = null; Refresh(); });
            feedback = UiKit.Label("Feedback", panel, "", new Vector2(770,80), new Vector2(0,-106), 22, UiKit.Gold);
            DominoLocalization.Bind(feedback, () => DominoLocalization.Get(saving ? "profile.saving" : feedbackKey ?? "profile.help"));
            UiKit.LButton("Cancel", panel, "system.cancel", new Vector2(330,70), new Vector2(-190,-212), UiKit.Hex("31514F"), Close);
            SaveButton = UiKit.LButton("Save", panel, "profile.save", new Vector2(330,70), new Vector2(190,-212), UiKit.Hex("397566"), Save);
            RetryButton = UiKit.LButton("Retry", panel, "system.retry", new Vector2(500,66), new Vector2(0,-296), Color.clear, Retry);
            Bind(playerService);
            overlay.SetActive(false); Refresh();
        }
        public void Bind(PlayerService playerService)
        {
            if (service != null) { service.SyncStateChanged -= OnState; service.BackendAvailabilityChanged -= OnAvailability; service.SnapshotChanged -= OnSnapshot; }
            service = playerService;
            if (service != null) { service.SyncStateChanged += OnState; service.BackendAvailabilityChanged += OnAvailability; service.SnapshotChanged += OnSnapshot; }
            Refresh();
        }
        public void Open()
        {
            overlay.SetActive(true); overlay.transform.SetAsLastSibling(); feedbackKey = null;
            AliasInput.text = service?.Player == null || DisplayNameRules.IsGenerated(service.Player.DisplayName) ? "" : service.Player.DisplayName;
            Refresh();
        }
        public void Close() { AliasInput.DeactivateInputField(); overlay.SetActive(false); }
        void OnState(PlayerSyncState _) => Refresh();
        void OnAvailability(BackendAvailability _) => Refresh();
        void OnSnapshot(PlayerSnapshot _) => Refresh();
        void Refresh()
        {
            summary.text = DisplayedName + "\n" + DominoLocalization.Get(StateKey);
            balance.text = DisplayedCoins;
            account.text = service?.Player == null ? "" : DominoLocalization.Get(service.Player.AccountType == PlayerAccountType.Guest ? "profile.guest" : "profile.registered");
            status.text = DominoLocalization.Get(StateKey);
            AliasInput.interactable = !saving && service != null && service.CanEdit;
            SaveButton.interactable = !saving && service != null && service.CanEdit && DisplayNameRules.IsValid(AliasInput.text) && AliasInput.text != service.Player.DisplayName;
            RetryButton.gameObject.SetActive(service != null && service.CanRetry);
            RetryButton.interactable = !saving && service != null && service.CanRetry;
            feedback.text = DominoLocalization.Get(saving ? "profile.saving" : feedbackKey ?? "profile.help");
        }
        async void Save()
        {
            if (!SaveButton.interactable) return;
            saving = true; Refresh();
            try {
                string requested = AliasInput.text;
                await service.UpdateDisplayNameAsync(requested, lifetime.Token);
                if (!this || lifetime.IsCancellationRequested) return;
                if (service.Error == null && service.Player?.DisplayName == requested) Close();
                else feedbackKey = service.Error?.ServerErrorCode == "DISPLAY_NAME_RESERVED" ? "profile.reserved" :
                    service.Error?.ServerErrorCode == "DISPLAY_NAME_INVALID" ? "profile.invalid" : "profile.save_failed";
            } catch { if (this) feedbackKey = "profile.save_failed"; }
            finally { if (this) { saving = false; Refresh(); } }
        }
        async void Retry()
        {
            if (service == null || !service.CanRetry) return;
            try { await service.RetryAsync(lifetime.Token); } catch { }
            if (this) Refresh();
        }
        void LateUpdate()
        {
            // Keyboard coordinates are physical pixels; restore the resting position after dismissal.
            if (panel) panel.anchoredPosition = new Vector2(0, TouchScreenKeyboard.visible ? 150 : 0);
        }
        void OnDisable() { if (overlay) Close(); }
        void OnDestroy()
        {
            if (service != null) { service.SyncStateChanged -= OnState; service.BackendAvailabilityChanged -= OnAvailability; service.SnapshotChanged -= OnSnapshot; }
            lifetime?.Cancel(); lifetime?.Dispose();
            if (overlay) Destroy(overlay);
            if (OpenButton) Destroy(OpenButton.gameObject);
        }
    }
}
