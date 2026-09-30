using System;
using Domino.AppShellMock;
using Domino.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed class MockShellPreview : EditorWindow
    {
        static MockShellState session = new MockShellState();
        public static readonly Vector2Int[] PreviewSizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        public static readonly string[] PreviewNames={"Small phone · 375 × 667","Tall phone · 393 × 852","Android · 412 × 915","Large phone · 430 × 932","Android large · 480 × 1040","Small tablet · 600 × 960","Tablet · 768 × 1024","Large tablet · 834 × 1194"};
        static readonly Vector2[] Insets={new Vector2(20,0),new Vector2(59,34),new Vector2(24,24),new Vector2(59,34),new Vector2(24,24),new Vector2(24,24),new Vector2(24,20),new Vector2(24,20)};
        VisualElement frame,host,scaledSlot; MockShellView view; DropdownField devices;Label dimensions;
        public float PreviewScale {get;private set;}=1;
        int device;
        [MenuItem("Domino/App Shell Mock/Open preview")]
        public static void Open()
        {
            session = new MockShellState();
            session.Start(MockEntry.NO_SESSION);
            var w=GetWindow<MockShellPreview>();
            w.titleContent=new GUIContent("App Shell · MOCK");
            w.minSize=new Vector2(440,720);
            w.device=0;
            w.CreateGUI();
            w.Show();
            w.Focus();
        }
        public void CreateGUI()
        {
            var root=rootVisualElement;root.Clear();root.name="AppShellMockPreview";root.style.backgroundColor=MockShellTheme.Background;
            var bar=new VisualElement();bar.style.flexDirection=FlexDirection.Row;bar.style.flexWrap=Wrap.Wrap;bar.style.paddingTop=8;bar.style.paddingBottom=8;root.Add(bar);
            var entries=new EnumField(session.OnboardingCompleted?MockEntry.EXISTING_REGISTERED:MockEntry.NO_SESSION);entries.style.width=230;entries.RegisterValueChangedCallback(e=>{session.Start((MockEntry)e.newValue);Refresh();});bar.Add(entries);
            devices=new DropdownField(new System.Collections.Generic.List<string>(PreviewNames),device);devices.style.width=260;devices.RegisterValueChangedCallback(e=>SetPreviewDevice(devices.index));bar.Add(devices);
            var reset=new Button(()=>{session.Start(MockEntry.NO_SESSION);Refresh();}){text="Reset demo"};bar.Add(reset);
            var route=new EnumField("Dev route",session.Page);route.style.width=310;route.RegisterValueChangedCallback(e=>{var p=(MockPage)e.newValue;if((int)p>=(int)MockPage.Home)session.Start(MockEntry.EXISTING_REGISTERED);session.Go(p);Refresh();});bar.Add(route);
            var profileMode=new EnumField("Profile demo",session.ProfileMode);profileMode.style.width=310;profileMode.RegisterValueChangedCallback(e=>{session.SetProfileMode((MockProfileMode)e.newValue);if(session.Page!=MockPage.Profile)session.Go(MockPage.Profile);Refresh();});bar.Add(profileMode);
            var note=new Label("DEVELOPMENT PREVIEW · fictional data · no network · controls above are not app UI");note.style.color=MockShellTheme.SecondaryText;note.style.fontSize=11;note.style.whiteSpace=WhiteSpace.Normal;root.Add(note);
            dimensions=new Label();dimensions.style.color=MockShellTheme.SecondaryText;dimensions.style.fontSize=11;root.Add(dimensions);
            host=new VisualElement();host.style.flexGrow=1;host.style.minHeight=0;host.style.overflow=Overflow.Hidden;host.style.alignItems=Align.Center;host.style.justifyContent=Justify.Center;host.RegisterCallback<GeometryChangedEvent>(e=>FitPreview());root.Add(host);Refresh();
        }
        [MenuItem("Domino/App Shell Mock/Open Home toolbar preview")]
        public static void OpenHome()
        {
            Open();session.Start(MockEntry.EXISTING_REGISTERED);
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [MenuItem("Domino/App Shell Mock/Open Learn preview")]
        public static void OpenLearn()
        {
            Open();session.Start(MockEntry.EXISTING_REGISTERED);session.Tab("Learn");
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [MenuItem("Domino/App Shell Mock/Open Coach selection preview")]
        public static void OpenCoaches()
        {
            Open();session.Start(MockEntry.NEW_GUEST);session.ExperienceLevel=1;session.ContinueExperience();
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [MenuItem("Domino/App Shell Mock/Open Experience preview")]
        public static void OpenExperience()
        {
            Open();session.Start(MockEntry.NEW_GUEST);
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [InitializeOnLoadMethod]
        static void RegisterMembershipPreview()
        {
            EditorApplication.update+=()=>
            {
                const string request="Library/AppShellMembership.request";
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!System.IO.File.Exists(request))return;
                try{System.IO.File.Delete(request);}catch(System.IO.IOException){return;}
                OpenMembership();
                System.IO.File.WriteAllText("Library/AppShellMembership.result.txt","MEMBERSHIP_PREVIEW_OPENED=YES\nPLAN=DIAMOND\nPERIOD=YEARLY\n");
            };
        }
        [MenuItem("Domino/App Shell Mock/Open Membership preview")]
        public static void OpenMembership()
        {
            Open();session.Start(MockEntry.NEW_GUEST);session.Go(MockPage.Trial);session.Go(MockPage.Plans);
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [MenuItem("Domino/App Shell Mock/Open Menu preview")]
        public static void OpenMenu()
        {
            Open();session.Start(MockEntry.EXISTING_REGISTERED);session.Tab("Menu");
            var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        [MenuItem("Domino/App Shell Mock/Open Profile preview")]
        public static void OpenProfile()
        {
            OpenMenu();session.OpenProfile(MockProfileMode.OWN_PROFILE);var window=GetWindow<MockShellPreview>();window.CreateGUI();window.Focus();
        }
        void Refresh()
        {
            if(host==null)return;host.Clear();
            scaledSlot=new VisualElement();scaledSlot.style.flexShrink=0;host.Add(scaledSlot);
            frame=new VisualElement {name="MockPhone"};frame.style.position=Position.Absolute;frame.style.left=0;frame.style.top=0;frame.style.backgroundColor=MockShellTheme.Background;
            frame.style.transformOrigin=new TransformOrigin(Length.Percent(0),Length.Percent(0),0);scaledSlot.Add(frame);
            view=new MockShellView(session,true);view.style.width=Length.Percent(100);view.style.maxWidth=620;view.style.alignSelf=Align.Center;frame.Add(view);
            ApplyDimensions();
        }
        void ApplyDimensions()
        {
            if(frame==null)return;var size=PreviewSizes[device];frame.style.width=size.x;frame.style.height=size.y;
            frame.style.paddingTop=Insets[device].x;frame.style.paddingBottom=Insets[device].y;FitPreview();
        }
        void FitPreview()
        {
            if(frame==null||host==null)return;var size=PreviewSizes[device];
            float availableWidth=host.contentRect.width-16,availableHeight=host.contentRect.height-16;
            if(availableWidth<=0||availableHeight<=0)return;
            PreviewScale=Mathf.Min(1,Mathf.Min(availableWidth/size.x,availableHeight/size.y));
            frame.style.scale=new Scale(new Vector3(PreviewScale,PreviewScale,1));scaledSlot.style.width=size.x*PreviewScale;scaledSlot.style.height=size.y*PreviewScale;
            dimensions.text=PreviewNames[device]+" · "+Mathf.RoundToInt(PreviewScale*100)+"% · logical viewport unchanged";
        }
        public void SetPreviewDevice(int index)
        {
            if(index<0||index>=PreviewSizes.Length)throw new ArgumentOutOfRangeException(nameof(index));
            device=index;if(devices!=null)devices.SetValueWithoutNotify(PreviewNames[index]);ApplyDimensions();
        }
    }
}
