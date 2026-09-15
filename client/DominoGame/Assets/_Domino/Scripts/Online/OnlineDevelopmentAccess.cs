#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Domino.Infrastructure.Api;
using UnityEngine;

namespace Domino.Online
{
    // Shared gate for manual online diagnostics, independent of identity-reset tooling.
    public static class OnlineDevelopmentAccess
    {
        public static bool Allowed
        {
            get {
                var settings = Resources.Load<DominoApiSettings>("ApiSettings");
                return (Application.isEditor || Debug.isDebugBuild) && settings &&
                    (settings.Environment == "LOCAL" || settings.Environment == "DEVELOPMENT" || settings.Environment == "TEST");
            }
        }
    }
}
#endif
