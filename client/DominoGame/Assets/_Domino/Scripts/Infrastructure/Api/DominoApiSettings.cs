using UnityEngine;

namespace Domino.Infrastructure.Api
{
    [CreateAssetMenu(menuName = "Domino/API Settings")]
    public sealed class DominoApiSettings : ScriptableObject
    {
        [SerializeField] bool enabled = false;
        [SerializeField] string environment = "Unconfigured development";
        [SerializeField] string baseUrl = "";
        [SerializeField, Range(1, 60)] int timeoutSeconds = 15;
        public string Environment => environment;
        public DominoApiConfiguration Configuration
        {
            get
            {
#if UNITY_EDITOR
                const bool isDevelopmentContext = true;
#else
                bool isDevelopmentContext = Debug.isDebugBuild;
#endif
                return new DominoApiConfiguration(enabled, baseUrl, timeoutSeconds, environment, isDevelopmentContext);
            }
        }
    }
}
