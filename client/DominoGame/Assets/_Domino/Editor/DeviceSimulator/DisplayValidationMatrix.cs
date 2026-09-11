using System;
using System.IO;
using Domino.Client;
using Domino.UI;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;
using Screen = UnityEngine.Device.Screen;

namespace Domino.Editor
{
    [Serializable] public sealed class DisplayProfile { public string friendlyName; public DisplayScreen[] screens; }
    [Serializable] public sealed class DisplayScreen { public int width, height; public float dpi; public DisplayOrientation[] orientations; }
    [Serializable] public sealed class DisplayRect
    {
        public float x, y, width, height;
        public Rect ToRect() => new Rect(x, y, width, height);
    }
    [Serializable] public sealed class DisplayOrientation { public int orientation; public DisplayRect safeArea; public DisplayRect[] cutouts; }

    public sealed class DisplayValidationMatrix : EditorWindow
    {
        public const string ProfileRoot = "Assets/_Domino/Editor/DeviceSimulator";
        Vector2 scroll;
        [MenuItem("Domino/Display Validation Matrix")]
        public static void Open() => GetWindow<DisplayValidationMatrix>("Display validation");
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Selecciona el perfil desde Device Simulator y prueba Portrait (Landscape se conserva solo para desarrollo). iPhone: safe area documentada, sin silueta exacta de Dynamic Island. Android Synthetic: escenarios de prueba, no modelos físicos.", MessageType.Info);
            if (GUILayout.Button("Abrir documentación")) EditorUtility.RevealInFinder(Path.GetFullPath("../DEVICE_TEST_MATRIX.md"));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in Directory.GetFiles(ProfileRoot, "*.device"))
            {
                var profile = JsonUtility.FromJson<DisplayProfile>(File.ReadAllText(path));
                if (GUILayout.Button($"{profile.friendlyName}   {profile.screens[0].width} × {profile.screens[0].height}"))
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // Public Unity extension API; users select the device/orientation in the Simulator itself.
    public sealed class DominoDisplayValidationPlugin : DeviceSimulatorPlugin
    {
        public override string title => "Domino · Safe Area";
        public override VisualElement OnCreateUI()
        {
            var root = new VisualElement();
            var status = new Label("Entra en Play y termina el reparto antes de validar.");
            status.style.whiteSpace = WhiteSpace.Normal;
            root.Add(status);
            root.Add(new Button(() => status.text = ValidateCurrent()) { text = "Validar layout actual" });
            return root;
        }
        static string ValidateCurrent()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
            if (!Application.isPlaying || !controller || !controller.View || controller.View.IsPreparingRound) return "Pendiente: inicia una partida y espera el reparto.";
            int failures = 0;
            foreach (var tile in controller.View.LocalTiles)
            {
                var corners = new Vector3[4]; tile.Rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                    if (!Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner))) failures++;
            }
            return $"{Screen.width} × {Screen.height} · {Screen.orientation}\nSafe Area: {Screen.safeArea}\nCutouts: {Screen.cutouts.Length}\nMano local: {(failures == 0 ? "PASS" : "FAIL")}\nRevisar visualmente mesa, HUD, avatares y contacto con cutouts. No mide rendimiento físico.";
        }
    }
}
