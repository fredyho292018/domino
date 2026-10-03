using System;
using System.Globalization;
using Domino.Infrastructure.Api;

namespace Domino.Player
{
    public enum PlayerPresentationAvailability { Empty, Loading, Ready, Unavailable }
    public enum PlayerMembershipKey { Unknown, Free, PremiumLegacy, Gold, Platinum, Diamond, Family }
    public enum PlayerTrialState { Unknown, Inactive, Active, Expired }

    // Immutable UI projection. No identifiers, brand, grants, permissions, DTO references or networking.
    public sealed class PlayerPresentationState
    {
        public PlayerPresentationAvailability Availability { get; }
        public string DisplayName { get; }
        public string NameFallbackKey => "profile.title"; // Existing neutral Player profile / Perfil de jugador.
        public PlayerAccountType? AccountType { get; }
        public string PreferredLocale { get; }
        public PlayerMembershipKey Membership { get; }
        public PlayerTrialState Trial { get; }
        public bool? TrialConsumed { get; }
        public DateTimeOffset? TrialEndsAt { get; }
        public PlayerCoachPresentation Coach { get; }
        public string CountryCode { get; }
        public DateTimeOffset? CreatedAt { get; }

        PlayerPresentationState(PlayerPresentationAvailability availability, PlayerSnapshot player = null,
            string locale = null, EntitlementSummaryDto envelope = null, PlayerCoachPresentation coach = null)
        {
            Availability = availability;
            if (availability != PlayerPresentationAvailability.Ready) return;
            DisplayName = string.IsNullOrWhiteSpace(player?.DisplayName) ? null : player.DisplayName;
            AccountType = player?.AccountType;
            PreferredLocale = locale ?? player?.Language;
            Coach = coach;
            CountryCode=player?.CountryCode;CreatedAt=player?.CreatedAt;
            var value = envelope?.availability == "AVAILABLE" ? envelope.snapshot : null;
            if (value == null) return;
            // Current backend emits FREE/PREMIUM. Future commercial keys are presentation only,
            // and are recognized solely in the same authoritative entitlement response field.
            Membership = value.plan == "FREE" && (value.status == "FREE" || value.status == "EXPIRED") ? PlayerMembershipKey.Free :
                value.status != "ACTIVE" ? PlayerMembershipKey.Unknown :
                value.plan == "PREMIUM" || value.plan == "PREMIUM_LEGACY" ? PlayerMembershipKey.PremiumLegacy :
                value.plan == "GOLD" ? PlayerMembershipKey.Gold :
                value.plan == "PLATINUM" ? PlayerMembershipKey.Platinum :
                value.plan == "DIAMOND" ? PlayerMembershipKey.Diamond :
                value.plan == "FAMILY" || value.plan == "FRIENDS_AND_FAMILY" ? PlayerMembershipKey.Family : PlayerMembershipKey.Unknown;
            if (Membership == PlayerMembershipKey.Unknown) return;
            TrialConsumed = value.trialConsumed;
            TrialEndsAt = DateTimeOffset.TryParse(value.trialEndsAt, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var end) ? end : (DateTimeOffset?)null;
            Trial = value.trialActive ? (TrialEndsAt.HasValue ? PlayerTrialState.Active : PlayerTrialState.Unknown) :
                value.status == "EXPIRED" ? PlayerTrialState.Expired : PlayerTrialState.Inactive;
        }

        internal static PlayerPresentationState Empty(PlayerPresentationAvailability state) => new PlayerPresentationState(state);
        internal static PlayerPresentationState FromConfirmed(PlayerSnapshot player, string locale, EntitlementSummaryDto entitlements, PlayerCoachPresentation coach = null) =>
            new PlayerPresentationState(PlayerPresentationAvailability.Ready, player, locale, entitlements, coach);
    }
}
