using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public static class MockToolbarValidation
    {
        const string Request="Library/AppShellToolbar.request", Result="Library/AppShellToolbar.result.txt";
        static int device,checks;static MockShellPreview window;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);Run();
        }
        [MenuItem("Domino/App Shell Mock/Validate toolbar")]
        public static void Run()
        {
            checks=0;device=0;File.WriteAllText(Result,"RUNNING\n");
            MockShellPreview.OpenLearn();window=EditorWindow.GetWindow<MockShellPreview>();Next();
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        static void Next()
        {
            window.SetPreviewDevice(device);
            window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(500);
        }
        static void Validate()
        {
            try
            {
                var root=window.rootVisualElement;
                var toolbar=root.Q("MockBottomToolbar");var phone=root.Q("MockPhone");
                Check(toolbar!=null,"Toolbar exists");
                Check(root.Q<Button>("BackAction")==null,"Learn root has no Back");
                var view=root.Q<MockShellView>();
                Check(view.State.Page==MockPage.Learn&&view.State.ActiveTab=="Learn","Learn active route");
                Check(view.Query<Label>().ToList().Any(l=>l.text==view.State.SelectedCoach.DisplayName),"Selected coach visible");
                foreach(var action in new[]{"Continue Learning","Lessons","Puzzles","Coach Games"})
                    Check(view.Query<Button>().ToList().Any(b=>b.text==action),"Learn action "+action);
                var buttons=toolbar.Children().OfType<Button>().ToArray();
                var names=new[]{"Home","Puzzles","Learn","Watch","Menu"};
                Check(buttons.Length==5,"Exactly five entries");
                for(int i=0;i<5;i++)
                {
                    var button=buttons[i];var icon=button.Q<Image>();var label=button.Q<Label>();
                    Check(button.name=="Nav"+names[i]&&label.text==names[i],"Order and label");
                    Check(icon.vectorImage!=null,"Imported vector "+names[i]);
                    Check(icon.tintColor==(i==2?MockShellTheme.Text:MockShellTheme.Inactive),"Icon tint");
                    Check(label.resolvedStyle.color==(i==2?MockShellTheme.Text:MockShellTheme.Inactive),"Label tint");
                    Check(button.style.unityFont.value.name=="SourceSans3-Semibold","Weight 600");
                    Check(button.layout.width>=44&&button.layout.height>=44,"Touch target");
                    // Pixel snapping distributes a fractional fifth-width with at most one pixel difference.
                    Check(Mathf.Abs(button.worldBound.width-buttons[0].worldBound.width)<=1.01f,"Equal width "+button.worldBound.width+" vs "+buttons[0].worldBound.width);
                    const float epsilon=.05f;
                    var firstIcon=buttons[0].Q<Image>();
                    File.AppendAllText(Result,$"ICON device={device} tab={names[i]} configured={icon.style.width.value.value:F3}x{icon.style.height.value.value:F3} resolved={icon.resolvedStyle.width:F3}x{icon.resolvedStyle.height:F3} rendered={icon.worldBound.width:F3}x{icon.worldBound.height:F3} scale={window.PreviewScale:F6}\n");
                    Check(Mathf.Abs(icon.style.width.value.value-26)<epsilon&&Mathf.Abs(icon.style.height.value.value-26)<epsilon,"Configured logical icon box");
                    Check(Mathf.Abs(icon.resolvedStyle.width-26)<epsilon&&Mathf.Abs(icon.resolvedStyle.height-26)<epsilon,"Resolved logical icon box");
                    Check(Mathf.Abs(icon.worldBound.width-firstIcon.worldBound.width)<epsilon&&Mathf.Abs(icon.worldBound.height-firstIcon.worldBound.height)<epsilon,"Uniform rendered icon boxes");
                    Check(Mathf.Abs(icon.worldBound.width-26*window.PreviewScale)<epsilon&&Mathf.Abs(icon.worldBound.height-26*window.PreviewScale)<epsilon,"Uniform preview scale");
                    Check(Mathf.Abs(icon.worldBound.center.x-button.worldBound.center.x)<.51f&&Mathf.Abs(icon.worldBound.yMin-firstIcon.worldBound.yMin)<epsilon,"Icon alignment");
                    Check(label.worldBound.yMax<=toolbar.worldBound.yMax+.5f,"Label bottom");
                    Check(icon.worldBound.yMin>=toolbar.worldBound.yMin-.5f,"Icon top");
                    Check(button.worldBound.xMin>=phone.worldBound.xMin&&button.worldBound.xMax<=phone.worldBound.xMax+.5f,"Horizontal bounds");
                }
                Check(toolbar.worldBound.yMax<=phone.worldBound.yMax-phone.resolvedStyle.paddingBottom*window.PreviewScale+.5f,"Bottom safe inset");
                File.AppendAllText(Result,"DEVICE_"+device+"=PASS\n");
                if(++device<MockShellPreview.PreviewSizes.Length){Next();return;}
                File.AppendAllText(Result,"TOOLBAR_CHECKS="+checks+"_PASS\nSAFE_AREA=SIMULATED_INSETS_PASS\n");
                MockShellPreview.OpenLearn();
            }
            catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
        }
    }
}
