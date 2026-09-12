using System;
using System.Text.RegularExpressions;

namespace Domino.Ads
{
    public enum AdsEnvironment { DEVELOPMENT, PRODUCTION }
    public enum AdsPlatform { Android, Ios, Unsupported }

    // Only ad unit IDs belong here. Native App IDs belong to Google's settings asset.
    public sealed class AdsConfiguration
    {
        public const string AndroidDemoRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string IosDemoRewarded = "ca-app-pub-3940256099942544/1712485313";
        public bool Enabled { get; }
        public AdsEnvironment Environment { get; }
        public string RewardedUnitId { get; }
        public string UnavailableReason { get; }
        public bool IsAvailable => Enabled && UnavailableReason == null;

        public AdsConfiguration(bool enabled, AdsEnvironment environment, AdsPlatform platform,
            bool developmentContext, string androidProduction = "", string iosProduction = "")
        {
            Enabled = enabled;
            Environment = environment;
            if (platform == AdsPlatform.Unsupported) { UnavailableReason = "PLATFORM"; return; }
            if (environment == AdsEnvironment.DEVELOPMENT)
            {
                if (!developmentContext) { UnavailableReason = "DEVELOPMENT_IN_RELEASE"; return; }
                RewardedUnitId = platform == AdsPlatform.Android ? AndroidDemoRewarded : IosDemoRewarded;
                return;
            }
            if (environment != AdsEnvironment.PRODUCTION) { UnavailableReason = "ENVIRONMENT"; return; }
            // Editor/development builds cannot accidentally use production inventory.
            if (developmentContext) { UnavailableReason = "PRODUCTION_IN_DEVELOPMENT"; return; }
            var id = platform == AdsPlatform.Android ? androidProduction : iosProduction;
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"\Aca-app-pub-[0-9]{16}/[0-9]{10}\z") ||
                id.StartsWith("ca-app-pub-3940256099942544/", StringComparison.Ordinal))
            { UnavailableReason = "PRODUCTION_UNIT_ID"; return; }
            RewardedUnitId = id;
        }
    }
}
