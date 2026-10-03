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
    public sealed class PlayerProfileBindingValidation : EditorWindow
    {
        const string Folder="Library/PlayerUi01D";
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        RoutingCompositionFixture fixture;
        PlayerHomeCatalogFixture catalogs;
        PlayerPresentationSource source;
        ProductionAppShell shell;
        VisualElement frame;
        bool running;
        int checks;
        ProductionProfilePage Page=>shell.Q<ProductionProfilePage>();
        [InitializeOnLoadMethod] static void Register()
        {
            EditorApplication.update+=()=>{
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(Consume("preflight")) {
                    var counts=new object[]{0,0,0};
                    typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType").Invoke(null,counts);
                    Write("Preflight.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nPLAY="+(EditorApplication.isPlaying?"ON":"OFF")+"\nCOMPILING=NO\nIMPORTING=NO\nCONSOLE_ERRORS="+counts[0]+"\nCONSOLE_WARNINGS="+counts[1]+"\n");
                }
                if(!EditorApplication.isPlayingOrWillChangePlaymode && Consume("request"))GetWindow<PlayerProfileBindingValidation>().Run();
                if(EditorApplication.isPlaying && File.Exists(Folder+"/inspect-live"))InspectLive();
            };
        }
        static bool Consume(string name){try{var p=Folder+"/"+name;if(!File.Exists(p))return false;File.Delete(p);return true;}catch(IOException){return false;}}
        static void Write(string name,string content){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/"+name,content);}
        void Need(bool value,string key){if(!value)throw new InvalidOperationException(key);checks++;}
        void Clean(){rootVisualElement.Clear();source?.Dispose();source=null;catalogs?.Dispose();catalogs=null;fixture?.Dispose();fixture=null;}
        void OnDisable(){if(!running)Clean();}
        async Task Prepare(string name="ProfilePlayer",string country="CU",string created="2021-12-03T23:59:59.999999999Z",string locale="en",string error=null,bool restore=true)
        {
            Clean();fixture=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(fixture,name:name,locale:locale);
            fixture.CreatedAt=created;fixture.Server.State.basicProfile.countryCode=country;fixture.ErrorAt=error;
            catalogs=new PlayerHomeCatalogFixture(fixture);
            source=new PlayerPresentationSource(fixture.Player,fixture.Composition.Router,()=>catalogs.Controller);source.SetLocale(locale);
            if(restore){await fixture.Forms.RestoreAsync();await source.CoachResolutionTask;}
        }
        async Task Mount(Vector2Int size,IProfileDataSource provider=null)
        {
            rootVisualElement.Clear();rootVisualElement.Add(new Label("PROFILE · ISOLATED · fictional identity · no network"));
            frame=new VisualElement();frame.style.width=size.x;frame.style.height=size.y;frame.style.flexShrink=0;rootVisualElement.Add(frame);
            shell=new ProductionAppShell(provider??new PlayerProfileDataSource(source),menuDataSource:new PlayerMenuDataSource(source),homeDataSource:new PlayerHomeDataSource(source));
            frame.Add(shell);shell.SetSafeArea(0,24,0,24);OpenProfile(shell);await Task.Delay(180);
        }
        static void OpenProfile(ProductionAppShell target){target.Select(ShellTab.Menu);target.OpenMenuDestination(MenuDestination.Profile);}
        static void Click(VisualElement element){using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=element;element.SendEvent(evt);}}
        void Identity()
        {
            var expected=new PlayerProfileDataSource(source).ReadProfile();
            Need(Page.Q<Label>("ProfilePlayerName").text==source.Current.DisplayName,"NAME_FROM_PLAYER");
            Need(Page.Q<Label>("ProfilePlayerName").text==shell.Pages[(int)ShellTab.Menu].Q<Label>("MenuDisplayName").text,"NAME_MATCHES_MENU");
            Need(Page.Q<Label>("ProfileCountry").text==expected.CountryName,"COUNTRY_FROM_CONFIRMED_PROFILE");
            Need(Page.Q<Label>("ProfileJoined").text==expected.JoinedLabel,"JOINED_FROM_PLAYER");
            var avatar=Page.Q<Image>("ProfilePlayerAvatarImage");
            Need(avatar.image==null && avatar.vectorImage==Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar"),"NEUTRAL_AVATAR_NOT_COACH");
            Need(Page.Q("PlayerAvatar").layout.width==76 && Page.Q("PlayerAvatar").layout.height==76,"AVATAR_GEOMETRY_PRESERVED");
            Need(Page.Query<VisualElement>("ProfileGameRow").ToList().Count==0 && Page.Q("ViewAllGames")==null && Page.Q("ProfileHistoryUnavailable")!=null,"NO_FICTIONAL_HISTORY");
            Need(!Page.Query<Label>().ToList().Any(l=>l.text.Contains("Alex")||l.text.Contains("Demo player")||l.text.Contains("January 21, 2022")||l.text.Contains("Mateo")),"NO_DEMO_IDENTITY_DATE_COACH");
        }
        async Task Geometry(Vector2Int size)
        {
            Need(Mathf.Abs(shell.layout.width-size.x)<.1f && Mathf.Abs(shell.layout.height-size.y)<.1f,"LOGICAL_VIEWPORT");
            Need(Page.Body.resolvedStyle.paddingLeft==24 && Page.Body.resolvedStyle.paddingRight==24,"UNCHANGED_MARGINS");
            Need(Page.Body.layout.width<=620.1f && Mathf.Abs(Page.Body.worldBound.center.x-shell.worldBound.center.x)<1,"CENTERED_COLUMN");
            foreach(var label in Page.Query<Label>().ToList().Where(l=>l.resolvedStyle.display!=DisplayStyle.None)) {
                Need(label.worldBound.xMin>=shell.worldBound.xMin-.2f && label.worldBound.xMax<=shell.worldBound.xMax+.2f,"HORIZONTAL_OVERFLOW_"+label.name);
                var measured=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                Need(measured.y<=label.contentRect.height+1,"TEXT_NO_CLIPPING_"+label.name);
            }
            var edit=Page.Q<Button>("ProfilePrimaryAction");Page.ScrollTo(edit);await Task.Delay(60);
            Need(edit.layout.height>=44 && edit.worldBound.yMax<=Page.contentViewport.worldBound.yMax+1,"EDIT_REACHABLE");
            Need(edit.worldBound.yMax<=shell.BottomNavigation.worldBound.yMin+1,"NO_TOOLBAR_OVERLAP");
            Need(Page.Q<Button>("ShareProfile").layout.height>=44,"SHARE_TOUCH_PRESERVED");
            Page.scrollOffset=Vector2.zero;
        }
        async void Run()
        {
            if(running)return;running=true;checks=0;Write("Unity.validation.txt","START="+DateTime.UtcNow.ToString("O")+"\n");
            titleContent=new GUIContent("PLAYER PROFILE · ISOLATED");minSize=new Vector2(440,950);Show();
            var original=LocalizationSettings.SelectedLocale;
            try {
                foreach(var locale in new[]{"en","es"}) {
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(locale);
                    foreach(var size in Sizes) {
                        await Prepare(name:"WWWWWWWWWWWWWWWW",locale:locale);await Mount(size);Identity();await Geometry(size);
                        Need(Page.Q<Label>("ProfileJoined").text==(locale=="es"?"Se unió el 3 de diciembre de 2021":"Joined December 3, 2021"),"EXACT_DATE_LOCALIZED_UNITY");
                        Need(Page.Q<Image>("ProfileFlag").vectorImage!=null && Page.Q<Label>("ProfileCountry").text=="Cuba","EXACT_COUNTRY_FLAG");
                        var reads=fixture.BootstrapCalls+fixture.StateCalls;
                        Click(Page.Q("ShareProfile"));Need(Page.Q<Label>("ProfileNotice").text=="Sharing coming soon.","SHARE_BEHAVIOR_UNCHANGED");
                        Click(Page.Q("ProfilePrimaryAction"));Need(shell.ActiveProfileSection==ProfileSection.EditProfile && Page.Q<Label>("SubpageMessage").text=="Coming Soon","EDIT_COMING_SOON");
                        shell.Back();await Task.Delay(40);Need(shell.ActiveProfileSection==ProfileSection.Profile,"BACK_PROFILE");
                        shell.Back();Need(shell.ActiveTab==ShellTab.Menu && !shell.HasSubpage,"BACK_MENU");
                        shell.Select(ShellTab.Home);await Task.Delay(40);
                        Need(shell.Pages[0].Q<Label>("HomeGreeting").text.Contains(source.Current.DisplayName),"HOME_NAME_PRESERVED");
                        Need(shell.Pages[0].Q<Label>("HomeCoachName").text=="Sofia" && shell.Pages[0].Q<Image>("HomeCoachAvatar").image!=null,"HOME_COACH_PRESERVED");
                        OpenProfile(shell);await Task.Delay(40);
                        Need(reads==fixture.BootstrapCalls+fixture.StateCalls && fixture.Server.Applied==0 && fixture.Server.TrialApplied==0 && fixture.AuthWrites==0,"NAVIGATION_NO_NEW_IO");
                        File.AppendAllText(Folder+"/Unity.validation.txt","PRESET="+size+" LOCALE="+locale+" PASS\n");
                    }
                    foreach(var code in new[]{"US","FR",null,"ZZ"}) {
                        await Prepare(country:code,created:null,locale:locale);await Mount(Sizes[1]);Identity();await Geometry(Sizes[1]);
                        Need(Page.Q<Image>("ProfileFlag").vectorImage==null && Page.Q<Label>("ProfileCountry").text!="Cuba","NO_WRONG_FLAG_OR_COUNTRY");
                        Need(Page.Q<Label>("ProfileJoined").text==(locale=="es"?"Fecha de ingreso no disponible":"Joined date unavailable"),"MISSING_DATE_NEUTRAL");
                        File.AppendAllText(Folder+"/Unity.validation.txt","COUNTRY_METADATA="+(code??"MISSING")+" LOCALE="+locale+" LABEL="+Page.Q<Label>("ProfileCountry").text+"\n");
                    }
                }
                LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");
                await Prepare(restore:false);await Mount(Sizes[1]);Need(Page.Profile.DisplayName=="Player profile" && Page.Profile.JoinedAt==null,"LOADING_NEUTRAL");
                await fixture.Forms.RestoreAsync();await source.CoachResolutionTask;await Task.Delay(100);Identity();
                fixture.Player.Dispose();Need(Page.Profile.DisplayName=="Player profile" && Page.Profile.CountryCode==null && Page.Profile.JoinedAt==null,"DISPOSE_IMMEDIATE_CLEAR");
                await Prepare(name:"PlayerB",country:"US",created:null);await Mount(Sizes[1]);
                Need(Page.Profile.DisplayName=="PlayerB" && Page.Profile.CountryCode=="US" && Page.Profile.JoinedAt==null,"SESSION_B_NO_A_IDENTITY");
                var detached=shell;frame.Remove(detached);source.Dispose();frame.Add(detached);
                Need(Page.Profile.DisplayName=="Player profile" && Page.Profile.JoinedAt==null,"REATTACH_NO_STALE_STATE");
                foreach(var error in new[]{"BOOTSTRAP","ONBOARDING"}) {
                    await Prepare(error:error);await Mount(Sizes[1]);Need(Page.Profile.DisplayName=="Player profile" && Page.Profile.JoinedAt==null && Page.Profile.CountryCode==null,"FAILURE_NEUTRAL_"+error);
                }
                await Prepare();await Mount(Sizes[1],new DemoProfileDataSource());
                Need(Page.Profile.DisplayName=="Alex · Demo player" && Page.Query<VisualElement>("ProfileGameRow").ToList().Count==5,"EXPLICIT_DEMO_FIXTURE_PRESERVED");
                await Mount(Sizes[1]);frame.Clear();
                var go=new GameObject("Isolated Profile binding host"){hideFlags=HideFlags.HideAndDontSave};go.SetActive(false);
                try {
                    var host=go.AddComponent<ProductionAuthHost>();host.BindIsolated(frame,fixture.Forms,fixture.Composition,source);
                    shell=frame.Q<ProductionAppShell>();Need(shell!=null && ReferenceEquals(host.PlayerPresentation,source),"ACTUAL_HOST_SINGLE_SOURCE");
                    OpenProfile(shell);await Task.Delay(100);Identity();Need(fixture.BootstrapCalls==1 && fixture.StateCalls==1,"HOST_NO_EXTRA_READ");
                } finally {DestroyImmediate(go);}
                File.AppendAllText(Folder+"/Unity.validation.txt","CHECKS="+checks+"_PASS\nFAIL=0\nRESPONSIVE=8/8_PASS_EN_ES\nREAL_OPERATIONS=0\n");
                await Prepare();await Mount(Sizes[1]);
            }catch(Exception e){File.AppendAllText(Folder+"/Unity.validation.txt","FAIL=1\nFAILURE="+e.GetType().Name+":"+e.Message+"\n");}
            finally{LocalizationSettings.SelectedLocale=original;running=false;}
        }
        // Observe the existing runtime only. No restore, retry, Player fetch, or credentials.
        static void InspectLive()
        {
            var route=ApplicationServices.AuthRouter?.Route;
            if(!route.HasValue || route==ProductionAuthRoute.Loading)return;
            if(typeof(ApplicationServices).GetField("ValidationSmokeTransportFactory",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.GetValue(null)!=null || ApplicationServices.ValidationFirebaseFactory!=null) {
                if(Consume("inspect-live"))Write("Live.validation.txt","PASS=NO\nSTOP=NONSTANDARD_RUNTIME_FACTORY\n");return;
            }
            if(Resources.Load<DominoApiSettings>("ApiSettings")?.Environment!="TEST" || route!=ProductionAuthRoute.AppShell) {
                if(Consume("inspect-live"))Write("Live.validation.txt","PASS=NO\nSTOP=TEST_APPSHELL_REQUIRED\n");return;
            }
            var hosts=UnityEngine.Object.FindObjectsByType<ProductionAuthHost>(FindObjectsSortMode.None);
            if(hosts.Length!=1 || hosts[0].PlayerPresentation?.Current.Availability!=PlayerPresentationAvailability.Ready)return;
            var host=hosts[0];var shell=host.GetComponent<UIDocument>().rootVisualElement.Q<ProductionAppShell>();
            if(shell==null || !Consume("inspect-live"))return;
            OpenProfile(shell);
            shell.schedule.Execute(()=>{
                var page=shell.Q<ProductionProfilePage>();var state=host.PlayerPresentation.Current;
                var model=ApplicationServices.Player.Player;var profile=ApplicationServices.Routing.Router.Onboarding.basicProfile;
                var expected=new PlayerProfileDataSource(host.PlayerPresentation).ReadProfile();
                bool name=page.Q<Label>("ProfilePlayerName").text==model.DisplayName && state.DisplayName==model.DisplayName && shell.Pages[(int)ShellTab.Menu].Q<Label>("MenuDisplayName").text==model.DisplayName;
                bool country=state.CountryCode==profile.countryCode && page.Q<Label>("ProfileCountry").text==expected.CountryName && !string.IsNullOrEmpty(state.CountryCode);
                var avatar=page.Q<Image>("ProfilePlayerAvatarImage");bool neutral=avatar.image==null && avatar.vectorImage==Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_avatar");
                bool history=page.Query<VisualElement>("ProfileGameRow").ToList().Count==0 && page.Q("ProfileHistoryUnavailable")!=null;
                bool date=state.CreatedAt==model.CreatedAt && page.Q<Label>("ProfileJoined").text==expected.JoinedLabel;
                bool visible=page.Q("ProfilePlayerCard").worldBound.yMax<=page.contentViewport.worldBound.yMax+1;
                Click(page.Q("ProfilePrimaryAction"));bool edit=shell.ActiveProfileSection==ProfileSection.EditProfile && shell.Q<Label>("SubpageMessage")?.text=="Coming Soon";shell.Back();
                Write("Live.validation.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nENVIRONMENT=TEST\nSCREEN="+Screen.width+"x"+Screen.height+"\nNAME_MATCH="+name+"\nCOUNTRY_MATCH="+country+"\nNEUTRAL_AVATAR="+neutral+"\nHISTORY_NEUTRAL="+history+"\nJOINED_CONTRACT_MATCH="+date+"\nJOINED="+(state.CreatedAt.HasValue?"PRESENT":"PENDING_BACKEND_DEPLOY")+"\nHEADER_VISIBLE="+visible+"\nEDIT_COMING_SOON="+edit+"\nPASS="+(name&&country&&neutral&&history&&date&&visible&&edit&&Screen.width==393&&Screen.height==852?"YES":"NO")+"\n");
            }).ExecuteLater(400);
        }
    }
}
