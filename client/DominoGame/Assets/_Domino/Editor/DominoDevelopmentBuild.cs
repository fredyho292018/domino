#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Domino.Editor
{
    public static class DominoDevelopmentBuild
    {
        [MenuItem("Domino/Build/Windows Development Duel Client")]
        public static void BuildWindowsDuelClient()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-dominoBuildOutput");
                var output = index >= 0 && index + 1 < args.Length ? args[index + 1]
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "../../../builds/duel-client-b"));
                Directory.CreateDirectory(output);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new Exception("Windows target unavailable");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null) throw new Exception("Addressables settings missing");
                foreach (var locale in new[] { "en", "es" })
                    if (!settings.groups.Where(g => g != null).SelectMany(g => g.entries).Any(e => e.address == locale))
                        throw new Exception("Locale missing: " + locale);
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
                if (!string.IsNullOrEmpty(content.Error)) throw new Exception(content.Error);
                Debug.Log("[BUILD] ADDRESSABLES_BUILD=PASS");
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ClientEditorTools.ScenePath },
                    locationPathName = Path.Combine(output, "DominoGame.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (result.summary.result != BuildResult.Succeeded) throw new Exception("Player build failed");
                foreach (var file in new[] { "DominoGame.exe", "UnityPlayer.dll", "DominoGame_Data/StreamingAssets/aa/settings.json" })
                    if (!File.Exists(Path.Combine(output, file))) throw new Exception("Missing runtime file: " + file);
                var aa = Path.Combine(output, "DominoGame_Data/StreamingAssets/aa");
                if (!Directory.GetFiles(aa, "*.bundle", SearchOption.AllDirectories).Any()) throw new Exception("Addressables bundles missing");
                Debug.Log("[BUILD] WINDOWS_DEVELOPMENT=PASS OUTPUT=" + output);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
#endif
