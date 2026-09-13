using System;
using System.Collections;
using Domino.Player;
using Domino.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // Attached to the canonical BoardView.Finish result. No independent round simulation.
    public sealed class RoundRewardView : MonoBehaviour
    {
        RoundRewardFlow flow;
        PlayerService player;
        Text balance, status, preview;
        Button watch;
        public Button ContinueButton { get; private set; }
        public Button WatchButton => watch;
        public string BalanceText => balance.text;
        public string StatusText => status.text;
        public void Initialize(RoundRewardFlow rewardFlow, PlayerService playerService, Func<string> result, Func<string> score, Action next)
        {
            flow = rewardFlow; player = playerService;
            var rect = (RectTransform)transform; rect.sizeDelta = new Vector2(620, 590);
            var background = gameObject.AddComponent<Image>(); background.sprite = UiKit.Rounded;
            background.type = Image.Type.Sliced; background.color = UiKit.Hex("153F3B"); background.raycastTarget = true;
            UiKit.LLabel("Result title", transform, "reward.round_end", new Vector2(560,48), new Vector2(0,245), 30, UiKit.Cream);
            var summary = UiKit.Label("Round result", transform, "", new Vector2(560,95), new Vector2(0,170), 23, UiKit.Cream);
            DominoLocalization.Bind(summary, result);
            var scores = UiKit.Label("Match scores", transform, "", new Vector2(560,42), new Vector2(0,95), 25, UiKit.Cream);
            DominoLocalization.Bind(scores, score);
            balance = UiKit.Label("Confirmed wallet", transform, "", new Vector2(560,38), new Vector2(0,45), 23, UiKit.Gold);
            DominoLocalization.Bind(balance, () => player?.Wallet == null ? DominoLocalization.Get("reward.balance_unknown") : DominoLocalization.Get("reward.balance", player.Wallet.Coins));
            preview = UiKit.Label("Reward preview", transform, "", new Vector2(560,36), new Vector2(0,-10), 23, UiKit.Gold);
            DominoLocalization.Bind(preview, () => DominoLocalization.Get("reward.amount", flow?.State == RoundRewardState.REWARDED ? flow.ConfirmedCoins : flow?.PreviewCoins ?? 10));
            status = UiKit.Label("Reward status", transform, "", new Vector2(560,48), new Vector2(0,-58), 21, UiKit.Muted);
            DominoLocalization.Bind(status, () => DominoLocalization.Get(flow?.MessageKey ?? "reward.unavailable"));
            watch = UiKit.Button("Watch reward", transform, "", new Vector2(440,60), new Vector2(0,-120), UiKit.Hex("315851"), Watch);
            DominoLocalization.Bind(watch.GetComponentInChildren<Text>(), () => DominoLocalization.Get(flow?.CanRetry == true ? "reward.retry" : "reward.watch"));
            ContinueButton = UiKit.LButton("Continue", transform, "game.continue", new Vector2(440,72), new Vector2(0,-223), UiKit.Hex("397566"), () => next());
            if (flow != null) flow.Changed += Refresh;
            if (player != null) { player.SnapshotChanged += Snapshot; player.SyncStateChanged += Sync; }
            Refresh();
        }
        async void Watch()
        {
            if (flow == null) return;
            if (flow.CanRetry) await flow.RetryAsync(); else await flow.WatchAsync();
        }
        void Refresh()
        {
            bool visible = flow != null && flow.State != RoundRewardState.HIDDEN;
            preview.gameObject.SetActive(visible); status.gameObject.SetActive(visible); watch.gameObject.SetActive(visible);
            watch.interactable = visible && (flow.CanWatch || flow.CanRetry);
            ContinueButton.interactable = true;
            foreach (var binding in GetComponentsInChildren<LocalizedUiText>(true)) binding.Refresh();
        }
        void Snapshot(PlayerSnapshot _) => Refresh();
        void Sync(PlayerSyncState _) => Refresh();
        void OnDestroy()
        {
            if (flow != null) flow.Changed -= Refresh;
            if (player != null) { player.SnapshotChanged -= Snapshot; player.SyncStateChanged -= Sync; }
        }
    }
}
