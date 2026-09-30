using System;
using System.IO;
using System.Linq;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed partial class ProductionShellPreview : EditorWindow
    {
        static readonly Vector2Int[] Sizes = { new Vector2Int(375,667), new Vector2Int(393,852), new Vector2Int(412,915), new Vector2Int(430,932), new Vector2Int(480,1040), new Vector2Int(600,960), new Vector2Int(768,1024), new Vector2Int(834,1194) };
        const string Result = "Library/UI02A.result.txt";
        int index, checks; ProductionAppShell shell; VisualElement frame;
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.update += () => {
                if(File.Exists("Library/HomeGeometry.request")&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete("Library/HomeGeometry.request");var d=GetWindow<ProductionShellPreview>();d.index=0;File.WriteAllText("Library/HomeGeometry.result.txt","FOCUSED_CENTERING=YES\n");d.DiagnosticMount();return;}
                if (!File.Exists("Library/UI02B1.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete("Library/UI02B1.request"); var w = GetWindow<ProductionShellPreview>();
                w.StartResizeTests();
            };
        }
        [MenuItem("Domino/Production App Shell/Open preview")]
        public static void Open() { var w = GetWindow<ProductionShellPreview>(); w.index = 0; w.Mount(false); }
        void Resize(int target) { index = target; Mount(false, true); }
        void Mount(bool test, bool preserveRoute = false)
        {
            var route = preserveRoute && shell != null ? shell.ActiveTab : ShellTab.Home;
            var profileSource=preserveRoute && shell!=null?shell.ProfileDataSource:null;
            var profileSection=preserveRoute && shell!=null?shell.ActiveProfileSection:ProfileSection.Profile;
            var rootDestination=preserveRoute && shell!=null?shell.ActiveRootDestination:null;
            var destination=preserveRoute && shell!=null?shell.ActiveMenuDestination:null;
            bool detail = preserveRoute && shell != null && shell.HasSubpage;
            titleContent = new GUIContent("PRODUCTION APP SHELL"); minSize = new Vector2(440,700); Show();
            rootVisualElement.Clear();
            var selector = new PopupField<string>(Sizes.Select(s => s.x + "x" + s.y).ToList(), index);
            selector.RegisterValueChangedCallback(e => { Resize(selector.index); }); rootVisualElement.Add(selector);
            frame = new VisualElement(); frame.style.position = Position.Absolute; frame.style.top = 28;
            frame.style.width = frame.style.minWidth = frame.style.maxWidth = Sizes[index].x;
            frame.style.height = frame.style.minHeight = frame.style.maxHeight = Sizes[index].y; frame.style.flexShrink = 0;
            float scale = Mathf.Min(1, Mathf.Min(position.width / Sizes[index].x, (position.height - 28) / Sizes[index].y));
            frame.style.transformOrigin = new TransformOrigin(0,0,0); frame.style.scale = new Scale(new Vector3(scale,scale,1));
            rootVisualElement.Add(frame); shell = new ProductionAppShell(profileSource); frame.Add(shell);
            shell.SetSafeArea(0,24,0,24);
            shell.Select(route); if(detail) { if(rootDestination.HasValue)shell.OpenRootDestination(rootDestination.Value);else if(destination.HasValue)shell.OpenMenuDestination(destination.Value);else shell.OpenDetail(); if(destination==MenuDestination.Profile)shell.OpenProfileSection(profileSection); }
            if (test) rootVisualElement.schedule.Execute(Test).ExecuteLater(500);
        }
        void DiagnosticMount(){Mount(false);rootVisualElement.schedule.Execute(CaptureGeometry).ExecuteLater(700);}
        static float LogicalX(VisualElement e,VisualElement root){float x=0;while(e!=root){x+=e.layout.x;e=e.parent;}return x;}
        void CaptureGeometry()
        {
            var page=shell.Pages[0];var body=page.Body;float left=LogicalX(body,shell),safeLeft=shell.resolvedStyle.paddingLeft,safeWidth=shell.layout.width-safeLeft-shell.resolvedStyle.paddingRight;
            float expectedWidth=Mathf.Min(safeWidth,ThemeProvider.Current.Sizing.ContentMaxWidth),expectedLeft=safeLeft+(safeWidth-expectedWidth)/2;
            string text="PRESET="+Sizes[index]+" VIEWPORT="+shell.layout.size+" SAFE_LEFT="+safeLeft+" SAFE_RIGHT="+shell.resolvedStyle.paddingRight+" SAFE_WIDTH="+safeWidth+" EXPECTED_WIDTH="+expectedWidth+" EXPECTED_LEFT="+expectedLeft+" EXPECTED_CENTER="+(safeLeft+safeWidth/2)+" ACTUAL_LEFT="+left+" ACTUAL_RIGHT="+(left+body.layout.width)+" ACTUAL_CENTER="+(left+body.layout.width/2)+" DELTA="+(left+body.layout.width/2-safeLeft-safeWidth/2)+" LEFT_MARGIN="+(left-safeLeft)+" RIGHT_MARGIN="+(safeLeft+safeWidth-left-body.layout.width)+"\n";
            foreach(var e in new VisualElement[]{shell,shell.PageHost,page,page.contentViewport,page.contentContainer,body}){
                var r=e.resolvedStyle;
                text+="ELEMENT="+(string.IsNullOrEmpty(e.name)?e.GetType().Name:e.name)+" LOGICAL_X="+LogicalX(e,shell)+" RECT="+e.layout+" position="+r.position+" left="+r.left+" right="+r.right+" minWidth="+r.minWidth+" maxWidth="+r.maxWidth+" margin="+r.marginLeft+","+r.marginRight+" padding="+r.paddingLeft+","+r.paddingRight+" alignSelf="+r.alignSelf+" alignItems="+r.alignItems+" justify="+r.justifyContent+" grow="+r.flexGrow+" shrink="+r.flexShrink+"\n";
            }
            File.AppendAllText("Library/HomeGeometry.result.txt",text);
            if(Mathf.Abs(left+body.layout.width/2-safeLeft-safeWidth/2)>1 || body.layout.width>620.1f || left<safeLeft-.1f || left+body.layout.width>safeLeft+safeWidth+.1f){File.AppendAllText("Library/HomeGeometry.result.txt","FAIL=CENTER_OR_BOUNDS\n");return;}
            if(++index<Sizes.Length)DiagnosticMount();else{File.AppendAllText("Library/HomeGeometry.result.txt","HOME_CENTERING=8/8_PASS\nCOMPLETE=YES\n");StartResizeTests();}
        }
        int resizeStep, resizeChecks;
        const string ResizeResult="Library/UI02AResize.result.txt";
        void StartResizeTests()
        {
            resizeStep=resizeChecks=0;index=0;Mount(false);
            File.WriteAllText(ResizeResult,"RUNNING\n");
            rootVisualElement.schedule.Execute(ResizeTest).ExecuteLater(200);
        }
        void ResizeTest()
        {
            try {
                if(resizeStep==0 && shell.ActiveTab!=ShellTab.Home)throw new Exception("New preview must open Home");
                var tab=(ShellTab)(resizeStep/8);int from=resizeStep%8;int to=(from+1)%8;
                shell.Select(tab);Resize(to);
                rootVisualElement.schedule.Execute(()=>{
                    try {
                        if(shell.ActiveTab!=tab)throw new Exception("Route lost: "+tab);
                        if(Mathf.Abs(shell.layout.width-Sizes[to].x)>.1f||Mathf.Abs(shell.layout.height-Sizes[to].y)>.1f)throw new Exception("Resize logical dimensions");
                        resizeChecks++;File.AppendAllText(ResizeResult,tab+" "+Sizes[from]+" -> "+Sizes[to]+" PASS\n");
                        if(++resizeStep<40){ResizeTest();return;}
                        shell.Select(ShellTab.Menu);shell.OpenDetail();Resize(1);
                        if(!shell.HasSubpage||shell.ActiveTab!=ShellTab.Menu)throw new Exception("Detail lost");
                        shell.Back();if(shell.ActiveTab!=ShellTab.Menu||shell.HasSubpage)throw new Exception("Restored detail Back");
                        resizeChecks++;
                        File.AppendAllText(ResizeResult,"RUN="+resizeChecks+"\nPASS="+resizeChecks+"\nFAIL=0\n");
                        index=checks=0;File.WriteAllText(Result,"RUNNING\n");Mount(true);
                    }catch(Exception e){ResizeFail(e);}
                }).ExecuteLater(200);
            }catch(Exception e){ResizeFail(e);}
        }
        void ResizeFail(Exception e){File.AppendAllText(ResizeResult,"PASS="+resizeChecks+"\nFAIL=1\n"+e.Message);Debug.LogException(e);}
        void FinishDemonstration()
        {
            Resize(1);shell.Select(ShellTab.Learn);Resize(6);
            if(shell.ActiveTab!=ShellTab.Learn)throw new Exception("Learn demonstration");
            shell.Select(ShellTab.Menu);Resize(0);
            if(shell.ActiveTab!=ShellTab.Menu)throw new Exception("Menu demonstration");
            index=1;Mount(false);File.AppendAllText(Result,"DEMONSTRATION=PASS FINAL=HOME_393x852\n");StartMenuTests();
        }
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        static void Click(VisualElement e) { using(var evt = NavigationSubmitEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); } }
        void Test()
        {
            try {
                Check(Mathf.Abs(shell.layout.width-Sizes[index].x)<1 && Mathf.Abs(shell.layout.height-Sizes[index].y)<1,"Logical viewport");
                Check(ReferenceEquals(shell.Theme,ThemeProvider.Current),"Provider");
                Check(shell.BottomNavigation.childCount==5 && shell.BottomNavigation[2].name=="TabLearn","Center Learn");
                for(int i=0;i<5;i++) {
                    Click(shell.Q("Tab"+(ShellTab)i)); Check(shell.ActiveTab==(ShellTab)i,"Direct tab route");
                    Check(shell.BottomNavigation.Children().Cast<ThemeButton>().Count(x=>x.Selected)==1,"Single active tab");
                    Check(shell.Q("ShellBack")==null,"Root no back");
                    var tab=shell.Q<ThemeButton>("Tab"+(ShellTab)i); Check(tab.layout.height>=60,"Tab touch");
                    Check(tab.Q<Image>().vectorImage!=null && tab.Q<Image>().layout.width==26,"Icon contract");
                }
                Click(shell.Q("MenuProfile")); Check(shell.HasSubpage,"Detail route");
                // Wait for layout of newly mounted detail/header.
                rootVisualElement.schedule.Execute(Finish).ExecuteLater(300);
            } catch(Exception e) { Fail(e); }
        }
        void Finish()
        {
            try {
                var back=shell.Q<Button>("ShellBack"); Check(back.layout.height>=44 && back.layout.width>=44,"Back touch");
                Click(back); Check(!shell.HasSubpage && shell.ActiveTab==ShellTab.Menu,"Back parent");
                shell.Select(ShellTab.Home);
                rootVisualElement.schedule.Execute(LayoutChecks).ExecuteLater(300);
            } catch(Exception e) { Fail(e); }
        }
        void LayoutChecks()
        {
            try {
                var page=shell.Pages[0]; var body=page.Body;
                Check(page is ProductionHomePage && shell.Pages.Count(p=>p is ProductionHomePage)==1,"Single production Home");
                Check(ReferenceEquals(((ProductionHomePage)page).Theme,ThemeProvider.Current),"Home provider");
                foreach(var name in new[]{"HomeGreeting","HomePlayCard","HomeCoachCard","HomeContinueLearning","HomeFriendsCard"})Check(page.Q(name)!=null,"Home section "+name);
                Check(page.Q<Label>("HomeGreeting").text=="Hola, Alex.","Demo data greeting");
                Check(page.Q("HomePlayCard").Q<Label>().text=="\u00bfJugamos?","UTF8 Play title");
                Check(page.Q<Button>("HomePlay").text=="PLAY \u2192","UTF8 Play CTA");
                Check(page.Q<Label>("HomeSubtitle").text=="Una buena partida empieza con una buena mesa.","UTF8 subtitle");
                Check(page.Q("HomeCoachCard").Query<Label>().ToList().Any(x=>x.text=="Hola, soy Amara.\nTe ense\u00f1ar\u00e9 a jugar domin\u00f3."),"UTF8 coach greeting");
                Check(page.Query<TextElement>().ToList().All(x=>x.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0),"UTF8 no mojibake");
                var glyphFont=ThemeProvider.Current.Typography.FontFor(TextRole.Body);
                const string glyphs="\u00e1\u00e9\u00ed\u00f3\u00fa\u00f1\u00bf\u00a1\u2192\u00b7";
                glyphFont.RequestCharactersInTexture(glyphs,15,FontStyle.Normal);
                foreach(char glyph in glyphs)Check(glyphFont.HasCharacter(glyph),"UTF8 glyph U+"+((int)glyph).ToString("X4"));
                Check(page.Q("HomeCoachPortrait").Q<Image>().image!=null,"Amara asset");
                Check(page.Q("BottomNavigation")==null,"Home has no toolbar");
                Click(page.Q("HomePlay"));Check(page.Q("HomePlayNotice").style.display.value==DisplayStyle.Flex && shell.ActiveTab==ShellTab.Home,"Play local notice only");
                page.Q("HomePlayNotice").style.display=DisplayStyle.None;
                Click(page.Q("HomeContinueLearning"));Check(shell.ActiveTab==ShellTab.Learn,"Continue Learning route");
                Click(shell.Q("TabHome"));Check(shell.ActiveTab==ShellTab.Home,"Home return");
                foreach(HomeContentState state in Enum.GetValues(typeof(HomeContentState))) {
                    if(state==HomeContentState.Content)continue;
                    var statePage=new ProductionHomePage(new StateSource(state),()=>{});
                    Check(statePage.ContentState==state && statePage.Q("HomeStatus")!=null && statePage.Q("HomePlay")==null,"Home state "+state);
                }
                rootVisualElement.schedule.Execute(HomeGeometryChecks).ExecuteLater(300);
            } catch(Exception e) { Fail(e); }
        }
        void HomeGeometryChecks()
        {
            try {
                var page=shell.Pages[0];var body=page.Body;
                float naturalEnd=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);
                foreach(var child in body.Children()) {
                    Check(child.layout.x>=0 && child.layout.xMax<=body.layout.width+.1f,"Home horizontal bounds "+child.name);
                    Check(child.layout.height<=page.contentViewport.layout.height && body.layout.y+child.layout.yMax<=naturalEnd+page.contentViewport.layout.height+1,"Home reachable "+child.name);
                }
                File.AppendAllText(Result,"HOME_SIZE="+Sizes[index]+" CONTENT="+page.contentContainer.layout.height+" VIEWPORT="+page.contentViewport.layout.height+" SCROLL_REQUIRED="+(naturalEnd>0)+"\n");
                Check(body.layout.width<=620.1f && body.layout.width<=Sizes[index].x,"Content max width");
                float safeLeft=shell.resolvedStyle.paddingLeft,safeWidth=shell.layout.width-safeLeft-shell.resolvedStyle.paddingRight;
                float bodyLeft=LogicalX(body,shell),expectedCenter=safeLeft+safeWidth/2;
                Check(Mathf.Abs(bodyLeft+body.layout.width/2-expectedCenter)<1,"Centered in safe area");
                Check(Mathf.Abs((bodyLeft-safeLeft)-(safeLeft+safeWidth-bodyLeft-body.layout.width))<1,"Symmetric margins");
                File.AppendAllText(Result,"CENTER="+Sizes[index]+" WIDTH="+body.layout.width+" LEFT="+bodyLeft+" DELTA="+(bodyLeft+body.layout.width/2-expectedCenter)+" PASS\n");
                Check(shell.PageHost.layout.y>=24 && shell.BottomNavigation.layout.yMax<=Sizes[index].y-24+.1f,"Safe area");
                Check(shell.PageHost.layout.yMax<=shell.BottomNavigation.layout.y+.1f,"No toolbar overlap");
                Check(shell.BottomNavigation.layout.x>=0 && shell.BottomNavigation.layout.xMax<=Sizes[index].x,"No horizontal overflow");
                var filler=new VisualElement(); filler.style.height=2000; filler.style.flexShrink=0; body.Add(filler);
                rootVisualElement.schedule.Execute(()=>ScrollChecks(filler)).ExecuteLater(200);
            } catch(Exception e) { Fail(e); }
        }
        void ScrollChecks(VisualElement filler)
        {
            try {
                var page=shell.Pages[0];float toolbarY=shell.BottomNavigation.layout.y;
                float end=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);
                page.scrollOffset=new Vector2(0,end); Check(end>0 && page.scrollOffset.y>=end-1,"Application scroll");
                shell.Select(ShellTab.Menu);shell.Select(ShellTab.Home);
                Check(ReferenceEquals(page,shell.Pages[0]) && page.scrollOffset.y>=end-1,"Retained root state");
                Check(shell.BottomNavigation.layout.y==toolbarY,"Fixed toolbar"); filler.RemoveFromHierarchy();page.scrollOffset=Vector2.zero;
                File.AppendAllText(Result,"PRESET="+Sizes[index]+" PASS\n");
                if(++index<Sizes.Length){Mount(true);return;}
                File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\n");FinishDemonstration();
            } catch(Exception e) { Fail(e); }
        }
        sealed class StateSource : IHomeDataSource
        {
            readonly HomeContentState state;
            public StateSource(HomeContentState state){this.state=state;}
            public HomeSummary Read()=>new HomeSummary(state);
        }
        void Fail(Exception e) { File.AppendAllText(Result,"FAIL="+e.Message+"\n");Debug.LogException(e); }
    }
}
