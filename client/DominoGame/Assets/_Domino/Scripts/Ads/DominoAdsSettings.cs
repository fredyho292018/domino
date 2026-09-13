using UnityEngine;

namespace Domino.Ads
{
    [CreateAssetMenu(menuName = "Domino/Ads Settings")]
    public sealed class DominoAdsSettings : ScriptableObject
    {
        [SerializeField] bool enabled = false;
        [SerializeField] AdsEnvironment environment = AdsEnvironment.DEVELOPMENT;
        [SerializeField] string androidRewardedAdUnitIdDevelopment = AdsConfiguration.AndroidDemoRewarded;
        [SerializeField] string androidRewardedAdUnitIdProduction = "";
        [SerializeField] string iosRewardedAdUnitIdDevelopment = AdsConfiguration.IosDemoRewarded;
        [SerializeField] string iosRewardedAdUnitIdProduction = "";
        public string AndroidRewardedAdUnitIdDevelopment => androidRewardedAdUnitIdDevelopment;
        public string IosRewardedAdUnitIdDevelopment => iosRewardedAdUnitIdDevelopment;
        public AdsConfiguration Configuration => new AdsConfiguration(enabled, environment,
            Application.platform == RuntimePlatform.IPhonePlayer ? AdsPlatform.Ios :
            Application.isEditor || Application.platform == RuntimePlatform.Android ? AdsPlatform.Android : AdsPlatform.Unsupported,
            Application.isEditor || Debug.isDebugBuild, androidRewardedAdUnitIdProduction, iosRewardedAdUnitIdProduction);
        void OnValidate()
        {
            // Development inventory cannot be overridden through the Inspector.
            androidRewardedAdUnitIdDevelopment = AdsConfiguration.AndroidDemoRewarded;
            iosRewardedAdUnitIdDevelopment = AdsConfiguration.IosDemoRewarded;
        }
    }
}
