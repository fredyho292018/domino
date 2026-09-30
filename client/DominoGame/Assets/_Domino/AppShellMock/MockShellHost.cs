#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.AppShellMock
{
    // Opt-in only: attach to a UIDocument in a NEW, isolated development scene.
    // Never bootstraps in existing client scenes. Not present in release players.
    [RequireComponent(typeof(UIDocument))]
    public sealed class MockShellHost : MonoBehaviour
    {
        public MockEntry StartingState=MockEntry.NO_SESSION;
        readonly MockShellState state=new MockShellState();
        VisualElement root; Rect lastSafe; int lastWidth,lastHeight;
        void OnEnable(){state.Start(StartingState);root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.style.flexGrow=1;root.Add(new MockShellView(state,false));ApplySafeArea();}
        void Update(){if(lastSafe!=Screen.safeArea||lastWidth!=Screen.width||lastHeight!=Screen.height)ApplySafeArea();}
        void ApplySafeArea()
        {
            if(root==null||Screen.width==0||Screen.height==0)return;
            lastSafe=Screen.safeArea;lastWidth=Screen.width;lastHeight=Screen.height;
            root.style.paddingLeft=Length.Percent(lastSafe.x/Screen.width*100);
            root.style.paddingRight=Length.Percent((Screen.width-lastSafe.xMax)/Screen.width*100);
            root.style.paddingTop=Length.Percent((Screen.height-lastSafe.yMax)/Screen.height*100);
            root.style.paddingBottom=Length.Percent(lastSafe.y/Screen.height*100);
        }
    }
}
#endif
