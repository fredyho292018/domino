using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    // Explicit local validation request only. Never starts Play or loads a product scene.
    public static class MockShellPreviewValidation
    {
        const string Request = "Library/AppShellMockPreviewValidation.request";
        const string Result = "Library/AppShellMockPreviewValidation.result.txt";

        [InitializeOnLoadMethod]
        static void Register() { EditorApplication.update += Poll; }

        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Domino/App Shell Mock/Validate welcome routing")]
        public static void Run()
        {
            int checks=0;
            try
            {
                MockShellPreview.Open();
                var window=EditorWindow.GetWindow<MockShellPreview>();
                var view=window.rootVisualElement.Q<Domino.AppShellMock.MockShellView>();
                void Check(bool value,string label) { if(!value) throw new Exception(label);checks++; }
                Check(view!=null,"Mock view mounted");
                Check(view.State.Page==Domino.AppShellMock.MockPage.Welcome,"Welcome initial route");
                Check(view.State.AccountType=="NONE","No session");
                Check(view.name=="WelcomeAuthRoot","Welcome/Auth root");
                var buttons=view.Query<Button>().ToList().Select(b=>b.text).ToArray();
                string[] providers={"Google","Facebook","Email","Phone","Guest"};
                string[] labels={"Continue with Google","Continue with Facebook","Continue with Email","Continue with Phone","Continue as Guest"};
                string[] descriptions={"Fast, secure and easy","Play with your friends","Use your email and password","Sign in with your phone number","Play now, create an account later"};
                var routes=new[]{Domino.AppShellMock.MockPage.Provider,Domino.AppShellMock.MockPage.Provider,Domino.AppShellMock.MockPage.EmailRegister,Domino.AppShellMock.MockPage.Phone,Domino.AppShellMock.MockPage.Experience};
                Check(view.Query<Button>(className:"welcome-auth-row").ToList().Count==5,"Five provider roots");
                for(int i=0;i<providers.Length;i++)
                {
                    var row=view.Q<Button>(labels[i],"welcome-auth-row");
                    Check(row!=null && row.enabledInHierarchy && row.clickable!=null && row.pickingMode==PickingMode.Position,"Actionable root: "+providers[i]);
                    Check(row.Q<Label>("AuthPrimaryLabel")?.text==labels[i],"Primary label: "+providers[i]);
                    Check(row.Q<Label>("AuthSecondaryLabel")?.text==descriptions[i],"Secondary label: "+providers[i]);
                    Check(row.Q<Image>("AuthProviderIcon")?.vectorImage!=null,"Provider icon: "+providers[i]);
                    Check(row.Q<Label>("AuthPrimaryLabel").pickingMode==PickingMode.Ignore && row.Q<Label>("AuthSecondaryLabel").pickingMode==PickingMode.Ignore && row.Q<Image>("AuthProviderIcon").pickingMode==PickingMode.Ignore,"Composite content passes input to root");
                    Check(row.tooltip==labels[i] && row.focusable,"Named keyboard target");
                    using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=row;row.SendEvent(submit);}
                    Check(view.State.Provider==providers[i] && view.State.Page==routes[i],"Provider action: "+providers[i]);
                    view.State.Start(Domino.AppShellMock.MockEntry.NO_SESSION);view.Rebuild();
                }
                Check(view.Q("WelcomeSignInRow")?.Q<Button>()?.text=="Sign In","Inline Sign In");
                foreach(var label in new[]{"Jugar","Historial","Social","Salir"})
                    Check(!buttons.Contains(label),"Legacy action "+label);
                Check(Enum.GetValues(typeof(Domino.AppShellMock.MockEntry)).Length==9,"Nine development states");
                view.State.Start(Domino.AppShellMock.MockEntry.EXISTING_REGISTERED);
                MockShellPreview.Open();
                view=window.rootVisualElement.Q<Domino.AppShellMock.MockShellView>();
                Check(view.State.Page==Domino.AppShellMock.MockPage.Welcome && view.State.AccountType=="NONE","Reopening resets session");
                int themeChecks=0;
                void ThemeCheck(bool value,string label) { if(!value)throw new Exception(label);themeChecks++; }
                ThemeCheck(ColorUtility.ToHtmlStringRGB(Domino.AppShellMock.MockShellTheme.Primary)=="71A84B","Primary token");
                ThemeCheck(ColorUtility.ToHtmlStringRGB(view.style.backgroundColor.value)=="2A2623","Background token");
                ThemeCheck(ColorUtility.ToHtmlStringRGB(Domino.AppShellMock.MockShellTheme.Surface)=="41403C","Surface token");
                ThemeCheck(ColorUtility.ToHtmlStringRGB(Domino.AppShellMock.MockShellTheme.Text)=="FBFAFA","Active token");
                ThemeCheck(ColorUtility.ToHtmlStringRGB(Domino.AppShellMock.MockShellTheme.Inactive)=="969495","Inactive token");
                var primary=view.Query<Button>().ToList().First(b=>b.name=="Continue with Google");
                ThemeCheck(primary.style.backgroundColor.value==Domino.AppShellMock.MockShellTheme.Primary,"Primary button background");
                ThemeCheck(primary.style.color.value==Domino.AppShellMock.MockShellTheme.Text,"Primary button text");
                ThemeCheck(primary.Q<Label>("AuthPrimaryLabel").style.unityFont.value.name=="SourceSans3-Bold","Real bold primary face");
                var secondary=view.Query<Button>().ToList().First(b=>b.name=="Continue with Facebook");
                ThemeCheck(secondary.Q<Label>("AuthPrimaryLabel").style.unityFont.value.name=="SourceSans3-Bold","Composite provider title bold");
                var tertiary=new Button(){text="Skip"};Domino.AppShellMock.MockButtonTypography.Apply(tertiary,false);
                ThemeCheck(tertiary.style.unityFont.value.name=="SourceSans3-Medium","Real medium tertiary face");
                var selection=new VisualElement();Domino.AppShellMock.MockShellTheme.Selection(selection,true);
                ThemeCheck(selection.style.backgroundColor.value==Domino.AppShellMock.MockShellTheme.Surface,"Selected surface remains dark");
                ThemeCheck(selection.style.borderTopColor.value==Domino.AppShellMock.MockShellTheme.Primary,"Selected border");
                int backChecks=0;
                foreach(Domino.AppShellMock.MockPage page in Enum.GetValues(typeof(Domino.AppShellMock.MockPage)))
                {
                    if(page==Domino.AppShellMock.MockPage.Welcome||page==Domino.AppShellMock.MockPage.Experience
                       ||page==Domino.AppShellMock.MockPage.Home||page==Domino.AppShellMock.MockPage.Puzzles
                       ||page==Domino.AppShellMock.MockPage.Learn||page==Domino.AppShellMock.MockPage.Watch||page==Domino.AppShellMock.MockPage.Menu)continue;
                    var state=new Domino.AppShellMock.MockShellState();state.PlaceholderTitle="Preview";state.Go(page);
                    var sample=new Domino.AppShellMock.MockShellView(state,true);
                    var back=sample.Q<Button>("BackAction");var icon=back?.Q<Image>("BackIcon");
                    if(back==null||!string.IsNullOrEmpty(back.text)||icon?.vectorImage==null
                       ||icon.tintColor!=Domino.AppShellMock.MockShellTheme.Text
                       ||back.style.width.value.value<44||back.style.height.value.value<44
                       ||back.tooltip!="Back"||back.clickable==null||!back.enabledInHierarchy||!back.focusable
                       ||icon.vectorImage!=Resources.Load<VectorImage>("AppShellMockIcons/icon_arrow_left")
                       ||sample.Query<Button>().ToList().Any(b=>b.text=="Back"||b.text=="Volver"||b.text=="Back to Diamond offer"))
                        throw new Exception("Back icon contract: "+page);
                    window.rootVisualElement.Add(sample);
                    using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=back;back.SendEvent(submit);}
                    var expected=page==Domino.AppShellMock.MockPage.CoachSelection?Domino.AppShellMock.MockPage.Experience:page==Domino.AppShellMock.MockPage.Contacts?Domino.AppShellMock.MockPage.CoachSelection:page==Domino.AppShellMock.MockPage.Trial?Domino.AppShellMock.MockPage.Contacts:Domino.AppShellMock.MockPage.Welcome;
                    if(state.Page!=expected)throw new Exception("Back route contract: "+page);
                    sample.RemoveFromHierarchy();
                    backChecks++;
                }
                window.rootVisualElement.schedule.Execute(()=>
                {
                    bool mounted=view.panel!=null && view.worldBound.width>0 && view.worldBound.height>0;
                    File.WriteAllText(Result,"PREVIEW_ROUTING_TESTS="+checks+"_PASS\nTHEME_TESTS="+themeChecks+"_PASS\nBACK_ICON_PAGES="+backChecks+"_PASS\nPREVIEW_PANEL_MOUNTED="+mounted+"\nPREVIEW_INITIAL_STATE=NO_SESSION\n");
                    Debug.Log("APP_SHELL_PREVIEW_ROUTING="+checks+"_PASS PANEL_MOUNTED="+mounted);
                    window.Focus();
                }).ExecuteLater(500);
            }
            catch(Exception ex)
            {
                File.WriteAllText(Result,"PREVIEW_ROUTING_TESTS=FAIL\nREASON="+ex.Message);
                Debug.LogException(ex);
            }
        }
    }
}
