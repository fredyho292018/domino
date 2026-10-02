using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI.AppShell;

static class PlayerPresentationTests
{
    static int mapping, session;
    static void Map(bool ok, string key) { if (!ok) throw new Exception(key); mapping++; }
    static void Need(bool ok, string key) { if (!ok) throw new Exception(key); session++; }
    static EntitlementSummaryDto Access(string plan, string status = "ACTIVE") => new EntitlementSummaryDto {
        availability = "AVAILABLE", snapshot = new EffectiveEntitlementsDto { plan = plan, status = status, revision = 1 }
    };
    static PlayerSnapshot Snapshot(string name = "PlayerOne") => new PlayerSnapshot("presentation-fixture", PlayerAccountType.Registered, name, "en", PlayerStatus.Active);
    static BasicProfileDto Profile(string name = "PlayerOne", string locale = "es") => new BasicProfileDto { displayName = name, preferredLocale = locale };
    static OnboardingDomainRevisionsDto Revision(long n) => new OnboardingDomainRevisionsDto { profile = n, preferences = n };
    static bool Neutral(PlayerPresentationState state) => state.DisplayName == null && state.NameFallbackKey == "profile.title" &&
        state.AccountType == null && state.PreferredLocale == null && state.Membership == PlayerMembershipKey.Unknown &&
        state.Trial == PlayerTrialState.Unknown && !state.TrialEndsAt.HasValue && !state.TrialConsumed.HasValue;
    static async Task Main()
    {
        try {
            Mapping(); await Sessions();
            Console.WriteLine("PLAYER_PRESENTATION_MAPPING=" + mapping + "_PASS");
            Console.WriteLine("SESSION_PRESENTATION_CHECKS=" + session + "_PASS");
            Console.WriteLine("PLAYER_PRESENTATION_TESTS=" + (mapping + session) + "_PASS REAL_OPERATIONS=0");
        } catch (Exception e) { Console.WriteLine("FAIL=" + e.Message); Environment.ExitCode = 1; }
    }
    static void Mapping()
    {
        var state = PlayerPresentationState.FromConfirmed(Snapshot(), "es", Access("FREE", "FREE"));
        Map(state.DisplayName == "PlayerOne" && state.PreferredLocale == "es" && state.AccountType == PlayerAccountType.Registered, "confirmed_name_and_locale");
        Map(state.Membership == PlayerMembershipKey.Free, "explicit_free");
        state = PlayerPresentationState.FromConfirmed(null, null, null);
        Map(Neutral(state), "missing_name_neutral_localized_key");
        foreach (var availability in new[] { PlayerPresentationAvailability.Empty, PlayerPresentationAvailability.Loading, PlayerPresentationAvailability.Unavailable })
            Map(Neutral(PlayerPresentationState.Empty(availability)), "no_identity_when_" + availability);
        foreach (var pair in new[] {
            ("FREE", "FREE", PlayerMembershipKey.Free), ("FREE", "EXPIRED", PlayerMembershipKey.Free),
            ("PREMIUM", "ACTIVE", PlayerMembershipKey.PremiumLegacy), ("PREMIUM_LEGACY", "ACTIVE", PlayerMembershipKey.PremiumLegacy),
            ("GOLD", "ACTIVE", PlayerMembershipKey.Gold), ("PLATINUM", "ACTIVE", PlayerMembershipKey.Platinum),
            ("DIAMOND", "ACTIVE", PlayerMembershipKey.Diamond), ("FAMILY", "ACTIVE", PlayerMembershipKey.Family),
            ("FRIENDS_AND_FAMILY", "ACTIVE", PlayerMembershipKey.Family), ("UNSUPPORTED", "ACTIVE", PlayerMembershipKey.Unknown),
            ("GOLD", "EXPIRED", PlayerMembershipKey.Unknown), ("FREE", null, PlayerMembershipKey.Unknown) }) {
            state = PlayerPresentationState.FromConfirmed(Snapshot(), null, Access(pair.Item1, pair.Item2));
            Map(state.Membership == pair.Item3, "entitlement_" + pair.Item1 + "_" + pair.Item2);
        }
        foreach (var missing in new[] { null, new EntitlementSummaryDto(), new EntitlementSummaryDto { availability = "AVAILABLE" },
            new EntitlementSummaryDto { availability = "UNAVAILABLE", snapshot = Access("FREE", "FREE").snapshot } }) {
            state = PlayerPresentationState.FromConfirmed(Snapshot(), null, missing);
            Map(state.Membership == PlayerMembershipKey.Unknown && state.Trial == PlayerTrialState.Unknown, "missing_not_free");
        }
        var trial = Access("PREMIUM"); trial.snapshot.trialActive = true; trial.snapshot.trialConsumed = true;
        trial.snapshot.trialEndsAt = "2001-02-03T04:05:06Z"; // Deliberately past: no local-clock expiry calculation.
        state = PlayerPresentationState.FromConfirmed(Snapshot(), null, trial);
        Map(state.Trial == PlayerTrialState.Active && state.TrialConsumed == true, "server_trial_active_not_local_clock");
        Map(state.TrialEndsAt == DateTimeOffset.Parse("2001-02-03T04:05:06Z"), "server_trial_date_exact");
        trial.snapshot.trialActive = false;
        Map(state.Trial == PlayerTrialState.Active, "snapshot_immutable_not_dto_reference");
        Map(PlayerPresentationState.FromConfirmed(Snapshot(), null, trial).Trial == PlayerTrialState.Inactive, "consumed_paid_not_expired");
        trial.snapshot.plan = "FREE"; trial.snapshot.status = "EXPIRED";
        state = PlayerPresentationState.FromConfirmed(Snapshot(), null, trial);
        Map(state.Trial == PlayerTrialState.Expired && state.Membership == PlayerMembershipKey.Free, "explicit_server_expired");
        trial = Access("PREMIUM"); trial.snapshot.trialActive = true; trial.snapshot.trialEndsAt = "invalid";
        Map(PlayerPresentationState.FromConfirmed(Snapshot(), null, trial).Trial == PlayerTrialState.Unknown, "bad_date_not_fabricated");
        Map(PlayerPresentationState.FromConfirmed(Snapshot("Guest-ABCDEFGH"), null, null).DisplayName == "Guest-ABCDEFGH", "confirmed_generated_alias_preserved");
        foreach (var property in typeof(PlayerPresentationState).GetProperties()) Map(property.SetMethod == null, "immutable_" + property.Name);
        foreach (var forbidden in new[] { "Uid", "Email", "CreatedAt", "Country", "Experience", "Avatar", "Coach", "Brand" })
            Map(typeof(PlayerPresentationState).GetProperty(forbidden) == null, "minimal_no_" + forbidden);
        Map(Neutral(PlayerPresentationState.Empty(PlayerPresentationAvailability.Loading)), "no_manufactured_demo_identity");
        foreach (var candidate in new[] { PlayerPresentationState.Empty(PlayerPresentationAvailability.Loading),
            PlayerPresentationState.Empty(PlayerPresentationAvailability.Unavailable), PlayerPresentationState.FromConfirmed(null, null, null) })
            Map(candidate.DisplayName != "Alex" && candidate.DisplayName != "Demo player", "explicit_no_demo_name_generation");
    }
    static async Task Sessions()
    {
        using (var f = new RoutingCompositionFixture()) using (var view = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
            Need(Neutral(view.Current) && view.Current.Availability == PlayerPresentationAvailability.Loading, "no_player_loading");
            f.Session = null; await f.Forms.RestoreAsync();
            Need(Neutral(view.Current) && view.Current.Availability == PlayerPresentationAvailability.Empty, "no_session_empty");
            Need(f.BootstrapCalls == 0 && f.StateCalls == 0 && f.AuthWrites == 0, "presentation_does_not_auth_or_load");
        }
        using (var f = new RoutingCompositionFixture()) {
            f.State("COMPLETED", 2); f.Server.State.basicProfile = Profile(); f.Server.State.domainRevisions = Revision(2);
            f.Server.Access = Access("PREMIUM");
            using var view = new PlayerPresentationSource(f.Player, f.Composition.Router);
            await f.Forms.RestoreAsync();
            Need(view.Current.Availability == PlayerPresentationAvailability.Ready && view.Current.DisplayName == "PlayerOne", "restored_server_profile_name");
            Need(f.Player.Player.DisplayName == "PlayerOne" && f.Player.PreferredLocale == "es", "single_player_owner_enriched");
            Need(view.Current.PreferredLocale == "es" && view.Current.Membership == PlayerMembershipKey.PremiumLegacy, "restored_locale_membership");
            Need(f.BootstrapCalls == 1 && f.StateCalls == 1 && f.Server.Applied == 0 && f.Server.TrialApplied == 0 && f.AuthWrites == 0, "no_additional_io_or_mutations");
            var original = view.Current; int notices = 0; view.Changed += () => notices++;
            f.Player.ReceiveEntitlements(f.Player.Player.Uid, Access("GOLD"));
            Need(view.Current.Membership == PlayerMembershipKey.Gold && notices > 0, "authoritative_entitlement_update");
            Need(original.Membership == PlayerMembershipKey.PremiumLegacy, "old_presentation_value_immutable");
            f.Player.ReceiveEntitlements("different-fixture", Access("DIAMOND"));
            Need(view.Current.Membership == PlayerMembershipKey.Gold, "foreign_entitlement_rejected");
            var current = f.Player.Player;
            Need(!f.Player.ReceiveConfirmedProfile(current, Profile("StaleName"), Revision(1)), "older_profile_revision_rejected");
            Need(!f.Player.ReceiveConfirmedProfile(current, Profile("Conflict"), Revision(2)), "equal_revision_conflict_rejected");
            Need(f.Player.ReceiveConfirmedProfile(current, Profile(), Revision(2)), "repeat_profile_idempotent");
            Need(!f.Player.ReceiveConfirmedProfile(Snapshot(), Profile(), Revision(3)), "foreign_snapshot_rejected");
            Need(f.Player.ReceiveConfirmedProfile(current, Profile("PlayerEdited"), Revision(3)), "newer_confirmed_name");
            Need(view.Current.DisplayName == "PlayerEdited", "view_observes_service_name_event");
            Need(!f.Player.ReceiveConfirmedProfile(current, Profile("LateName"), Revision(4)), "old_snapshot_reference_rejected");
            notices = 0; bool clearedInEvent = false; view.Changed += () => clearedInEvent = Neutral(view.Current);
            f.Player.Dispose();
            Need(Neutral(view.Current) && notices > 0 && clearedInEvent, "disposal_clears_and_notifies");
            f.Player.ReceiveEntitlements(f.Session.Uid, Access("DIAMOND"));
            Need(Neutral(view.Current), "late_entitlement_after_disposal_rejected");
        }
        foreach (var failure in new[] { "BOOTSTRAP", "ONBOARDING", "UPDATE" }) {
            using var f = new RoutingCompositionFixture(); using var view = new PlayerPresentationSource(f.Player, f.Composition.Router);
            f.ErrorAt = failure; await f.Forms.RestoreAsync();
            Need(view.Current.Availability == PlayerPresentationAvailability.Unavailable && Neutral(view.Current), "routing_failure_" + failure);
            Need(f.Server.Applied == 0 && f.Server.TrialApplied == 0, "failure_write_free");
        }
        using (var f = new RoutingCompositionFixture()) using (var view = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
            f.State("COMPLETED", 2); await f.Forms.RestoreAsync();
            var a = view.Current;
            f.Auth.SignOut();
            Need(Neutral(view.Current), "auth_logout_clears_even_before_router_event");
            await f.Composition.ResolveAsync();
            Need(view.Current.Availability == PlayerPresentationAvailability.Empty, "logout_route_empty");
            Need(a.DisplayName == "Fixture", "prior_immutable_value_is_not_current_source");
        }
        using (var f = new RoutingCompositionFixture()) using (var view = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
            f.State("COMPLETED", 2); await f.Forms.RestoreAsync();
            f.Auth.SignOut(); // The existing auth service intentionally caches its current identity until invalidated.
            f.Session = new FirebaseAuthSessionSnapshot("other-fixture", true, false, false); await f.Auth.RestoreAsync();
            Need(Neutral(view.Current), "changed_identity_without_bootstrap_hides_old_name");
        }
        using (var a = new RoutingCompositionFixture()) using (var oldView = new PlayerPresentationSource(a.Player, a.Composition.Router)) {
            a.State("COMPLETED", 2); a.Hold = new TaskCompletionSource<bool>(); var late = a.Forms.RestoreAsync();
            a.Dispose();
            using var b = new RoutingCompositionFixture(); b.Session = new FirebaseAuthSessionSnapshot("next-fixture", true, false, false);
            b.State("COMPLETED", 2); b.Server.State.basicProfile = Profile("SecondPlayer", "en");
            using var next = new PlayerPresentationSource(b.Player, b.Composition.Router);
            await b.Forms.RestoreAsync(); a.Hold.SetResult(true); await late;
            Need(Neutral(oldView.Current) && next.Current.DisplayName == "SecondPlayer", "late_player_a_cannot_apply_to_b");
            Need(b.BootstrapCalls == 1 && b.StateCalls == 1, "replacement_no_extra_fetch");
        }
        using (var f = new RoutingCompositionFixture()) {
            f.State("COMPLETED", 2); f.Server.State.basicProfile = Profile(); f.Server.State.domainRevisions = Revision(3);
            await f.Forms.RestoreAsync();
            using var view = new PlayerPresentationSource(f.Player, f.Composition.Router);
            Need(view.Current.DisplayName == "PlayerOne" && f.StateCalls == 1, "source_created_after_restore_uses_confirmed_get");
            int notices = 0; view.Changed += () => notices++; view.Dispose(); notices = 0;
            f.Player.ReceiveEntitlements(f.Player.Player.Uid, Access("GOLD"));
            Need(notices == 0 && Neutral(view.Current), "unsubscribe_after_dispose");
        }
        foreach (var status in new[] { "NOT_STARTED", "IN_PROGRESS" }) {
            using var f = new RoutingCompositionFixture(); f.State(status, status == "NOT_STARTED" ? (int?)null : 2, status == "NOT_STARTED" ? null : "BASIC_PROFILE_STEP");
            using var view = new PlayerPresentationSource(f.Player, f.Composition.Router); await f.Forms.RestoreAsync();
            Need(Neutral(view.Current) && view.Current.Availability == PlayerPresentationAvailability.Loading, "no_shell_identity_before_ready_" + status);
        }
        using (var f = new RoutingCompositionFixture()) {
            f.State("COMPLETED"); using var view = new PlayerPresentationSource(f.Player, f.Composition.Router); await f.Forms.RestoreAsync();
            Need(view.Current.DisplayName == "Fixture", "legacy_missing_optional_profile_uses_bootstrap");
        }
        using (var f = new RoutingCompositionFixture()) using (var view = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
            f.State("COMPLETED", 2); await f.Forms.RestoreAsync();
            f.ErrorAt = "BOOTSTRAP"; await f.Player.RefreshConfirmedAsync();
            Need(f.Player.HasConfirmedSnapshots && view.Current.Availability == PlayerPresentationAvailability.Unavailable && Neutral(view.Current), "failed_refresh_cannot_expose_retained_domain_snapshot");
        }
        using (var f = new RoutingCompositionFixture()) using (var view = new PlayerPresentationSource(f.Player, f.Composition.Router)) {
            f.State("COMPLETED", 2); f.Server.State.basicProfile = Profile(); f.Server.State.domainRevisions = Revision(3);
            await f.Forms.RestoreAsync();
            f.Composition.Dispose();
            Need(Neutral(view.Current), "disposed_routing_not_ready_even_if_player_remains");
        }
        using (var a = new RoutingCompositionFixture()) using (var oldView = new PlayerPresentationSource(a.Player, a.Composition.Router)) {
            a.State("COMPLETED", 2); a.Server.Hold = new TaskCompletionSource<bool>(); var late = a.Forms.RestoreAsync();
            Need(a.BootstrapCalls == 1 && a.Composition.Router.Route == AuthenticatedRoute.ResolvingOnboarding, "hold_confirmed_profile_response");
            a.Dispose();
            using var b = new RoutingCompositionFixture(); // Same UID, new service lifetime: no competing identity epoch.
            b.State("COMPLETED", 2); b.Server.State.basicProfile = Profile("RestoredPlayer", "en");
            using var next = new PlayerPresentationSource(b.Player, b.Composition.Router);
            await b.Forms.RestoreAsync(); a.Server.Hold.SetResult(true); await late;
            Need(Neutral(oldView.Current) && next.Current.DisplayName == "RestoredPlayer", "late_profile_same_uid_old_lifetime_rejected");
        }
    }
}
