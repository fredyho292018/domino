using System;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.UI.AppShell;
static class PlayerAliasBindingTests
{
    static int checks;
    static void Need(bool value,string key){if(!value)throw new Exception(key);checks++;}
    static async Task Main()
    {
        using var f=new RoutingCompositionFixture();
        f.CreatedAt="2026-01-01T00:00:00Z";
        using var presentation=new PlayerPresentationSource(f.Player,f.Composition.Router,()=>f.Composition.Onboarding);
        await f.Forms.RestoreAsync();await f.Composition.LoadOnboardingAsync();
        var controller=f.Composition.Onboarding;
        await controller.StartAsync();
        controller.Profile.FirstName="PrivateFirst";controller.Profile.LastName="PrivateLast";
        controller.Profile.DisplayName="AcceptedAlias";controller.Profile.Country="CU";controller.Profile.Language="en";
        var created=f.Player.Player.CreatedAt;
        await controller.SaveProfileAsync();
        Need(controller.ProfileFeedback=="","save_confirmed");
        Need(f.Player.Player.DisplayName=="AcceptedAlias","immediate_player_refresh");
        Need(f.Player.Player.CreatedAt==created,"created_at_preserved");
        Need(f.BootstrapCalls==1,"no_rebootstrap");
        Need(controller.State.basicProfile.firstName=="PrivateFirst","private_first_persisted");
        Need(controller.State.basicProfile.lastName=="PrivateLast","private_last_persisted");
        f.State("COMPLETED",2);await f.Composition.Router.ReevaluateOnboardingAsync();
        Need(presentation.Current.DisplayName=="AcceptedAlias","presentation_refresh");
        Need(HomePlayerText.Greeting(presentation.Current,"en").Contains("AcceptedAlias"),"home_alias");
        Need(!HomePlayerText.Greeting(presentation.Current,"en").Contains("Private"),"private_names_not_public");
        Console.WriteLine("PLAYER_ALIAS_BINDING_CHECKS="+checks+"_PASS REAL_OPERATIONS=0");
    }
}
