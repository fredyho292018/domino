using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed class PlayerHomeBindingValidation : EditorWindow
    {
        const string Folder="Library/PlayerUi01C";
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        RoutingCompositionFixture fixture;
        PlayerHomeCatalogFixture catalogs;
        PlayerPresentationSource presentation;
        ProductionAppShell shell;
        VisualElement frame;
        int checks;
        bool running;
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.update+=()=>{
                if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
                if(Consume("preflight")) {
                    var counts=new object[]{0,0,0};
                    typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType").Invoke(null,counts);
                    Write("Preflight.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nPLAY="+(EditorApplication.isPlaying?"ON":"OFF")+"\nCOMPILING=NO\nIMPORTING=NO\nCONSOLE_ERRORS="+counts[0]+"\nCONSOLE_WARNINGS="+counts[1]+"\n");
                }
                if(!EditorApplication.isPlayingOrWillChangePlaymode && Consume("request"))GetWindow<PlayerHomeBindingValidation>().Run();
                if(EditorApplication.isPlaying && File.Exists(Folder+"/inspect-live"))InspectLive();
            };
        }
        static bool Consume(string name){try{var path=Folder+"/"+name;if(!File.Exists(path))return false;File.Delete(path);return true;}catch(IOException){return false;}}
        static void Write(string name,string value){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name,value);}
        void Need(bool ok,string key){if(!ok)throw new InvalidOperationException(key);checks++;}
        void Clean(){rootVisualElement.Clear();presentation?.Dispose();presentation=null;catalogs?.Dispose();catalogs=null;fixture?.Dispose();fixture=null;}
        void OnDisable(){if(!running)Clean();}
        async Task Prepare(string key="SOFIA",string name="HomePlayer",string locale="en",bool restore=true)
        {
            Clean();fixture=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(fixture,key,name,locale);
            catalogs=new PlayerHomeCatalogFixture(fixture);
            presentation=new PlayerPresentationSource(fixture.Player,fixture.Composition.Router,()=>catalogs.Controller);
            presentation.SetLocale(locale);
            if(restore){await fixture.Forms.RestoreAsync();await presentation.CoachResolutionTask;}
        }
        async Task Mount(Vector2Int size)
        {
            rootVisualElement.Clear();rootVisualElement.Add(new Label("HOME · ISOLATED · fictional identity · no network"));
            frame=new VisualElement();frame.style.width=size.x;frame.style.height=size.y;frame.style.flexShrink=0;rootVisualElement.Add(frame);
            shell=new ProductionAppShell(menuDataSource:new PlayerMenuDataSource(presentation),homeDataSource:new PlayerHomeDataSource(presentation));
            frame.Add(shell);shell.SetSafeArea(0,24,0,24);await Task.Delay(180);await presentation.CoachResolutionTask;await Task.Delay(60);
        }
        ProductionHomePage Home=>(ProductionHomePage)shell.Pages[(int)ShellTab.Home];
        void NoDemo(){Need(!Home.Query<Label>().ToList().Any(x=>x.text.Contains("Alex")||x.text.Contains("Hola, soy Amara")),"NO_DEMO_IDENTITY");}
        static void Click(VisualElement e){using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=e;e.SendEvent(evt);}}
        async Task Geometry(Vector2Int size,string locale)
        {
            Need(Mathf.Abs(shell.layout.width-size.x)<.1f && Mathf.Abs(shell.layout.height-size.y)<.1f,"LOGICAL_VIEWPORT");
            Need(Home.Body.resolvedStyle.paddingLeft==24 && Home.Body.resolvedStyle.paddingRight==24,"UNCHANGED_MARGINS");
            Need(Home.Body.layout.width<=620.1f && Mathf.Abs(Home.Body.worldBound.center.x-shell.worldBound.center.x)<1,"CENTERED_COLUMN");
            Need(Home.Q<Label>("HomeGreeting").text==(locale=="es"?"Hola, ":"Hello, ")+"WWWWWWWWWWWWWWWW.","LONG_LOCALIZED_NAME");
            foreach(var label in Home.Query<Label>().ToList().Where(x=>x.resolvedStyle.display!=DisplayStyle.None)) {
                Need(label.worldBound.xMin>=shell.worldBound.xMin-.1f && label.worldBound.xMax<=shell.worldBound.xMax+.1f,"TEXT_HORIZONTAL_BOUNDS");
                if(label.text=="Coming Soon")continue;
                var measured=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                Need(measured.y<=label.contentRect.height+1,"TEXT_NO_CLIPPING");
            }
            var greeting=Home.Q<Label>("HomeGreeting");var subtitle=Home.Q<Label>("HomeSubtitle");var play=Home.Q("HomePlayCard");var coach=Home.Q("HomeCoachCard");
            Need(greeting.worldBound.yMax<=subtitle.worldBound.yMin+.1f && subtitle.worldBound.yMax<=play.worldBound.yMin+.1f && play.worldBound.yMax<=coach.worldBound.yMin+.1f,"CARD_AND_TEXT_NO_OVERLAP");
            var portrait=Home.Q("HomeCoachPortrait");Need(portrait.layout.width==120 && portrait.layout.height==120 && Home.Q<Image>("HomeCoachAvatar").image!=null,"PORTRAIT_GEOMETRY_ASSET");
            Need(Home.Q<Label>("HomeCoachName").text=="Sofia" && Home.Q<Label>("HomeCoachDescription").text==presentation.Current.Coach.Description,"CATALOG_COPY");
            Need(Home.Q("HomeFriendsCard").Query<Label>().ToList().Any(x=>x.text=="5 fictional players"),"FRIENDS_DEFERRED_UNCHANGED");
            foreach(var action in new[]{"HomePlay","HomeContinueLearning"}) {
                var button=Home.Q<Button>(action);Home.ScrollTo(button);await Task.Delay(60);
                Need(button.layout.height>=44 && button.enabledInHierarchy && button.worldBound.yMin>=Home.contentViewport.worldBound.yMin-1 && button.worldBound.yMax<=Home.contentViewport.worldBound.yMax+1,"ACTION_REACHABLE_"+action);
            }
            Home.ScrollTo(Home.Q("HomeFriendsCard"));await Task.Delay(40);
            Need(Home.Q("HomeFriendsCard").worldBound.yMax<=shell.BottomNavigation.worldBound.yMin+1,"BOTTOM_CONTENT_REACHABLE");
            var readCount=catalogs.Requests.Count;var bootstrap=fixture.BootstrapCalls;var get=fixture.StateCalls;
            shell.Select(ShellTab.Menu);await Task.Delay(40);
            var menu=(ProductionMenuPage)shell.Pages[(int)ShellTab.Menu];var expected=new PlayerMenuDataSource(presentation).Read();
            Need(menu.Q<Label>("MenuDisplayName").text==presentation.Current.DisplayName,"MENU_SAME_SOURCE");
            Need(menu.Q<Label>("MenuMembership").text==expected.MembershipLabel+" · "+MenuPlayerText.ProductBrand,"MENU_MEMBERSHIP_PRESERVED");
            shell.Select(ShellTab.Home);Click(Home.Q("HomeContinueLearning"));Need(shell.ActiveTab==ShellTab.Learn,"LEARNING_ROUTE_UNCHANGED");
            shell.Select(ShellTab.Home);Home.scrollOffset=Vector2.zero;
            Need(readCount==catalogs.Requests.Count && bootstrap==fixture.BootstrapCalls && get==fixture.StateCalls,"NAVIGATION_NO_NEW_FETCH");NoDemo();
        }
        async void Run()
        {
            if(running)return;running=true;checks=0;Write("Unity.validation.txt","START="+DateTime.UtcNow.ToString("O")+"\n");
            titleContent=new GUIContent("PLAYER HOME · ISOLATED");minSize=new Vector2(440,950);Show();
            var original=LocalizationSettings.SelectedLocale;
            try {
                foreach(var locale in new[]{"en","es"}) {
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(locale);
                    foreach(var size in Sizes) {
                        await Prepare(name:"WWWWWWWWWWWWWWWW",locale:locale);await Mount(size);await Geometry(size,locale);
                        Need(fixture.BootstrapCalls==1 && fixture.StateCalls==1 && fixture.Server.Applied==0 && fixture.Server.TrialApplied==0 && fixture.AuthWrites==0,"ISOLATED_READS_ONLY");
                        File.AppendAllText(Folder+"/Unity.validation.txt","PRESET="+size+" LOCALE="+locale+" PASS\n");
                    }
                }
                LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");
                foreach(var key in PlayerHomeCatalogFixture.Keys) {
                    await Prepare(key);await Mount(Sizes[1]);
                    Need(Home.Q<Label>("HomeCoachName").text==presentation.Current.Coach.Name,"TEN_COACH_NAMES");
                    Need(Home.Q<Image>("HomeCoachAvatar").image==Resources.Load<Texture2D>("AppShellMockCoaches/coach_"+key.ToLowerInvariant()),"TEN_EXACT_PORTRAITS");
                }
                await Prepare(restore:false);catalogs.Hold=new TaskCompletionSource<bool>();await Mount(Sizes[1]);NoDemo();
                Need(Home.Q("HomeGreeting")==null && Home.Q("HomeCoachCard")==null,"BEFORE_RESTORE_NEUTRAL");
                await fixture.Forms.RestoreAsync();await Task.Delay(60);Need(Home.Q("HomeCoachCard")==null && Home.Q<Label>("HomeCoachStatus").text=="Loading coach…","ASYNC_LOADING_NEUTRAL");
                catalogs.Hold.SetResult(true);await presentation.CoachResolutionTask;await Task.Delay(80);
                Need(Home.Q<Label>("HomeCoachName").text=="Sofia","ASYNC_UPDATE_ATTACHED_VIEW");
                fixture.Player.Dispose();Need(Home.Q("HomeGreeting")==null && Home.Q("HomeCoachCard")==null,"DISPOSAL_IMMEDIATE_CLEAR");
                await Prepare("OMAR","PlayerB");await Mount(Sizes[1]);Need(Home.Q<Label>("HomeGreeting").text=="Hello, PlayerB." && Home.Q<Label>("HomeCoachName").text=="Omar","REPLACEMENT_SESSION");
                var detached=shell;frame.Remove(detached);presentation.Dispose();frame.Add(detached);Need(Home.Q("HomeCoachCard")==null && Home.Q("HomeGreeting")==null,"REATTACH_RECHECKS_AUTHORITY");
                foreach(var mode in new[]{"MISSING","FAILURE","INACTIVE","AVATAR"}) {
                    await Prepare(key:mode=="MISSING"?null:"MATEO",restore:false);
                    catalogs.Fail=mode=="FAILURE";catalogs.InactiveKey=mode=="INACTIVE"?"MATEO":null;catalogs.UnknownAvatar=mode=="AVATAR";
                    await fixture.Forms.RestoreAsync();await presentation.CoachResolutionTask;await Mount(Sizes[1]);NoDemo();
                    if(mode=="MISSING"||mode=="FAILURE")Need(Home.Q("HomeCoachCard")==null && Home.Q<Label>("HomeCoachStatus").text=="Coach unavailable.","UNAVAILABLE_NO_SUBSTITUTION");
                    else Need(Home.Q<Label>("HomeCoachName").text=="Mateo","SAVED_COACH_RETAINED");
                    if(mode=="AVATAR")Need(Home.Q<Image>("HomeCoachAvatar").image==null && Home.Q<Image>("HomeCoachAvatar").vectorImage!=null,"UNKNOWN_ASSET_NEUTRAL");
                }
                await Prepare();await Mount(Sizes[1]);frame.Clear();
                var go=new GameObject("Isolated Home binding host"){hideFlags=HideFlags.HideAndDontSave};go.SetActive(false);
                try {
                    var host=go.AddComponent<ProductionAuthHost>();host.BindIsolated(frame,fixture.Forms,fixture.Composition,presentation);
                    shell=frame.Q<ProductionAppShell>();await Task.Delay(100);
                    Need(shell!=null && ReferenceEquals(host.PlayerPresentation,presentation),"ACTUAL_HOST_ONE_SOURCE");
                    Need(Home.Q<Label>("HomeGreeting").text=="Hello, HomePlayer." && Home.Q<Label>("HomeCoachName").text=="Sofia","ACTUAL_HOST_REAL_ADAPTER_INJECTION");
                } finally {DestroyImmediate(go);}
                File.AppendAllText(Folder+"/Unity.validation.txt","CHECKS="+checks+"_PASS\nFAIL=0\nRESPONSIVE=8/8_PASS_EN_ES\nAVATARS=10/10_PASS\nREAL_OPERATIONS=0\n");
                await Prepare();await Mount(Sizes[1]);
            }catch(Exception e){File.AppendAllText(Folder+"/Unity.validation.txt","FAIL=1\nFAILURE="+e.GetType().Name+":"+e.Message+"\n");}
            finally{LocalizationSettings.SelectedLocale=original;running=false;}
        }
        // Reads only the existing runtime host. Never restores, bootstraps, retries or reads credentials.
        static void InspectLive()
        {
            var route=ApplicationServices.AuthRouter?.Route;
            if(!route.HasValue || route==ProductionAuthRoute.Loading)return;
            // The temporary local SMOKE hook is optional and is not part of the checkpoint contract.
            if(typeof(ApplicationServices).GetField("ValidationSmokeTransportFactory",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null)!=null || ApplicationServices.ValidationFirebaseFactory!=null) {
                if(Consume("inspect-live"))Write("Live.validation.txt","PASS=NO\nSTOP=NONSTANDARD_RUNTIME_FACTORY\n");return;
            }
            if(Resources.Load<DominoApiSettings>("ApiSettings")?.Environment!="TEST" || route!=ProductionAuthRoute.AppShell) {
                if(Consume("inspect-live"))Write("Live.validation.txt","PASS=NO\nSTOP=TEST_APPSHELL_REQUIRED\n");return;
            }
            var hosts=UnityEngine.Object.FindObjectsByType<ProductionAuthHost>(FindObjectsSortMode.None);
            if(hosts.Length!=1 || hosts[0].PlayerPresentation?.Current.Availability!=PlayerPresentationAvailability.Ready)return;
            var host=hosts[0];var current=host.PlayerPresentation.Current;
            if(current.Coach?.Availability==PlayerCoachAvailability.Loading || !host.PlayerPresentation.CoachResolutionTask.IsCompleted)return;
            if(current.Coach?.Availability!=PlayerCoachAvailability.Ready) {
                if(Consume("inspect-live"))Write("Live.validation.txt","PASS=NO\nSTOP=COACH_UNAVAILABLE\nREASON="+current.Coach?.Reason+"\n");return;
            }
            var shell=host.GetComponent<UIDocument>().rootVisualElement.Q<ProductionAppShell>();if(shell==null || !Consume("inspect-live"))return;
            shell.Select(ShellTab.Home);var page=(ProductionHomePage)shell.Pages[(int)ShellTab.Home];page.scrollOffset=Vector2.zero;
            page.schedule.Execute(()=>{
                var state=host.PlayerPresentation.Current;var coach=state.Coach;
                var expected=new PlayerHomeDataSource(host.PlayerPresentation).Read();
                var name=page.Q<Label>("HomeGreeting");var coachName=page.Q<Label>("HomeCoachName");var avatar=page.Q<Image>("HomeCoachAvatar");
                var authoritative=ApplicationServices.Routing.Router.Onboarding;
                bool nameOk=!string.IsNullOrWhiteSpace(state.DisplayName) && name?.text==expected.Greeting;
                bool keyOk=coach.Key==PlayerCoachResolution.SavedKey(authoritative) && coach.OnboardingVersion==authoritative.catalogVersion && expected.Coach.CoachId==coach.Key;
                bool coachOk=coachName?.text==coach.Name && avatar?.image==expected.Coach.Avatar && avatar.image!=null;
                bool visible=name.worldBound.yMin>=page.contentViewport.worldBound.yMin-1 && avatar.worldBound.yMax<=page.contentViewport.worldBound.yMax+1;
                var menu=(ProductionMenuPage)shell.Pages[(int)ShellTab.Menu];var expectedMenu=new PlayerMenuDataSource(host.PlayerPresentation).Read();
                bool menuOk=menu.Q<Label>("MenuDisplayName").text==state.DisplayName && menu.Q<Label>("MenuMembership").text==expectedMenu.MembershipLabel+" · "+MenuPlayerText.ProductBrand;
                Write("Live.validation.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nENVIRONMENT=TEST\nSCREEN="+Screen.width+"x"+Screen.height+
                    "\nREAL_PLAYER_COACH_KEY="+coach.Key+"\nREAL_PINNED_ONBOARDING_VERSION="+coach.OnboardingVersion+"\nREAL_RESOLVED_COACH_CATALOG_VERSION="+coach.CatalogVersion+
                    "\nREAL_HOME_COACH_KEY="+expected.Coach.CoachId+"\nNAME_MATCH="+nameOk+"\nCOACH_KEY_MATCH="+keyOk+"\nCOACH_COPY_AVATAR_MATCH="+coachOk+"\nBOTH_VISIBLE="+visible+
                    "\nMENU_PRESERVED="+menuOk+"\nPASS="+(nameOk&&keyOk&&coachOk&&visible&&menuOk&&Screen.width==393&&Screen.height==852?"YES":"NO")+"\n");
            }).ExecuteLater(300);
        }
    }
}
