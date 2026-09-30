using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public static class MockMenuValidation
    {
        const string Request="Library/AppShellMenu.request",Result="Library/AppShellMenu.result.txt";
        static MockShellPreview window;static int device,checks;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            try{File.Delete(Request);}catch(IOException){return;}Run();
        }
        [MenuItem("Domino/App Shell Mock/Validate Menu rows")]
        public static void Run(){device=0;checks=0;File.WriteAllText(Result,"RUNNING\n");MockShellPreview.OpenMenu();window=EditorWindow.GetWindow<MockShellPreview>();Next();}
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static void Next(){window.SetPreviewDevice(device);window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(700);}
        static void Validate()
        {
            try
            {
                var view=window.rootVisualElement.Q<MockShellView>();
                var routes=new[]{MockPage.Friends,MockPage.Messages,MockPage.Stats,MockPage.Coach,MockPage.Theme,MockPage.Membership,MockPage.Settings,MockPage.Support};
                var rows=view.Query<Button>().ToList().Where(b=>b.name.StartsWith("MenuRow")).ToArray();
                Check(rows.Length==8,"Eight rows");
                float labelX=rows[0].Q<Label>().worldBound.xMin,iconX=rows[0].Q<Image>().worldBound.xMin,right=rows[0].Q<Image>("MenuRowChevron").worldBound.xMax;
                for(int i=0;i<8;i++)
                {
                    var row=rows[i];var label=row.Q<Label>();var icon=row.Q<Image>();var chevron=row.Q<Image>("MenuRowChevron");
                    Check(row.name=="MenuRow"+routes[i],"Row order");
                    Check(label.text==(i==7?"Help & Support":routes[i].ToString()),"Label");
                    Check(icon.vectorImage!=null&&chevron.vectorImage!=null,"Vector assets");
                    Check(label.resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft,"Left aligned");
                    Check(Mathf.Abs(label.worldBound.xMin-labelX)<1&&Mathf.Abs(icon.worldBound.xMin-iconX)<1&&Mathf.Abs(chevron.worldBound.xMax-right)<1,"Shared alignment axes");
                    File.AppendAllText(Result,$"TOUCH device={device} row={routes[i]} visualLogical={row.resolvedStyle.height:F3} clickableLogical={row.layout.height:F3} rendered={row.worldBound.height:F3} scale={window.PreviewScale:F6} root={row.name}\n");
                    Check(row.layout.height>=44,"Logical touch height");
                    Check(row.clickable!=null&&row.enabledInHierarchy&&row.pickingMode==PickingMode.Position,"Entire row actionable");
                    Check(Mathf.Abs(row.worldBound.height-row.layout.height*window.PreviewScale)<.05f,"Touch root uniform preview scale");
                    Check(label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x<=label.layout.width+1,"Label fits");
                    Check(row.style.unityFont.value.name=="SourceSans3-Semibold","Weight 600");
                    Check(icon.tintColor==MockShellTheme.Inactive&&chevron.tintColor==MockShellTheme.Inactive,"Icon colors");
                }
                var profile=view.Q<Button>("MenuProfile");
                File.AppendAllText(Result,$"PROFILE device={device} logical={profile.layout.height:F3} rendered={profile.worldBound.height:F3}\n");
                Check(profile.layout.height>=44&&profile.clickable!=null&&profile.enabledInHierarchy&&profile.pickingMode==PickingMode.Position,"Profile header logical touch");
                foreach(var tab in new[]{"Home","Puzzles","Learn","Watch","Menu"}){
                    var target=view.Q<Button>("Nav"+tab);
                    Check(target.layout.height>=44&&target.layout.width>=44&&target.clickable!=null,"Bottom nav logical touch "+tab);
                    File.AppendAllText(Result,$"NAV device={device} tab={tab} logical={target.layout.width:F3}x{target.layout.height:F3}\n");
                }
                foreach(var route in routes)
                {
                    var row=view.Q<Button>("MenuRow"+route);
                    Check(row.Children().All(c=>c.pickingMode==PickingMode.Ignore),"Whole row target");
                    using(var e=NavigationSubmitEvent.GetPooled()){e.target=row;row.SendEvent(e);}
                    Check(view.State.Page==route,"Route "+route);view.State.Back();view.Rebuild();
                    Check(view.State.Page==MockPage.Menu&&view.State.ActiveTab=="Menu","Back to Menu");
                }
                var nav=view.Q<Button>("NavMenu");Check(nav.Q<Image>().tintColor==MockShellTheme.Text,"Menu tab active");
                File.AppendAllText(Result,"DEVICE_"+device+"=PASS\n");
                if(++device<MockShellPreview.PreviewSizes.Length){Next();return;}
                File.AppendAllText(Result,"MENU_CHECKS="+checks+"_PASS\n");MockShellPreview.OpenMenu();
            }
            catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
        }
    }
}
