using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Domino.Infrastructure
{
    // Explicit validation processes fail closed before the application composition root.
    public static class ValidationNetworkPolicy
    {
#if UNITY_EDITOR
        const string RealKey = "Domino.Validation.RealNetwork";
        const string IsolatedKey = "Domino.Validation.Isolated";
        public static int NetworkEntrypoints { get; private set; }
        public static bool Isolated => (Application.isBatchMode || SessionState.GetBool(IsolatedKey, false)) && !(SessionState.GetBool(RealKey, false) && Environment.GetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS") == "true");
        public static void BeginIsolated() { SessionState.SetBool(RealKey, false); SessionState.SetBool(IsolatedKey, true); }
        public static void AuthorizeReal()
        {
            RequireRealOptIn();
            SessionState.SetBool(RealKey, true); SessionState.SetBool(IsolatedKey, false);
        }
        public static void EndValidation() { SessionState.EraseBool(RealKey); SessionState.EraseBool(IsolatedKey); }
#elif DEVELOPMENT_BUILD
        static bool HasArg(string value) => Array.IndexOf(Environment.GetCommandLineArgs(), value) >= 0;
        public static bool Isolated => HasArg("--i21-visual") || ((HasArg("--i21-client-b") || HasArg("-m5Role")) && Environment.GetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS") != "true");
#else
        public static bool Isolated => false;
#endif
        public static void RequireRealOptIn()
        {
            if (Environment.GetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS") != "true")
                throw new InvalidOperationException("REAL_FIRESTORE_DISABLED");
        }
        public static void RequireNetwork()
        {
            if (Isolated) throw new InvalidOperationException("VALIDATION_NETWORK_DISABLED");
#if UNITY_EDITOR
            NetworkEntrypoints++;
#endif
        }
    }
}
