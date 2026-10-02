using System;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI.AppShell;

static class PlayerMenuBindingTests
{
    static int checks;
    static void Need(bool value, string key) { if (!value) throw new Exception(key); checks++; }
    static EntitlementSummaryDto Access(string key, string status = "ACTIVE") => new EntitlementSummaryDto {
        availability = "AVAILABLE", snapshot = new EffectiveEntitlementsDto { plan = key, status = status, revision = 1 }
    };
    static string Name(PlayerPresentationState value, string locale = "en") => MenuPlayerText.Name(value, locale);
    static string Plan(PlayerPresentationState value, string locale = "en") => MenuPlayerText.Membership(value, locale);
    static void Prepare(RoutingCompositionFixture f, string name)
    {
        f.State("COMPLETED", 2);
        f.Server.State.basicProfile = new BasicProfileDto { displayName = name, preferredLocale = "es" };
        f.Server.State.domainRevisions = new OnboardingDomainRevisionsDto { profile = 2, preferences = 2 };
        f.Server.Access = Access("FREE", "FREE");
    }
    static async Task Main()
    {
        try {
            var player = new PlayerSnapshot("menu-fixture", PlayerAccountType.Registered, "PlayerOne", "en", PlayerStatus.Active);
            foreach (var locale in new[] { "en", "es" }) {
                foreach (var pair in new[] { ("FREE", "FREE"), ("GOLD", "ACTIVE"), ("PLATINUM", "ACTIVE"),
                    ("DIAMOND", "ACTIVE"), ("FAMILY", "ACTIVE"), ("PREMIUM", "ACTIVE"), ("PREMIUM_LEGACY", "ACTIVE") }) {
                    var state = PlayerPresentationState.FromConfirmed(player, locale, Access(pair.Item1, pair.Item2));
                    var expected = pair.Item1.StartsWith("PREMIUM") ? "PREMIUM" : pair.Item1 == "FREE" && locale == "es" ? "GRATIS" : pair.Item1;
                    Need(Name(state, locale) == "PlayerOne", "literal_name_" + locale);
                    Need(Plan(state, locale) == expected, "server_membership_" + pair.Item1 + "_" + locale);
                }
                foreach (var availability in new[] { PlayerPresentationAvailability.Empty, PlayerPresentationAvailability.Loading, PlayerPresentationAvailability.Unavailable }) {
                    var state = PlayerPresentationState.Empty(availability);
                    Need(Name(state, locale) == (locale == "es" ? "Perfil de jugador" : "Player profile"), "neutral_name");
                    Need(Plan(state, locale) == "", "unresolved_not_free");
                }
                Need(Plan(PlayerPresentationState.FromConfirmed(player, locale, null), locale) == "", "missing_entitlement");
                Need(Name(null, locale) != "Alex" && !Name(null, locale).Contains("Demo player"), "no_demo_fallback");
            }
            Need(MenuPlayerText.Name(null, "es", key => key == "profile.title" ? "Localized profile" : null) == "Localized profile", "existing_localized_key");
            Need(MenuPlayerText.ProductBrand == "Cuban Domino Club", "brand_not_entitlement");
            var longName = "WWWWWWWWWWWWWWWW";
            Need(DisplayNameRules.IsValid(longName), "long_valid_name");
            Need(Name(PlayerPresentationState.FromConfirmed(new PlayerSnapshot("menu-fixture", PlayerAccountType.Registered, longName, "en", PlayerStatus.Active), "en", null)) == longName, "no_domain_truncation");
            using (var f = new RoutingCompositionFixture()) using (var source = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
                Prepare(f, "PlayerOne");
                Need(Plan(source.Current) == "" && Name(source.Current) == "Player profile", "loading_clear");
                await f.Forms.RestoreAsync();
                Need(Name(source.Current) == "PlayerOne" && Plan(source.Current) == "FREE", "restored_identity");
                int calls = f.BootstrapCalls + f.StateCalls;
                for (int i = 0; i < 10; i++) { Name(source.Current); Plan(source.Current); }
                Need(calls == f.BootstrapCalls + f.StateCalls, "read_and_refresh_no_network");
                f.Player.ReceiveEntitlements(f.Player.Player.Uid, Access("GOLD"));
                Need(Plan(source.Current) == "GOLD", "entitlement_update");
                f.Auth.SignOut();
                Need(Name(source.Current) == "Player profile" && Plan(source.Current) == "", "logout_clear_before_route");
                f.Session = new FirebaseAuthSessionSnapshot("second-menu-fixture", true, false, false); await f.Auth.RestoreAsync();
                Need(Name(source.Current) != "PlayerOne", "session_b_cannot_see_a");
                Need(f.AuthWrites == 0 && f.Server.Applied == 0 && f.Server.TrialApplied == 0, "no_domain_writes");
            }
            foreach (var error in new[] { "BOOTSTRAP", "ONBOARDING", "UPDATE" }) {
                using var f = new RoutingCompositionFixture(); using var source = new PlayerPresentationSource(f.Player, f.Composition.Router);
                Prepare(f, "PlayerOne"); f.ErrorAt = error; await f.Forms.RestoreAsync();
                Need(Plan(source.Current) == "" && Name(source.Current) == "Player profile", "failure_not_fake_" + error);
            }
            using (var a = new RoutingCompositionFixture()) using (var old = new PlayerPresentationSource(a.Player, a.Composition.Router)) {
                Prepare(a, "PlayerOne"); a.Server.Hold = new TaskCompletionSource<bool>(); var late = a.Forms.RestoreAsync();
                a.Dispose();
                using var b = new RoutingCompositionFixture(); using var next = new PlayerPresentationSource(b.Player, b.Composition.Router);
                Prepare(b, "PlayerTwo"); await b.Forms.RestoreAsync(); a.Server.Hold.SetResult(true); await late;
                Need(Name(old.Current) != "PlayerOne" && Plan(old.Current) == "", "late_a_clear");
                Need(Name(next.Current) == "PlayerTwo", "late_a_does_not_override_b");
            }
            Console.WriteLine("MENU_BINDING_TESTS=" + checks + "_PASS\nFAIL=0\nREAL_OPERATIONS=0");
        } catch (Exception e) { Console.WriteLine("FAIL=" + e.Message); Environment.ExitCode = 1; }
    }
}
