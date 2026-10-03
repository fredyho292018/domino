using System;
using System.Linq;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI.AppShell;

static class PlayerProfileBindingTests
{
    static int identity,dates,country,safety;
    static void Check(bool ok,string key,ref int count){if(!ok)throw new Exception(key);count++;}
    static void I(bool ok,string k)=>Check(ok,k,ref identity);
    static void D(bool ok,string k)=>Check(ok,k,ref dates);
    static void C(bool ok,string k)=>Check(ok,k,ref country);
    static void S(bool ok,string k)=>Check(ok,k,ref safety);
    static PlayerPresentationState State(string code="CU",DateTimeOffset? created=null)=>PlayerPresentationState.FromConfirmed(
        new PlayerSnapshot("profile-fixture",PlayerAccountType.Registered,"ProfilePlayer","en",PlayerStatus.Active,created,code),"en",null);
    static async Task Main()
    {
        var codec=new UnityApiJsonCodec();
        var prefix="{\"player\":{\"uid\":\"profile-fixture\",\"accountType\":\"REGISTERED\",\"displayName\":\"ProfilePlayer\",\"language\":\"en\",\"status\":\"ACTIVE\"";
        foreach(var wire in new[]{"",",\"createdAt\":null",",\"createdAt\":12",",\"createdAt\":{}",",\"createdAt\":\"bad\""}) {
            var result=PlayerSnapshotMapper.Player(codec.ReadSuccess(prefix+wire+"},\"wallet\":{\"coins\":0}}").player);
            D(!result.CreatedAt.HasValue,"old_or_invalid_optional_date_neutral");
            I(result.DisplayName=="ProfilePlayer","optional_date_does_not_replace_identity");
        }
        foreach(var instant in new[]{"2022-01-21T23:59:59.123456789Z","2022-01-21T23:59:59.999999999Z","2022-01-22T01:59:59+02:00","2022-01-21T12:00:00Z"}) {
            var result=PlayerSnapshotMapper.Player(codec.ReadSuccess(prefix+",\"createdAt\":\""+instant+"\"},\"wallet\":{\"coins\":0}}").player);
            D(result.CreatedAt.HasValue && result.CreatedAt.Value.UtcDateTime.Date==new DateTime(2022,1,21),"wire_instant_utc_date");
            D(ProfilePlayerText.Joined(State(created:result.CreatedAt),"en")=="Joined January 21, 2022","english_joined");
            D(ProfilePlayerText.Joined(State(created:result.CreatedAt),"es")=="Se unió el 21 de enero de 2022","spanish_joined");
        }
        foreach(var invalid in new[]{"","2022-01-21","2022-01-21T12:00:00","2022-99-21T12:00:00Z"})D(!PlayerSnapshotMapper.CreatedAt(invalid).HasValue,"no_inferred_instant");
        foreach(var code in new[]{"CU","US","FR",null,"ZZ"})foreach(var locale in new[]{"en","es"}) {
            var text=ProfilePlayerText.Country(State(code),locale);
            C(!string.IsNullOrWhiteSpace(text),"country_or_neutral");
            C(ProfilePlayerText.CountryCode(State(code))==(code=="ZZ"?null:code),"only_approved_iso_code");
            if(code!="CU")C(text!="Cuba","no_default_cuba");
            if(code=="US")C(text==(locale=="es"?"Estados Unidos":"United States"),"localized_country_metadata");
            if(code=="FR")C(text==(locale=="es"?"Francia":"France"),"localized_france");
        }
        foreach(var availability in new[]{PlayerPresentationAvailability.Empty,PlayerPresentationAvailability.Loading,PlayerPresentationAvailability.Unavailable}) {
            var empty=PlayerPresentationState.Empty(availability);
            S(empty.CreatedAt==null && empty.CountryCode==null && empty.DisplayName==null,"empty_projection_no_identity");
            S(ProfilePlayerText.Joined(empty,"en")=="Joined date unavailable" && ProfilePlayerText.Country(empty,"es")=="País no disponible","neutral_fields");
        }
        using(var f=new RoutingCompositionFixture())using(var catalogs=new PlayerHomeCatalogFixture(f)) {
            PlayerHomeCatalogFixture.Select(f,name:"ProfilePlayer");f.CreatedAt="2021-12-03T00:00:00Z";f.Server.State.basicProfile.countryCode="CU";
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>catalogs.Controller);
            await f.Forms.RestoreAsync();await source.CoachResolutionTask;
            var state=source.Current;
            I(state.DisplayName==f.Player.Player.DisplayName && state.DisplayName=="ProfilePlayer","authoritative_owner");
            I(MenuPlayerText.Name(state,"en")==state.DisplayName && HomePlayerText.Greeting(state,"en")=="Hello, ProfilePlayer.","menu_home_same_name");
            D(state.CreatedAt==f.Player.Player.CreatedAt && ProfilePlayerText.Joined(state,"en")=="Joined December 3, 2021","bootstrap_not_onboarding_completion");
            C(state.CountryCode=="CU" && f.Player.Player.CountryCode=="CU","confirmed_profile_country");
            I(state.Coach.Key=="SOFIA","home_coach_unchanged");
            var changed=new BasicProfileDto{displayName="PlayerRenamed",countryCode="US",preferredLocale="es"};
            S(!f.Player.ReceiveConfirmedProfile(f.Player.Player,changed,new OnboardingDomainRevisionsDto{profile=2,preferences=2}),"same_revision_conflict_rejected");
            S(f.Player.ReceiveConfirmedProfile(f.Player.Player,changed,new OnboardingDomainRevisionsDto{profile=3,preferences=3}),"new_revision_accepted");
            C(source.Current.CountryCode=="US","country_projection_updates");D(source.Current.CreatedAt==state.CreatedAt,"enrichment_preserves_date");
            await f.Player.RefreshConfirmedAsync();
            C(f.Player.Player.CountryCode=="US","bootstrap_refresh_preserves_confirmed_country");
            S(f.BootstrapCalls==2 && f.StateCalls==1 && f.Server.Applied==0 && f.Server.TrialApplied==0 && f.AuthWrites==0,"no_profile_fetch_or_write");
            f.Player.Dispose();S(source.Current.DisplayName==null && source.Current.CountryCode==null && source.Current.CreatedAt==null,"dispose_clears_all_identity");
        }
        foreach(var fail in new[]{"BOOTSTRAP","ONBOARDING"}) {
            using var f=new RoutingCompositionFixture();PlayerHomeCatalogFixture.Select(f);f.CreatedAt="2020-01-01T00:00:00Z";f.ErrorAt=fail;
            using var source=new PlayerPresentationSource(f.Player,f.Composition.Router);
            await f.Forms.RestoreAsync();S(source.Current.CountryCode==null && source.Current.CreatedAt==null && source.Current.DisplayName==null,"failed_route_no_stale_profile");
        }
        using(var a=new RoutingCompositionFixture())using(var b=new RoutingCompositionFixture()) {
            PlayerHomeCatalogFixture.Select(a,name:"PlayerA");a.CreatedAt="2020-01-01T00:00:00Z";a.Server.State.basicProfile.countryCode="CU";
            PlayerHomeCatalogFixture.Select(b,name:"PlayerB");b.Server.State.basicProfile.countryCode="US";
            using var old=new PlayerPresentationSource(a.Player,a.Composition.Router);using var current=new PlayerPresentationSource(b.Player,b.Composition.Router);
            await a.Forms.RestoreAsync();a.Auth.SignOut();await b.Forms.RestoreAsync();
            S(old.Current.CreatedAt==null && old.Current.CountryCode==null && old.Current.DisplayName==null,"session_a_erased");
            S(current.Current.DisplayName=="PlayerB" && current.Current.CountryCode=="US" && current.Current.CreatedAt==null,"session_b_no_a_date_country");
        }
        Console.WriteLine("IDENTITY="+identity+"_PASS\nCREATED_AT="+dates+"_PASS\nCOUNTRY="+country+"_PASS\nSAFETY="+safety+"_PASS\nTOTAL="+(identity+dates+country+safety)+"_PASS\nFAIL=0");
    }
}
