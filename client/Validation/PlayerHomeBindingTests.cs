using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI.AppShell;

static class PlayerHomeBindingTests
{
    static int names,coaches,versions,isolation;
    static void Check(bool ok,string key,ref int group){if(!ok)throw new Exception(key);group++;}
    static void N(bool ok,string key)=>Check(ok,key,ref names);
    static void C(bool ok,string key)=>Check(ok,key,ref coaches);
    static void V(bool ok,string key)=>Check(ok,key,ref versions);
    static void S(bool ok,string key)=>Check(ok,key,ref isolation);
    static async Task Main()
    {
        foreach(var locale in new[]{"en","es"})foreach(var name in new[]{"HomePlayer","WWWWWWWWWWWWWWWW",null,""}) {
            var snapshot=string.IsNullOrWhiteSpace(name)?null:new PlayerSnapshot("fixture",PlayerAccountType.Guest,name,"en",PlayerStatus.Active);
            var state=PlayerPresentationState.FromConfirmed(snapshot,locale,null);
            var greeting=HomePlayerText.Greeting(state,locale);
            N(greeting==(string.IsNullOrWhiteSpace(name)?(locale=="es"?"Perfil de jugador":"Player profile"):(locale=="es"?"Hola, ":"Hello, ")+name+"."),"localized_name_or_neutral");
            N(!greeting.Contains("Alex") && !greeting.Contains("fixture"),"no_demo_or_identifier");
        }
        foreach(var availability in new[]{PlayerPresentationAvailability.Empty,PlayerPresentationAvailability.Loading,PlayerPresentationAvailability.Unavailable}) {
            N(HomePlayerText.Greeting(PlayerPresentationState.Empty(availability),"en")=="Player profile","nonready_neutral");
        }
        foreach(var key in PlayerHomeCatalogFixture.Keys)foreach(var locale in new[]{"en","es"}) {
            using var f=new RoutingCompositionFixture(); PlayerHomeCatalogFixture.Select(f,key,locale:locale);
            using var catalogs=new PlayerHomeCatalogFixture(f);
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            source.SetLocale(locale);await f.Forms.RestoreAsync();await source.CoachResolutionTask;
            var state=source.Current;var coach=state.Coach;
            C(coach?.Availability==PlayerCoachAvailability.Ready && coach.Key==key,"exact_saved_key_"+key);
            C(coach.Name==key.Substring(0,1)+key.Substring(1).ToLowerInvariant() && coach.Locale==locale,"catalog_localized_copy");
            C(CoachAvatarResources.VersionedPath(coach.AvatarKey,coach.AvatarVersion,coach.AvatarPath)=="AppShellMockCoaches/coach_"+key.ToLowerInvariant(),"versioned_portrait");
            V(coach.OnboardingVersion==2 && coach.CatalogVersion==7,"explicit_reference_not_equal_version");
            V(catalogs.Requests.Count==2 && catalogs.Requests[0].Contains("onboarding/catalog") && catalogs.Requests[0].Contains("version=2") && catalogs.Requests[1].Contains("version=7"),"exact_wire_version_queries");
            N(state.DisplayName=="HomePlayer" && HomePlayerText.Greeting(state,locale).Contains("HomePlayer"),"shared_real_name");
            for(int i=0;i<8;i++){var ignored=source.Current;}
            source.SetLocale(locale); f.Player.ReceiveEntitlements(f.Player.Player.Uid,f.Server.Access);
            V(catalogs.Requests.Count==2,"read_refresh_idempotent_no_refetch");
            S(f.BootstrapCalls==1 && f.StateCalls==1 && f.Server.Applied==0 && f.Server.TrialApplied==0 && f.AuthWrites==0,"no_extra_player_reads_or_writes");
        }
        foreach(var scenario in new[]{"MISSING_KEY","INVALID_KEY","UNPINNED","MISSING_REFERENCE","WRONG_ONBOARDING","WRONG_COACH","MISSING_ENTRY","DUPLICATE","FAILURE"}) {
            using var f=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(f);
            using var catalogs=new PlayerHomeCatalogFixture(f);
            if(scenario=="MISSING_KEY")f.Server.State.answers=Array.Empty<OnboardingAnswerDto>();
            if(scenario=="INVALID_KEY")f.Server.State.answers[0].optionKey="INVALID";
            if(scenario=="UNPINNED")f.State("COMPLETED");
            catalogs.MissingReference=scenario=="MISSING_REFERENCE";catalogs.WrongOnboardingVersion=scenario=="WRONG_ONBOARDING";
            catalogs.WrongCoachVersion=scenario=="WRONG_COACH";catalogs.MissingKey=scenario=="MISSING_ENTRY"?"SOFIA":null;
            catalogs.DuplicateKey=scenario=="DUPLICATE";catalogs.Fail=scenario=="FAILURE";
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            await f.Forms.RestoreAsync();await source.CoachResolutionTask;
            C(source.Current.Coach?.Availability==PlayerCoachAvailability.Unavailable,"neutral_"+scenario);
            C(source.Current.Coach.Name==null && source.Current.Coach.AvatarKey==null,"no_fake_identity_"+scenario);
            V(catalogs.Requests.All(r=>r.Contains("version=")),"never_current_pointer");
            if(scenario=="MISSING_KEY"||scenario=="UNPINNED")V(catalogs.Requests.Count==0,"missing_context_no_catalog_request");
            if(scenario=="MISSING_REFERENCE")V(catalogs.Requests.Count==1,"missing_reference_no_coach_query");
        }
        using(var f=new RoutingCompositionFixture())using(var catalogs=new PlayerHomeCatalogFixture(f)) {
            PlayerHomeCatalogFixture.Select(f,"MATEO");catalogs.InactiveKey="MATEO";
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            await f.Forms.RestoreAsync();await source.CoachResolutionTask;
            C(source.Current.Coach.Key=="MATEO" && !source.Current.Coach.Selectable && source.Current.Coach.Availability==PlayerCoachAvailability.Ready,"inactive_saved_coach_preserved");
            var progress=f.Server.State;progress.status="IN_PROGRESS";progress.currentStepKey="MEMBERSHIP_STEP";
            var resolved=await PlayerCoachResolution.ResolveAsync(progress,"es",catalogs.Controller,default);
            V(resolved.Availability==PlayerCoachAvailability.Ready && resolved.CatalogVersion==7,"in_progress_uses_same_reference");
            C(CoachAvatarResources.VersionedPath("COACH_MATEO",2,"AppShellMockCoaches/coach_mateo.png")==null,"unknown_asset_version_neutral");
            C(CoachAvatarResources.VersionedPath("COACH_MATEO",1,"AppShellMockCoaches/coach_amara.png")==null,"mismatched_avatar_path_neutral");
        }
        using(var a=new RoutingCompositionFixture())using(var ca=new PlayerHomeCatalogFixture(a)) {
            PlayerHomeCatalogFixture.Select(a,"MATEO","PlayerA");ca.Hold=new TaskCompletionSource<bool>();
            using var old=new PlayerPresentationSource(a.Player,a.Composition.Router,()=>ca.Controller);
            await a.Forms.RestoreAsync();var late=old.CoachResolutionTask;
            C(old.Current.Coach.Availability==PlayerCoachAvailability.Loading && old.Current.Coach.Name==null,"loading_has_no_demo_coach");
            a.Player.Dispose();S(old.Current.DisplayName==null && old.Current.Coach==null,"name_coach_clear_together");
            using var b=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(b,"OMAR","PlayerB");
            using var cb=new PlayerHomeCatalogFixture(b);using var next=new PlayerPresentationSource(b.Player,b.Composition.Router,()=>cb.Controller);
            await b.Forms.RestoreAsync();await next.CoachResolutionTask;ca.Hold.SetResult(true);await late;
            S(old.Current.Coach==null && next.Current.DisplayName=="PlayerB" && next.Current.Coach.Key=="OMAR","late_old_epoch_same_uid_cannot_apply_to_b");
            next.Dispose();S(next.Current.DisplayName==null && next.Current.Coach==null,"source_disposal_clears_both");
        }
        using(var f=new RoutingCompositionFixture())using(var catalogs=new PlayerHomeCatalogFixture(f)) {
            PlayerHomeCatalogFixture.Select(f);catalogs.Hold=new TaskCompletionSource<bool>();
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            await f.Forms.RestoreAsync();var old=source.CoachResolutionTask;
            source.SetLocale("es");var newer=source.CoachResolutionTask;
            catalogs.Hold.SetResult(true);await old;await newer;
            S(source.Current.Coach.Locale=="es","late_locale_cannot_replace_new_copy");
            f.Auth.SignOut();S(source.Current.Coach==null && source.Current.DisplayName==null,"invalidated_auth_no_old_projection");
        }
        foreach(var failure in new[]{"BOOTSTRAP","ONBOARDING","UPDATE"}) {
            using var f=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(f);f.ErrorAt=failure;
            using var catalogs=new PlayerHomeCatalogFixture(f);using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            await f.Forms.RestoreAsync();S(source.Current.DisplayName==null && source.Current.Coach==null && catalogs.Requests.Count==0,"routing_failure_no_fake_name_or_coach");
        }
        Console.WriteLine("HOME_NAME_CHECKS="+names+"_PASS\nHOME_COACH_CHECKS="+coaches+"_PASS\nVERSION_CHECKS="+versions+"_PASS\nSESSION_IO_CHECKS="+isolation+"_PASS\nTOTAL="+(names+coaches+versions+isolation)+"_PASS\nFAIL=0");
    }
}
