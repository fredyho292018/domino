// Editor-only diagnostic. No authentication calls or session writes.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Domino.Infrastructure;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    [InitializeOnLoad]
    public static class WelcomeRuntimeFooterProbe
    {
        const string Request="Library/WelcomeRuntimeFooter.request";
        const string Result="Library/WelcomeRuntimeFooter.result.txt";
        static readonly Vector2Int[] Sizes={new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,800)};
        static int index=-1; static double due; static bool matrix;
        static WelcomeRuntimeFooterProbe(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!EditorApplication.isPlaying)return;
            try
            {
                if(File.Exists(Request))
                {
                    string mode=File.ReadAllText(Request).Trim();File.Delete(Request);
                    File.WriteAllText(Result,"RUN="+DateTime.UtcNow.ToString("O")+" MODE="+mode+"\n");
                    matrix=mode=="matrix"; index=0;
                    if(matrix)Resize(Sizes[0]);
                    due=EditorApplication.timeSinceStartup+2;
                }
                if(index<0||EditorApplication.timeSinceStartup<due)return;
                Inspect(matrix);
                if(!matrix||++index==Sizes.Length){index=-1;Log("COMPLETE=YES");return;}
                Resize(Sizes[index]);due=EditorApplication.timeSinceStartup+2;
            }
            catch(Exception ex){index=-1;Log("FAIL="+ex.GetType().Name+":"+ex.Message);}
        }
        static void Log(string text){File.AppendAllText(Result,text+"\n");}
        static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static float Y(VisualElement e,VisualElement root){float y=0;while(e!=root){y+=e.layout.y;e=e.parent;}return y;}
        static float X(VisualElement e,VisualElement root){float x=0;while(e!=root){x+=e.layout.x;e=e.parent;}return x;}
        public static float TextCenter(TextElement e,VisualElement root)
        {
            float height=e.MeasureTextSize(e.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).y;
            var rect=e.contentRect;int anchor=(int)e.resolvedStyle.unityTextAlign;
            float offset=anchor<3?0:anchor<6?(rect.height-height)/2:rect.height-height;
            return Y(e,root)+rect.y+offset+height/2;
        }
        public static void Footer(ProductionWelcomeView welcome,Action<string> log,bool validate)
        {
            var row=welcome.Q("WelcomeSignInRow");var prompt=row.Q<Label>();var link=row.Q<Button>("SignIn");
            float ph=prompt.MeasureTextSize(prompt.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).y;
            float lh=link.MeasureTextSize(link.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).y;
            float pc=TextCenter(prompt,welcome),lc=TextCenter(link,welcome);
            log("FOOTER_ROW_HEIGHT="+row.layout.height+" ALIGN_ITEMS="+row.resolvedStyle.alignItems);
            log("PROMPT_ELEMENT_HEIGHT="+prompt.layout.height+" TEXT_LINE_HEIGHT="+ph+" TEXT_ALIGN="+prompt.resolvedStyle.unityTextAlign+" TEXT_CENTER_Y="+pc);
            log("SIGNIN_BUTTON_HEIGHT="+link.layout.height+" WIDTH="+link.layout.width+" MIN_HEIGHT="+link.resolvedStyle.minHeight+" ALIGN_ITEMS="+link.resolvedStyle.alignItems+" JUSTIFY_CONTENT="+link.resolvedStyle.justifyContent);
            log("SIGNIN_TEXT_HEIGHT="+lh+" TEXT_ALIGN="+link.resolvedStyle.unityTextAlign+" TEXT_CENTER_Y="+lc+" CENTER_DEVIATION="+Mathf.Abs(pc-lc));
            float center=(X(prompt,welcome)+X(link,welcome)+link.layout.width)/2;
            log("FOOTER_GROUP_CENTER_X="+center+" DEVIATION="+Mathf.Abs(center-welcome.layout.width/2));
            log("TEXT_METRIC=MEASURE_TEXT_SIZE_LINE_BOX_NOT_GLYPH_BASELINE");
            if(!validate)return;
            Need(Mathf.Abs(pc-lc)<=1,"FOOTER_TEXT_CENTER");
            Need(Mathf.Abs(center-welcome.layout.width/2)<=1,"FOOTER_GROUP_CENTER");
            Need(link.layout.width>=44&&link.layout.height>=44,"FOOTER_TOUCH");
            Need(link.resolvedStyle.backgroundColor.a==0&&link.resolvedStyle.borderLeftWidth==0&&link.resolvedStyle.borderRightWidth==0,"FOOTER_SURFACE");
            Need(X(prompt,welcome)>=0&&X(link,welcome)+link.layout.width<=welcome.layout.width,"FOOTER_OVERFLOW");
        }
        static void Inspect(bool validate)
        {
            Need(ApplicationServices.AuthRouter!=null&&ApplicationServices.AuthRouter.SessionKind.ToString()=="NoSession"&&ApplicationServices.AuthRouter.Route.ToString()=="Welcome","NO_SESSION_WELCOME_REQUIRED");
            var host=UnityEngine.Object.FindObjectsByType<ProductionAuthHost>(FindObjectsSortMode.None).Single();
            var root=host.GetComponent<UIDocument>().rootVisualElement;
            var welcome=root.Q<ProductionWelcomeView>();Need(welcome!=null,"RUNTIME_WELCOME_MISSING");
            Log("SCREEN="+Screen.width+"x"+Screen.height+" ROOT="+root.layout.size+" WELCOME="+welcome.layout.size+" SAFE_AREA="+Screen.safeArea);
            if(validate)Need(Screen.width==Sizes[index].x&&Screen.height==Sizes[index].y,"SCREEN_SIZE");
            Footer(welcome,Log,validate);
            float left=X(welcome.Body,root)+welcome.Body.resolvedStyle.paddingLeft;
            float right=X(welcome.Body,root)+welcome.Body.layout.width-welcome.Body.resolvedStyle.paddingRight;
            Log("CONTENT_LEFT="+left+" RIGHT="+right+" WIDTH="+(right-left));
            if(validate)Need(Mathf.Abs(left-24)<1&&Mathf.Abs(right-(Screen.width-24))<1,"CONTENT_WIDTH");
            foreach(var row in new[]{"Google","Facebook","Email","Phone","Guest"}.Select(p=>welcome.Q("Auth"+p)))
                if(validate)Need(Mathf.Abs(X(row,root)-left)<1&&Mathf.Abs(row.layout.width-(right-left))<1,"PROVIDER_COLUMN");
            foreach(var label in welcome.Body.Children().OfType<Label>().Take(4))
                if(validate)Need(label.resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft&&Mathf.Abs(X(label,root)-left)<1,"HEADER_ALIGNMENT");
            var or=welcome.Q("WelcomeSeparator").Q<Label>();
            if(validate)Need(Mathf.Abs(X(or,root)+or.layout.width/2-root.layout.width/2)<1,"OR_ALIGNMENT");
            Need(welcome.Q("AuthStatus").resolvedStyle.display==DisplayStyle.None,"STALE_COMING_SOON");
            if(validate)Log("RUNTIME_FOOTER_"+Screen.width+"=PASS");
        }
        internal static void Resize(Vector2Int size)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes",true);
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
            var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{sizesType.GetProperty("currentGroupType",flags).GetValue(sizes)});
            int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),selected=-1;
            for(int i=0;i<count;i++)
            {
                var existing=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
                if((int)existing.GetType().GetProperty("width",flags).GetValue(existing)==size.x&&(int)existing.GetType().GetProperty("height",flags).GetValue(existing)==size.y){selected=i;break;}
            }
            if(selected<0)
            {
                var kind=assembly.GetType("UnityEditor.GameViewSizeType",true);var type=assembly.GetType("UnityEditor.GameViewSize",true);
                var value=Activator.CreateInstance(type,new[]{Enum.Parse(kind,"FixedResolution"),(object)size.x,size.y,"Welcome runtime validation"});
                group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{value});selected=count;
            }
            var viewType=assembly.GetType("UnityEditor.GameView",true);var window=EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex",flags).SetValue(window,selected);window.Show();window.Focus();window.Repaint();
        }
    }
}
