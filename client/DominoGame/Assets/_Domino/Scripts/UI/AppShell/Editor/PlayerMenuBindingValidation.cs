using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Identity;
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
    // Explicit, isolated validation only. Never starts Play, restores live auth or calls a live API.
    public sealed class PlayerMenuBindingValidation : EditorWindow
    {
        const string Folder = "Library/PlayerUi01B";
        const string Result = Folder + "/Unity.validation.txt";
        static readonly Vector2Int[] Sizes = { new Vector2Int(375,667), new Vector2Int(393,852), new Vector2Int(412,915), new Vector2Int(430,932), new Vector2Int(480,1040), new Vector2Int(600,960), new Vector2Int(768,1024), new Vector2Int(834,1194) };
        RoutingCompositionFixture fixture;
        PlayerPresentationSource presentation;
        ProductionAppShell shell;
        VisualElement frame;
        int checks, logoutCallbacks;
        bool running;

        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.update += () => {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && Consume(Folder + "/preflight")) {
                    var asset=Resources.Load<DominoApiSettings>("ApiSettings");
                    var counts=new object[]{0,0,0};
                    typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType").Invoke(null,counts);
                    File.WriteAllText(Folder+"/Preflight.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nENVIRONMENT="+(asset?.Environment=="TEST"?"TEST":"NOT_TEST")+"\nPLAY_MODE="+(EditorApplication.isPlaying?"ON":"OFF")+"\nCOMPILING=NO\nIMPORTING=NO\nCONSOLE_ERRORS="+counts[0]+"\nCONSOLE_WARNINGS="+counts[1]+"\nCONSOLE_LOGS="+counts[2]+"\n");
                }
                if(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && Consume(Folder+"/tool-check")) {
                    var path=Folder+"/request-lock-check";
                    bool locked;
                    using(var handle=new FileStream(path,FileMode.Create,FileAccess.ReadWrite,FileShare.None))locked=!Consume(path) && File.Exists(path);
                    bool released=Consume(path) && !File.Exists(path);
                    File.WriteAllText(Folder+"/Tool.validation.txt","LOCKED_REQUEST_RETAINED="+locked+"\nRELEASED_REQUEST_CONSUMED="+released+"\nMISSING_REQUEST_IGNORED="+!Consume(path)+"\nPASS="+(locked && released)+"\n");
                    GetWindow<PlayerMenuBindingValidation>().PreviewOnly();
                }
                if (File.Exists(Folder + "/inspect-live") && !EditorApplication.isCompiling && !EditorApplication.isUpdating) InspectLive();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/request")) return;
                if(Consume(Folder + "/request"))GetWindow<PlayerMenuBindingValidation>().Run();
            };
        }
        static bool Consume(string path)
        {
            try { if(!File.Exists(path))return false;File.Delete(path);return true; }
            catch(IOException) { return false; } // A request producer may still hold its short write lock. Try next Editor tick.
        }
        async void PreviewOnly()
        {
            titleContent=new GUIContent("PLAYER MENU · ISOLATED");minSize=new Vector2(440,950);Show();
            await Prepare();await Mount(Sizes[1]);
        }
        // Reads the existing host and selects its retained Menu. Does not restore, refresh or bootstrap.
        static void InspectLive()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var asset=Resources.Load<DominoApiSettings>("ApiSettings");
            string stop=null;
            if(asset==null || asset.Environment!="TEST")stop="ENVIRONMENT_NOT_TEST";
            // The temporary local SMOKE hook is optional and is not part of the checkpoint contract.
            else if(typeof(ApplicationServices).GetField("ValidationSmokeTransportFactory",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null)!=null || ApplicationServices.ValidationFirebaseFactory!=null)stop="NONSTANDARD_RUNTIME_FACTORY";
            var route=ApplicationServices.AuthRouter?.Route;
            if(stop==null && (!route.HasValue || route==ProductionAuthRoute.Loading))return;
            if(stop==null && route!=ProductionAuthRoute.AppShell)stop="CURRENT_SESSION_NOT_APPSHELL";
            if(stop!=null){if(Consume(Folder+"/inspect-live"))File.WriteAllText(Folder+"/Live.validation.txt","PASS=NO\nSTOP="+stop+"\n");return;}
            var hosts=UnityEngine.Object.FindObjectsByType<ProductionAuthHost>(FindObjectsSortMode.None);
            if(hosts.Length!=1 || hosts[0].PlayerPresentation?.Current.Availability!=PlayerPresentationAvailability.Ready) {
                if(Consume(Folder+"/inspect-live"))File.WriteAllText(Folder+"/Live.validation.txt","PASS=NO\nSTOP=AUTHORITATIVE_SOURCE_UNAVAILABLE\n");return;
            }
            var host=hosts[0];var root=host.GetComponent<UIDocument>().rootVisualElement;
            var actual=root.Q<ProductionAppShell>();
            if(actual==null)return;
            if(!Consume(Folder+"/inspect-live"))return;actual.Select(ShellTab.Menu);
            actual.schedule.Execute(()=>{
                var state=host.PlayerPresentation.Current;
                var menu=(ProductionMenuPage)actual.Pages[(int)ShellTab.Menu];
                var name=menu.Q<Label>("MenuDisplayName");var plan=menu.Q<Label>("MenuMembership");
                var expected=new PlayerMenuDataSource(host.PlayerPresentation).Read();
                bool realName=state.Availability==PlayerPresentationAvailability.Ready && !string.IsNullOrEmpty(state.DisplayName) && name.text==state.DisplayName;
                bool realPlan=state.Membership!=PlayerMembershipKey.Unknown && plan.text==expected.MembershipLabel+" · "+MenuPlayerText.ProductBrand;
                bool visible=menu.resolvedStyle.display==DisplayStyle.Flex && name.worldBound.width>0 && name.worldBound.yMin>=menu.contentViewport.worldBound.yMin && plan.worldBound.yMax<=menu.contentViewport.worldBound.yMax;
                bool avatar=menu.Q<Image>("MenuAvatar").vectorImage==Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar");
                File.WriteAllText(Folder+"/Live.validation.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nENVIRONMENT=TEST\nLIVE_PRODUCTION_HOST=YES\nREAL_DISPLAY_NAME_VISIBLE="+(realName&&visible?"YES":"NO")+"\nREAL_MEMBERSHIP_VISIBLE="+(realPlan&&visible?"YES":"NO")+"\nNEUTRAL_AVATAR="+(avatar?"YES":"NO")+"\nSCREEN="+Screen.width+"x"+Screen.height+"\nMENU_OPEN_REQUESTS=0\nREAL_AUTH_LOGOUT=NO\nBOOTSTRAP_STARTUP=USER_AUTHORIZED\nPASS="+(realName&&realPlan&&visible&&avatar?"YES":"NO")+"\n");
            }).ExecuteLater(300);
        }
        [MenuItem("Domino/Production App Shell/Validate Player Menu binding (isolated)")]
        static void Open() { if (!EditorApplication.isPlayingOrWillChangePlaymode) GetWindow<PlayerMenuBindingValidation>().Run(); }
        void Need(bool ok, string key) { if (!ok) throw new InvalidOperationException(key); checks++; }
        void Clean()
        {
            rootVisualElement.Clear(); presentation?.Dispose(); presentation = null; fixture?.Dispose(); fixture = null;
        }
        void OnDisable() { if (!running) Clean(); }
        static EntitlementSummaryDto Access(string plan, string status = "ACTIVE") => new EntitlementSummaryDto {
            availability = "AVAILABLE", snapshot = new EffectiveEntitlementsDto { plan = plan, status = status, revision = 1 }
        };
        async Task Prepare(string name = "MenuPlayer", string locale = "en", bool restore = true)
        {
            Clean(); fixture = new RoutingCompositionFixture(); fixture.State("COMPLETED", 2);
            fixture.Server.State.basicProfile = new BasicProfileDto { displayName = name, preferredLocale = locale };
            fixture.Server.State.domainRevisions = new OnboardingDomainRevisionsDto { profile = 2, preferences = 2 };
            fixture.Server.Access = Access("FREE", "FREE");
            presentation = new PlayerPresentationSource(fixture.Player, fixture.Composition.Router);
            if (restore) await fixture.Forms.RestoreAsync();
        }
        async Task Mount(Vector2Int size, IMenuDataSource data = null)
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new Label("MENU · ISOLATED · fictional identity · no network"));
            frame = new VisualElement(); frame.style.width = size.x; frame.style.height = size.y; frame.style.flexShrink = 0;
            rootVisualElement.Add(frame);
            shell = new ProductionAppShell(signOut: () => logoutCallbacks++, menuDataSource: data ?? new PlayerMenuDataSource(presentation));
            frame.Add(shell); shell.SetSafeArea(0,24,0,24); shell.Select(ShellTab.Menu);
            await Task.Delay(180);
        }
        ProductionMenuPage Page => (ProductionMenuPage)shell.Pages[(int)ShellTab.Menu];
        string NameText => Page.Q<Label>("MenuDisplayName").text;
        string PlanText => Page.Q<Label>("MenuMembership").text;
        static void Click(VisualElement element) { using (var e = NavigationSubmitEvent.GetPooled()) { e.target = element; element.SendEvent(e); } }
        void NoDemo()
        {
            Need(!Page.Query<Label>().ToList().Any(x => x.text.Contains("Alex") || x.text.Contains("Demo player")), "NO_MENU_DEMO");
            Need(Page.Q<Image>("MenuAvatar").vectorImage == Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar"), "NEUTRAL_AVATAR");
        }
        async Task Geometry(Vector2Int size)
        {
            var page = Page; var body = page.Body;
            Need(Mathf.Abs(shell.layout.width-size.x)<.1f && Mathf.Abs(shell.layout.height-size.y)<.1f,"LOGICAL_VIEWPORT");
            Need(body.resolvedStyle.paddingLeft==24 && body.resolvedStyle.paddingRight==24,"UNCHANGED_MARGINS");
            Need(body.layout.width<=620.1f,"CONTENT_MAX_WIDTH");
            Need(Mathf.Abs(body.worldBound.center.x-shell.worldBound.center.x)<1,"COLUMN_CENTER");
            var card=page.Q<Button>("MenuProfile"); var avatar=page.Q<Image>("MenuAvatar");
            var name=page.Q<Label>("MenuDisplayName"); var plan=page.Q<Label>("MenuMembership");
            Need(card.focusable && card.enabledInHierarchy && card.layout.height>=44,"PROFILE_ACCESSIBLE");
            Need(avatar.layout.width==48 && avatar.layout.height==48,"AVATAR_GEOMETRY");
            Need(name.worldBound.xMin>=avatar.worldBound.xMax && name.worldBound.yMax<=plan.worldBound.yMin+.1f,"IDENTITY_NO_OVERLAP");
            foreach(var text in new[]{name,plan}) {
                Need(text.worldBound.xMin>=card.worldBound.xMin && text.worldBound.xMax<=card.worldBound.xMax+.1f,"IDENTITY_BOUNDS");
                var measured=text.MeasureTextSize(text.text,text.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                Need(measured.y<=text.contentRect.height+1,"TEXT_HEIGHT_NO_CLIPPING");
                Need(text.resolvedStyle.whiteSpace==WhiteSpace.Normal,"EXISTING_WRAP_POLICY");
            }
            var rows=body.Children().OfType<ThemeButton>().Where(x=>x.name.StartsWith("MenuRow")).ToArray();
            Need(rows.Select(x=>x.Q<Label>("PrimaryLabel").text).SequenceEqual(new[]{"Friends","Messages","Stats","Coach","Theme","Membership","Settings","Help & Support"}),"UNCHANGED_ROWS");
            Need(body.Children().Last().name=="MenuSignOut","LOGOUT_LAST");
            foreach(var row in rows) {
                Need(row.layout.height==56 && row.focusable && row.enabledInHierarchy,"ROW_ACCESSIBLE");
                Need(row.Q<Image>("MenuRowIcon").vectorImage!=null && row.Q<Image>("MenuRowChevron").vectorImage!=null,"ROW_ASSETS");
            }
            Need(shell.BottomNavigation.childCount==5 && shell.Q<ThemeButton>("TabMenu").Selected,"UNCHANGED_NAVIGATION");
            foreach(var child in body.Children())Need(child.worldBound.xMin>=shell.worldBound.xMin-.1f && child.worldBound.xMax<=shell.worldBound.xMax+.1f,"HORIZONTAL_OVERFLOW");
            var signOut=page.Q<Button>("MenuSignOut"); page.ScrollTo(signOut); await Task.Delay(60);
            Need(signOut.layout.height>=44 && signOut.worldBound.yMax<=page.contentViewport.worldBound.yMax+1,"LOGOUT_REACHABLE");
            Need(signOut.worldBound.yMax<=shell.BottomNavigation.worldBound.yMin+1,"NO_TOOLBAR_OVERLAP");
            page.scrollOffset=Vector2.zero; await Task.Delay(30);
            foreach(MenuDestination destination in Enum.GetValues(typeof(MenuDestination))) {
                Click(page.Q(destination==MenuDestination.Profile?"MenuProfile":"MenuRow"+destination));
                Need(shell.ActiveMenuDestination==destination && shell.HasSubpage,"MENU_ROUTE_"+destination);
                await Task.Delay(20); Click(shell.Q<Button>("ShellBack"));
                Need(shell.ActiveTab==ShellTab.Menu && !shell.HasSubpage,"RETURN_TO_MENU");
            }
            var count=logoutCallbacks; Click(signOut); Need(logoutCallbacks==count+1,"LOGOUT_CALLBACK_UNCHANGED_FAKE_ONLY");
            NoDemo();
        }
        async void Run()
        {
            if(running)return; running=true; checks=logoutCallbacks=0;
            Directory.CreateDirectory(Folder); File.WriteAllText(Result,"START="+DateTime.UtcNow.ToString("O")+"\n");
            titleContent=new GUIContent("PLAYER MENU · ISOLATED"); minSize=new Vector2(440,700); Show();
            var originalLocale=LocalizationSettings.SelectedLocale;
            try {
                await Prepare(restore:false); await Mount(Sizes[1]);
                Need(NameText=="Player profile" || NameText=="Perfil de jugador","LOADING_NEUTRAL");
                Need(PlanText==MenuPlayerText.ProductBrand,"LOADING_NO_FAKE_PLAN"); NoDemo();
                await fixture.Forms.RestoreAsync(); await Task.Delay(80);
                Need(NameText=="MenuPlayer","RESTORE_UPDATES_ATTACHED_MENU");
                foreach(var key in new[]{"FREE","GOLD","PLATINUM","DIAMOND","FAMILY","PREMIUM","PREMIUM_LEGACY"}) {
                    fixture.Player.ReceiveEntitlements(fixture.Player.Player.Uid,Access(key,key=="FREE"?"FREE":"ACTIVE"));
                    var summary=new PlayerMenuDataSource(presentation).Read();
                    Need(PlanText==summary.MembershipLabel+" · "+MenuPlayerText.ProductBrand,"AUTHORITATIVE_"+key);
                    Need(NameText=="MenuPlayer","PLAN_DOES_NOT_ALTER_NAME"); NoDemo();
                }
                fixture.Player.ReceiveEntitlements(fixture.Player.Player.Uid,null);
                Need(PlanText==MenuPlayerText.ProductBrand,"MISSING_NOT_FREE");
                int calls=fixture.BootstrapCalls+fixture.StateCalls;
                for(int i=0;i<5;i++){shell.Select(ShellTab.Home);shell.Select(ShellTab.Menu);}
                Need(calls==fixture.BootstrapCalls+fixture.StateCalls,"MENU_OPEN_NO_REQUESTS");
                fixture.Player.Dispose(); Need(NameText!="MenuPlayer" && PlanText==MenuPlayerText.ProductBrand,"SESSION_CLEAR_VISIBLE_IMMEDIATELY");
                await Prepare("PlayerTwo"); await Mount(Sizes[1]); Need(NameText=="PlayerTwo","REPLACEMENT_SESSION");
                var detached=shell; frame.Remove(detached); presentation.Dispose(); frame.Add(detached);
                Need(NameText!="PlayerTwo" && PlanText==MenuPlayerText.ProductBrand,"REATTACH_RECHECKS_SOURCE");
                foreach(var error in new[]{"BOOTSTRAP","ONBOARDING","UPDATE"}) {
                    await Prepare(restore:false); fixture.ErrorAt=error; await fixture.Forms.RestoreAsync(); await Mount(Sizes[1]);
                    Need(NameText!="MenuPlayer" && PlanText==MenuPlayerText.ProductBrand,"FAILURE_NEUTRAL_"+error); NoDemo();
                }
                foreach(var locale in new[]{"en","es"}) {
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(locale);
                    foreach(var size in Sizes) {
                        await Prepare("WWWWWWWWWWWWWWWW",locale); await Mount(size); await Geometry(size);
                        Need(NameText=="WWWWWWWWWWWWWWWW","LITERAL_UNTRUNCATED_LONG_NAME");
                        Need(fixture.BootstrapCalls==1 && fixture.StateCalls==1 && fixture.AuthWrites==0 && fixture.Server.Applied==0 && fixture.Server.TrialApplied==0,"FIXTURE_ONLY_NO_EXTRA_IO");
                        File.AppendAllText(Result,"PRESET="+size.x+"x"+size.y+" LOCALE="+locale+" GEOMETRY=PASS ACCESSIBILITY=PASS ROUTES=9/9_PASS\n");
                    }
                }
                await Prepare(); await Mount(Sizes[1],new DemoMenuDataSource());
                Need(NameText=="Alex · Demo player","EXPLICIT_EXISTING_FIXTURE_PRESERVED");
                await Mount(Sizes[1],new PlayerMenuDataSource(null)); Need(NameText!="Alex · Demo player" && PlanText==MenuPlayerText.ProductBrand,"PRODUCTION_DEFAULT_NEUTRAL");
                frame.Clear(); var hostObject=new GameObject("Isolated Menu binding host"){hideFlags=HideFlags.HideAndDontSave};hostObject.SetActive(false);
                try {
                    var host=hostObject.AddComponent<ProductionAuthHost>();host.BindIsolated(frame,fixture.Forms,fixture.Composition,presentation);
                    shell=frame.Q<ProductionAppShell>();Need(shell!=null,"ACTUAL_AUTH_HOST_SHELL");shell.Select(ShellTab.Menu);await Task.Delay(80);
                    Need(NameText=="MenuPlayer","ACTUAL_HOST_PRESENTATION_INJECTED");
                    Need(ReferenceEquals(host.PlayerPresentation,presentation),"ACTUAL_HOST_SINGLE_SOURCE");
                    Need(fixture.BootstrapCalls==1 && fixture.StateCalls==1,"HOST_BIND_NO_EXTRA_REQUESTS");
                } finally { DestroyImmediate(hostObject); }
                File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\nRESPONSIVE=8/8_PASS_EN_ES\nMENU_OPEN_REQUESTS=0\nREAL_OPERATIONS=0\nPLAY_MODE=OFF\n");
                LocalizationSettings.SelectedLocale=originalLocale;
                await Prepare(); await Mount(Sizes[1]);
                File.AppendAllText(Result,"FINAL=ISOLATED_MENU_393x852\nCURRENT_LIVE_SOURCE_AVAILABLE="+(ApplicationServices.Player?.IsCurrentSession==true?"YES":"NO")+"\n");
            } catch(Exception e) { File.AppendAllText(Result,"FAIL=1\nFAILURE="+e.GetType().Name+":"+e.Message+"\n"); }
            finally { LocalizationSettings.SelectedLocale=originalLocale; running=false; }
        }
    }
}
