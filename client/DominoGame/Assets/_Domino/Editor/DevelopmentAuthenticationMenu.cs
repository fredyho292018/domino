#if UNITY_EDITOR
using UnityEditor;
namespace Domino.Editor
{
    public static class DevelopmentAuthenticationMenu
    {
        [MenuItem("Domino/Development/Authentication/Reset Anonymous Identity")]
        public static void Open() => Domino.Development.DevelopmentAuthentication.Open();
        [MenuItem("Domino/Development/Authentication/Reset Anonymous Identity", true)]
        public static bool Available() => EditorApplication.isPlaying && Domino.Development.DevelopmentAuthentication.Allowed;
    }
}
#endif
