#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Domino.Editor
{
    public static class AnonymousIdentityValidationEditor
    {
        public static void Run() {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/Validation/Generated/"))
                throw new InvalidOperationException("Isolated validation project required");
            SessionState.SetBool("DevAuth.Validate", true);
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
