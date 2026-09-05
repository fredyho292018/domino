using System;
using System.IO;
using System.Collections.Generic;
using Domino.Client;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Isolated batch-only geometry replay. Does not impersonate Device Simulator or use private APIs.
    public static class DisplayProfileValidation
    {
        const string Running = "Domino.DisplayReplay";
        static DominoClientController controller;
        static readonly List<(string name, DisplayScreen screen, DisplayOrientation orientation)> cases = new();
        static readonly List<string> results = new();
        static int index, frames, checks, lastFrame = -1;
        static double deadline;
        static Camera camera;
        static string Output => Path.GetFullPath("../DeviceMatrix");
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) Finish(false, message);
            };
            deadline = EditorApplication.timeSinceStartup + 120;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Running, true);
            EditorSceneManager.OpenScene("Assets/_Domino/Scenes/DominoClient.unity");
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Replay timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (!controller)
                {
                    controller = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                    Time.timeScale = 8;
                }
                if (!controller || !controller.AcceptingInput) return;
                if (cases.Count == 0)
                {
                    foreach (string path in Directory.GetFiles(DisplayValidationMatrix.ProfileRoot, "*.device"))
                    {
                        Check(AssetDatabase.LoadMainAssetAtPath(path) != null, "Imported " + path);
                        var profile = JsonUtility.FromJson<DisplayProfile>(File.ReadAllText(path));
                        foreach (var orientation in profile.screens[0].orientations) cases.Add((profile.friendlyName, profile.screens[0], orientation));
                    }
                    var canvas = controller.View.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.transform.position = Vector3.zero;
                    camera = new GameObject("Profile replay camera").AddComponent<Camera>();
                    camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    canvas.worldCamera = camera;
                    controller.View.GetComponentInChildren<SafeArea>().enabled = false;
                }
                if (index >= cases.Count) { Finish(true, $"{checks} checks; {cases.Count} profile/orientation geometry replays. Actual Device Simulator interaction remains manual."); return; }
                var item = cases[index];
                bool landscape = item.orientation.orientation >= 3;
                int width = landscape ? item.screen.height : item.screen.width;
                int height = landscape ? item.screen.width : item.screen.height;
                var safe = item.orientation.safeArea.ToRect();
                var canvasRect = (RectTransform)controller.View.transform;
                canvasRect.pivot = Vector2.one * .5f;
                canvasRect.localScale = Vector3.one;
                canvasRect.position = Vector3.zero;
                canvasRect.sizeDelta = new Vector2(width, height);
                canvasRect.position = Vector3.zero;
                var safeRect = controller.View.GetComponentInChildren<SafeArea>().GetComponent<RectTransform>();
                safeRect.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
                safeRect.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
                safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
                Canvas.ForceUpdateCanvases();
                if (++frames < 4) return;
                var root = controller.View.transform.Find("Safe area/Landscape composition");
                File.AppendAllText(Path.Combine(Output, "transforms.txt"), $"{index} canvas={canvasRect.rect} pos={canvasRect.position} safe={safeRect.rect} pos={safeRect.position} content={((RectTransform)root).rect} pos={root.position} scale={root.lossyScale}\n");
                Check(!float.IsNaN(root.localScale.x) && root.localScale.x > 0, "Finite layout");
                if (landscape)
                {
                    foreach (var tile in controller.View.LocalTiles) Bounds(tile.Rect, safe, width, height, item.name + " local hand");
                    foreach (var tile in controller.View.ReserveViews) Bounds(tile.Rect, safe, width, height, item.name + " reserve");
                    foreach (var player in controller.View.GetComponentsInChildren<PlayerView>())
                        foreach (string child in new[] { "Avatar", "Name", "Count", "Turn halo" }) Bounds((RectTransform)player.transform.Find(child), safe, width, height, child);
                    foreach (string child in new[] { "Playing surface", "Menu", "Score surface", "Play", "Restart" }) Bounds((RectTransform)root.Find(child), safe, width, height, child);
                    var surface = (RectTransform)root.Find("Playing surface");
                    Check(Mathf.Abs(surface.lossyScale.x - surface.lossyScale.y) < .01f, "Uniform board scale");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        int captureWidth = Mathf.Min(1600, width), captureHeight = Mathf.RoundToInt(height * captureWidth / (float)width);
                        var target = new RenderTexture(captureWidth, captureHeight, 24);
                        camera.orthographicSize = height / 2f; camera.aspect = width / (float)height; camera.targetTexture = target;
                        camera.Render();
                        var previous = RenderTexture.active; RenderTexture.active = target;
                        var pixels = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                        pixels.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(Output, item.name.Replace(' ', '_') + "_" + item.orientation.orientation + ".png"), pixels.EncodeToPNG());
                        RenderTexture.active = previous; camera.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
                    }
                }
                results.Add($"{item.name} | {item.orientation.orientation} | {width}x{height} | {safe} | {(landscape ? "GEOMETRY_PASS" : "FINITE_ONLY_PASS")}");
                frames = 0; index++;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Bounds(RectTransform rect, Rect safe, int width, int height, string name)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = (Vector2)corner + new Vector2(width, height) * .5f;
                Check(point.x >= safe.xMin - 1 && point.x <= safe.xMax + 1 && point.y >= safe.yMin - 1 && point.y <= safe.yMax + 1, name + " outside safe area: " + point);
            }
        }
        static void Check(bool condition, string detail) { checks++; if (!condition) throw new Exception(detail); }
        static void Finish(bool success, string message)
        {
            SessionState.SetBool(Running, false);
            File.WriteAllLines(Path.Combine(Output, "geometry-results.txt"), results);
            File.WriteAllText(Path.Combine(Output, "result.txt"), (success ? "SUCCESS\nCONSOLE_ERRORS=0\n" : "FAILURE\n") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
