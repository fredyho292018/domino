using System;
using System.IO;
using System.Linq;
using Domino.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
    public sealed class ThemeProbeValidation:EditorWindow
    {
        const string Request="Library/UI01Theme.request",Result="Library/UI01Theme.result.txt";
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        public int RequestedViewportWidth=>Sizes[index].x;
        public int RequestedViewportHeight=>Sizes[index].y;
        public Vector2 ActualLogicalViewportSize=>probe.contentViewport.layout.size;
        bool fixtureOnly; bool overflowOnly; int attempted; int index,checks;VisualElement frame;ThemeProbeView probe;
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{if(File.Exists("Library/UI01Fixture.request")&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete("Library/UI01Fixture.request");RunFixture();return;}if(File.Exists("Library/UI01Overflow.request")&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete("Library/UI01Overflow.request");RunOverflow();return;}if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Request);Run();};}
        [MenuItem("Domino/Theme/Validate production theme")]
        public static void Run(){var w=GetWindow<ThemeProbeValidation>();w.titleContent=new GUIContent("Production Theme Probe");w.minSize=new Vector2(440,700);w.overflowOnly=false;w.index=w.checks=0;File.WriteAllText(Result,"RUNNING\n");w.Show();w.Next();}
        void Check(bool ok,string message){attempted++;if(!ok)throw new Exception(message);checks++;}
        void Next()
        {
            rootVisualElement.Clear();frame=new VisualElement();frame.style.position=Position.Absolute;frame.style.left=frame.style.top=0;
            frame.style.flexShrink=0;frame.style.width=frame.style.minWidth=frame.style.maxWidth=RequestedViewportWidth;
            frame.style.height=frame.style.minHeight=frame.style.maxHeight=RequestedViewportHeight;
            frame.style.transformOrigin=new TransformOrigin(0,0,0);float scale=Mathf.Min(1,Mathf.Min(position.width/Sizes[index].x,position.height/Sizes[index].y));frame.style.scale=new Scale(new Vector3(scale,scale,1));rootVisualElement.Add(frame);probe=new ThemeProbeView();frame.Add(probe);rootVisualElement.schedule.Execute(Validate).ExecuteLater(700);
        }
        const string FixtureResult="Library/UI01Fixture.result.txt";
        static void RunFixture(){var w=GetWindow<ThemeProbeValidation>();w.titleContent=new GUIContent("Production Theme Probe");w.minSize=new Vector2(440,700);w.fixtureOnly=true;w.overflowOnly=false;w.index=w.checks=w.attempted=0;File.WriteAllText(FixtureResult,"RUNNING\n");w.Show();w.Next();}
        void ValidateFixture()
        {
            try{
                Check(Mathf.Abs(frame.layout.width-RequestedViewportWidth)<.1f&&Mathf.Abs(frame.layout.height-RequestedViewportHeight)<.1f,"Logical frame exact");
                Check(Mathf.Abs(ActualLogicalViewportSize.x-RequestedViewportWidth)<.1f&&Mathf.Abs(ActualLogicalViewportSize.y-RequestedViewportHeight)<.1f,"Logical application viewport exact");
                var scale=frame.style.scale.value.value;Check(scale.x>0&&scale.x<=1&&Mathf.Abs(scale.x-scale.y)<.0001f,"Uniform preview scaling");
                File.AppendAllText(FixtureResult,"REQUESTED="+Sizes[index]+" ACTUAL="+ActualLogicalViewportSize+" FRAME="+frame.layout.size+" DISPLAY="+position.size+" SCALE="+scale.x+" PASS\n");
                if(++index<Sizes.Length){Next();return;}
                File.AppendAllText(FixtureResult,"VIEWPORT_FIXTURE=5/5_PASS\nCHECKS_PASS="+checks+"\n");
                RunOverflow();
            }catch(Exception e){File.AppendAllText(FixtureResult,"FAIL="+e.Message+"\n");Debug.LogException(e);}
        }
        const string OverflowResult="Library/UI01Overflow.result.txt";
        static void RunOverflow(){var w=GetWindow<ThemeProbeValidation>();w.titleContent=new GUIContent("Production Theme Probe");w.minSize=new Vector2(440,700);w.fixtureOnly=false;w.overflowOnly=true;w.index=w.checks=w.attempted=0;File.WriteAllText(OverflowResult,"RUNNING\n");w.Show();w.Next();}
        void ValidateOverflow()
        {
            try {
                var body=probe.Q("ThemeContent");var viewport=probe.contentViewport;var t=ThemeProvider.Current;
                Check(probe.panel!=null&&viewport.layout.height>0,"Mounted viewport");
                Check(body.layout.width>0&&body.layout.height>0,"Nonblank body");
                Check(body.layout.width<=t.Sizing.ContentMaxWidth+.1f&&body.layout.width<=viewport.layout.width+.1f,"Max width and viewport bounds");
                Check(Mathf.Abs(body.layout.center.x-body.parent.layout.width/2)<1,"Centered content");
                foreach(var child in body.Children())Check(child.layout.x>=-.1f&&child.layout.xMax<=body.layout.width+.1f&&child.layout.height>0,"Horizontal bounds: "+child.name);
                foreach(var name in new[]{"Primary","SecondaryButton","Card","MenuRow","AuthRow","Segments","BottomNavigation"})Check(body.Q(name)!=null,"Coverage: "+name);
                Check(body.Q<TextField>()!=null,"Input coverage");
                var first=body.Children().First();var last=body.Children().Last();
                float height=viewport.layout.height;float bottom=Mathf.Max(0,probe.contentContainer.layout.height-height);
                float firstTop=body.layout.y+first.layout.y;float lastBottom=body.layout.y+last.layout.yMax;
                probe.scrollOffset=Vector2.zero;Check(Mathf.Abs(probe.scrollOffset.y)<.1f&&firstTop>=0&&firstTop+first.layout.height<=height+.1f,"First element reachable");
                probe.scrollOffset=new Vector2(0,bottom);Check(probe.scrollOffset.y>=bottom-1,"Scroll range available if needed");
                Check(lastBottom-probe.scrollOffset.y<=height+1&&lastBottom-last.layout.height-probe.scrollOffset.y>=-1,"Final control fully reachable");
                foreach(var child in body.Children())Check(child.layout.height<=height+1&&body.layout.y+child.layout.y>=0&&body.layout.y+child.layout.yMax<=bottom+height+1,"Control reachable: "+child.name);
                File.AppendAllText(OverflowResult,"PRESET="+Sizes[index]+" OVERFLOW=PASS SCROLL=PASS WIDTH="+body.layout.width+" VIEWPORT_HEIGHT="+height+" CONTENT_HEIGHT="+probe.contentContainer.layout.height+" SCROLL_REQUIRED="+(bottom>0)+" SCROLL_DISTANCE="+bottom+" CENTERED=YES MAX_WIDTH=PASS\n");probe.scrollOffset=Vector2.zero;
                if(++index<Sizes.Length){Next();return;}
                File.AppendAllText(OverflowResult,"CHECKS_RUN="+attempted+"\nCHECKS_PASS="+checks+"\nCHECKS_FAIL=0\n");
            }catch(Exception e){File.AppendAllText(OverflowResult,"CHECKS_RUN="+attempted+"\nCHECKS_PASS="+checks+"\nCHECKS_FAIL=1\nFAIL="+e.Message+"\n");Debug.LogException(e);}
        }
        void Validate()
        {
            if(fixtureOnly){ValidateFixture();return;}if(overflowOnly){ValidateOverflow();return;}
            try{
                var t=ThemeProvider.Resolve();Check(ReferenceEquals(t,ThemeProvider.Current)&&ReferenceEquals(t,probe.Theme),"Shared provider");Check(t.Id==ThemeId.MODERN_SOCIAL_PREMIUM&&t.DisplayName=="ModernSocialPremium","Default ID");
                var colors=new[]{t.Colors.Primary,t.Colors.Background,t.Colors.Surface,t.Colors.TextPrimary,t.Colors.TextSecondary,t.Colors.IconActive,t.Colors.IconInactive};var expected=new[]{"71A84B","2A2623","41403C","FBFAFA","969495","FBFAFA","969495"};for(int i=0;i<colors.Length;i++)Check(ColorUtility.ToHtmlStringRGB(colors[i])==expected[i],"Exact color "+i);
                foreach(TextRole role in Enum.GetValues(typeof(TextRole)))Check(t.Typography[role].Size>0&&t.Typography.FontFor(role)!=null,"Typography "+role);
                Check(t.Typography[TextRole.ButtonPrimary].Weight==700&&t.Typography[TextRole.ButtonSecondary].Weight==600&&t.Typography[TextRole.NavigationLabel].Weight==600,"Weights");
                Check(t.Sizing.MinTouchTarget>=44&&t.Sizing.MenuRowHeight==56&&t.Sizing.ProfileTouchHeight>=44&&t.Sizing.BottomTabTouchHeight==60&&t.Sizing.NavIconLogicalSize==26&&t.Sizing.AuthRowHeight>=48,"Logical sizes");
                Check(t.BottomTabs.SequenceEqual(new[]{"HOME","PUZZLES","LEARN","WATCH","MENU"}),"Future tabs contract");
                Check(probe.panel!=null&&probe.worldBound.width>0&&probe.worldBound.height>0,"Mounted");var body=probe.Q("ThemeContent");Check(body.layout.width<=620.1f&&body.layout.width<=Sizes[index].x+.1f,"Responsive logical width");
                var primary=probe.Q<ThemeButton>("Primary");Check(ReferenceEquals(primary.Theme,t)&&primary.style.backgroundColor.value==t.Colors.Primary,"Primary");Check(primary.style.unityFont.value==t.Typography.FontFor(TextRole.ButtonPrimary),"Primary face");
                Check(primary.CurrentState==ComponentState.Normal,"Normal");
                using(var e=PointerEnterEvent.GetPooled()){e.target=primary;primary.SendEvent(e);}Check(primary.CurrentState==ComponentState.Hover,"Hover");
                using(var e=PointerDownEvent.GetPooled()){e.target=primary;primary.SendEvent(e);}Check(primary.CurrentState==ComponentState.Pressed,"Pressed");
                using(var e=PointerLeaveEvent.GetPooled()){e.target=primary;primary.SendEvent(e);}primary.Blur();Check(primary.CurrentState==ComponentState.Normal,"Pointer release outside");
                bool rejected=false;try{ThemeProvider.Resolve((ThemeId)999);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Unsupported preference rejected");
                primary.Selected=true;Check(primary.CurrentState==ComponentState.Selected&&primary.style.borderTopColor.value==t.Colors.Primary,"Selected");primary.SetEnabled(false);Check(primary.CurrentState==ComponentState.Disabled&&primary.style.color.value==t.Colors.Disabled,"Disabled");primary.SetEnabled(true);primary.Selected=false;
                primary.Focus();Check(primary.CurrentState==ComponentState.Focused,"Focused");primary.Blur();
                var back=probe.Q<ThemeButton>("Back");Check(back.tooltip=="Back"&&back.Q<Image>().vectorImage!=null&&back.layout.height>=44,"Shared back asset");
                Check(probe.Q<Image>("InactiveIcon").tintColor==t.Colors.IconInactive,"Inactive tint");Check(probe.Q("Card").style.backgroundColor.value==t.Colors.Surface,"Selected card surface");
                foreach(var name in new[]{"MenuRow","AuthRow"}){var row=probe.Q<ThemeButton>(name);Check(row.layout.height>=44&&row.Q<Label>("PrimaryLabel")!=null,"Composite row");int before=probe.Actions;using(var e=NavigationSubmitEvent.GetPooled()){e.target=row;row.SendEvent(e);}Check(probe.Actions==before+1,"Row actionable");}
                var field=probe.Q<TextField>();field.value="value";Check(field.Q("ThemePlaceholder").style.display.value==DisplayStyle.None,"Placeholder hides");
                var nav=probe.Q("BottomNavigation");Check(nav!=null&&nav.childCount==5,"Mounted five tabs");
                foreach(var tab in nav.Children()){Check(tab.layout.height>=60&&tab.layout.width>=44,"Tab logical touch");var icon=tab.Q<Image>();Check(icon.layout.width==26&&icon.layout.height==26,"Tab logical icon");}
                Check(Mathf.Abs(body.layout.center.x-body.parent.layout.width/2)<1,"Content centered");
                Check(body.layout.width>0&&body.layout.height>0,"Nonblank body");
                foreach(var child in body.Children())Check(child.layout.x>=-.1f&&child.layout.xMax<=body.layout.width+.1f&&child.layout.height>0,"Content within horizontal bounds: "+child.name);
                Check(probe.contentViewport.layout.height>0,"Visible scroll viewport");
                float bottom=Mathf.Max(0,probe.contentContainer.layout.height-probe.contentViewport.layout.height);probe.scrollOffset=new Vector2(0,bottom);
                Check(probe.scrollOffset.y>=bottom-1,"Bottom content reachable by scrolling");probe.scrollOffset=Vector2.zero;
                Check(probe.Query<VisualElement>().ToList().All(e=>e.layout.width>=0),"Valid layout");
                File.AppendAllText(Result,"PRESET_"+Sizes[index]+"=PASS\n");if(++index<Sizes.Length){Next();return;}File.AppendAllText(Result,"THEME_CHECKS="+checks+"_PASS\nRESPONSIVE_PROBE=5_PASS\n");
            }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
        }
    }
}
