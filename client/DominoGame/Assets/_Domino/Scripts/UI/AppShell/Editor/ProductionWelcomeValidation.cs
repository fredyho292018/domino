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
    public sealed class ProductionWelcomeValidation : EditorWindow
    {
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        const string Result="Library/Auth01Welcome.result.txt";
        int index,checks,guestCalls;bool testing;
        VisualElement frame;ProductionWelcomeView welcome;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=()=>{
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists("Library/Auth01.request")){File.Delete("Library/Auth01.request");var w=GetWindow<ProductionWelcomeValidation>();w.index=w.checks=0;w.testing=true;File.WriteAllText(Result,"RUNNING\n");w.Mount();}
            if(File.Exists("Library/Auth01Preview.request")){File.Delete("Library/Auth01Preview.request");Open();}
        };}
        [MenuItem("Domino/Production Auth/Welcome preview (isolated)")]
        public static void Open(){var w=GetWindow<ProductionWelcomeValidation>();w.testing=false;w.index=1;w.Mount();w.Focus();}
        void Mount(){
            titleContent=new GUIContent("PRODUCTION WELCOME");minSize=new Vector2(440,700);Show();rootVisualElement.Clear();
            var select=new PopupField<string>(Sizes.Select(x=>x.x+"x"+x.y).ToList(),index);select.RegisterValueChangedCallback(_=>{index=select.index;testing=false;Mount();});rootVisualElement.Add(select);
            frame=new VisualElement();frame.style.position=Position.Absolute;frame.style.top=28;frame.style.width=Sizes[index].x;frame.style.height=Sizes[index].y;frame.style.paddingTop=frame.style.paddingBottom=24;
            float scale=Mathf.Min(1,Mathf.Min(position.width/Sizes[index].x,(position.height-28)/Sizes[index].y));frame.style.transformOrigin=new TransformOrigin(0,0,0);frame.style.scale=new Scale(new Vector3(scale,scale,1));rootVisualElement.Add(frame);
            guestCalls=0;welcome=new ProductionWelcomeView(()=>guestCalls++);frame.Add(welcome);if(testing)rootVisualElement.schedule.Execute(CheckGeometry).ExecuteLater(400);
        }
        void Need(bool b,string key){if(!b)throw new Exception(key);checks++;}
        static float X(VisualElement e,VisualElement root){float x=0;while(e!=root){x+=e.layout.x;e=e.parent;}return x;}
        static void Click(VisualElement e){using(var ev=NavigationSubmitEvent.GetPooled()){ev.target=e;e.SendEvent(ev);}}
        void CheckGeometry(){try{
            var body=welcome.Body;Need(Mathf.Abs(X(body,frame)+body.layout.width/2-Sizes[index].x/2f)<1&&body.layout.width<=620.1f,"CENTERING");
            Need(welcome.contentViewport.layout.width==welcome.layout.width,"SCROLL_GUTTER");
            float end=Mathf.Max(0,welcome.contentContainer.layout.height-welcome.contentViewport.layout.height);
            foreach(var child in body.Children())Need(child.layout.x>=0&&child.layout.xMax<=body.layout.width+.1f&&body.layout.y+child.layout.yMax<=end+welcome.contentViewport.layout.height+1,"REACHABILITY");
            foreach(var label in body.Query<Label>().ToList()){Need(X(label,body)>=0&&X(label,body)+label.layout.width<=body.layout.width+.1f,"LABEL_BOUNDS");Need(label.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0,"UTF8");}
            foreach(var provider in new[]{"Google","Facebook","Email","Phone","Guest"}){
                var row=welcome.Q<ThemeButton>("Auth"+provider);Need(row.layout.height==56&&row.layout.width>=44,"TOUCH");
                Need(row.Q<Image>("AuthProviderIcon").vectorImage!=null,"ICON");Need(!string.IsNullOrEmpty(row.Q<Label>("AuthSecondaryLabel").text),"SECONDARY");
                Need(row.Q<Label>("AuthPrimaryLabel").text==(provider=="Guest"?"Continue as Guest":"Continue with "+provider),"PRIMARY");
                if(provider!="Guest"){Click(row);Need(guestCalls==0&&welcome.Q<Label>("AuthStatus").text=="Coming Soon","PROVIDER_PLACEHOLDER");}
            }
            var footer=welcome.Q("WelcomeSignInRow");var signIn=welcome.Q<Button>("SignIn");var prompt=footer.Q<Label>();
            Need(signIn.layout.width>=44&&signIn.layout.height>=44,"SIGN_IN_TOUCH_TARGET");
            Need(signIn.resolvedStyle.backgroundColor.a==0&&signIn.resolvedStyle.borderLeftWidth==0,"SIGN_IN_NO_SURFACE");
            Need(Mathf.Abs((X(prompt,body)+X(signIn,body)+signIn.layout.width)/2-body.layout.width/2)<1,"FOOTER_CENTERING");
            Need(X(prompt,body)>=0&&X(signIn,body)+signIn.layout.width<=body.layout.width+.1f,"FOOTER_OVERFLOW");
            Need(X(signIn,footer)-X(prompt,footer)-prompt.layout.width<=8,"FOOTER_INLINE_GAP");
            float inkWidth=signIn.MeasureTextSize(signIn.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x;
            float promptWidth=prompt.MeasureTextSize(prompt.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x;
            float textInset=signIn.resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter?Mathf.Max(0,(signIn.contentRect.width-inkWidth)/2):0;
            float visualGap=X(signIn,footer)+signIn.contentRect.x+textInset-X(prompt,footer)-prompt.contentRect.x-promptWidth;
            Need(Mathf.Abs(signIn.resolvedStyle.marginLeft-6)<.1f,"FOOTER_MARGIN_6");
            Need(visualGap>=5.5f&&visualGap<=9,"FOOTER_VISUAL_TEXT_GAP");
            Need(signIn.resolvedStyle.color==ThemeProvider.Current.Colors.Primary&&signIn.style.unityFont.value==ThemeProvider.Current.Typography.FontFor(TextRole.ButtonPrimary),"SIGN_IN_PRIMARY_BOLD");
            File.AppendAllText(Result,Sizes[index]+" FOOTER_VISUAL_GAP="+visualGap.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" HIT="+signIn.layout.width+"x"+signIn.layout.height+"\n");
            using(var ev=PointerEnterEvent.GetPooled()){ev.target=signIn;signIn.SendEvent(ev);}
            Need(signIn.resolvedStyle.backgroundColor.a==0,"SIGN_IN_HOVER_TRANSPARENT");
            Click(signIn);Need(guestCalls==0&&welcome.Q<Label>("AuthStatus").text=="Coming Soon","SIGN_IN_PLACEHOLDER");
            welcome.SetState(true,"Connecting…");Click(welcome.Q("AuthGuest"));Need(guestCalls==0&&!welcome.Q("AuthGuest").enabledSelf,"LOADING_DISABLED");
            welcome.SetState(false,"Try again");Click(welcome.Q("AuthGuest"));Need(guestCalls==1,"GUEST_ACTION");
            welcome.scrollOffset=new Vector2(0,end);Need(Mathf.Abs(welcome.scrollOffset.y-end)<1,"SCROLL");
            File.AppendAllText(Result,Sizes[index]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS PROVIDERS=PASS\n");
            if(++index<Sizes.Length){Mount();return;}File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\n");testing=false;index=1;Mount();
        }catch(Exception e){testing=false;File.AppendAllText(Result,"FAIL="+e.Message+"\n");Debug.LogException(e);}}
    }
}
