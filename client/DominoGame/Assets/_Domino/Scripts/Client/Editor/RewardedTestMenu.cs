#if UNITY_EDITOR
using Domino.Ads;
using Domino.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Domino.Editor
{
    public static class RewardedTestMenu
    {
        [MenuItem("Domino/Ads/Recover Pending Verified Reward")]
        public static async void RecoverPending()
        {
            if (!Application.isPlaying || ApplicationServices.RewardVerification == null) return;
            await ApplicationServices.RewardVerification.RecoverPendingAsync();
            Debug.Log("[ECONOMY] pending state=" + ApplicationServices.RewardVerification.State);
        }
        [MenuItem("Domino/Ads/Consume Verified Reward")]
        public static async void Consume()
        {
            if (!Application.isPlaying || ApplicationServices.RewardVerification == null) return;
            bool applied = await ApplicationServices.RewardVerification.ConsumeRewardAsync();
            Debug.Log(applied ? "[ECONOMY] confirmed wallet received" : "[ECONOMY] consume unavailable or failed");
        }
        [MenuItem("Domino/Ads/Refresh Reward Intent Status")]
        public static async void RefreshStatus()
        {
            if (!Application.isPlaying || ApplicationServices.RewardVerification == null) return;
            await ApplicationServices.RewardVerification.RefreshAsync();
            Debug.Log("[ADS] reward verification state=" + ApplicationServices.RewardVerification.State);
        }
        [MenuItem("Domino/Ads/Authorize and Load Editor Mock")]
        public static async void Load()
        {
            var settings = Resources.Load<DominoAdsSettings>("AdsSettings");
            if (!Application.isPlaying || !settings || !settings.Configuration.Enabled ||
                settings.Configuration.Environment != AdsEnvironment.DEVELOPMENT || ApplicationServices.EditorAdsConsent == null)
            { Debug.Log("[ADS] Editor mock requires Play Mode and enabled DEVELOPMENT settings before Play."); return; }
            ApplicationServices.EditorAdsConsent.AuthorizedForThisPlaySession = true;
            await ApplicationServices.Rewarded.InitializeAsync();
        }
        [MenuItem("Domino/Ads/Show Rewarded Test Ad")]
        public static async void Show()
        {
            if (!Application.isPlaying || ApplicationServices.Rewarded == null) return;
            var result = await ApplicationServices.Rewarded.ShowRewardedAsync();
            Debug.Log("[ADS] Editor mock result=" + result + "; Domino coins credited=0");
        }
    }
}
#endif
