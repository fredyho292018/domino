#if UNITY_EDITOR
using UnityEngine;
namespace Domino.Ads
{
    // Explicit approval for the SDK's local Editor mock only, never mobile consent.
    public sealed class EditorMockAdsConsent : IAdsConsentGate
    {
        public bool AuthorizedForThisPlaySession { get; set; }
        public bool CanInitializeAds => Application.isEditor && Application.isPlaying && AuthorizedForThisPlaySession;
    }
}
#endif
