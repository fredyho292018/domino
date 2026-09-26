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
        public const string TestBaseUrl = "https://domino-api-test.teamfho.com";
#if UNITY_EDITOR
        static string OverrideKey => "Domino.Api.Environment." + Application.dataPath;
        public static bool EditorTestSelected => UnityEditor.EditorPrefs.GetBool(OverrideKey, false);
        [UnityEditor.MenuItem("Domino/Environment/TEST server")]
        public static void SelectTest() { UnityEditor.EditorPrefs.SetBool(OverrideKey, true); }
        [UnityEditor.MenuItem("Domino/Environment/Configured asset (LOCAL)")]
        public static void SelectConfigured() { UnityEditor.EditorPrefs.DeleteKey(OverrideKey); }
#endif
        public string Environment {
            get {
#if UNITY_EDITOR
                if (EditorTestSelected) return "TEST";
#endif
                return environment;
            }
        }
        public DominoApiConfiguration Configuration
        {
            get
            {
#if UNITY_EDITOR
                const bool isDevelopmentContext = true;
#else
                bool isDevelopmentContext = Debug.isDebugBuild;
#endif
                // TEST is an explicit preset in the existing configuration, with WSS derived from REST.
                // Editor selection preserves the user-local asset; builds use its environment field.
                bool test = Environment == "TEST";
                return new DominoApiConfiguration(test || enabled, test ? TestBaseUrl : baseUrl,
                    timeoutSeconds, Environment, isDevelopmentContext);
            }
        }
    }
}
