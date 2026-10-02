using System;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // Menu copy only. The shared presentation remains the identity/entitlement authority.
    public static class MenuPlayerText
    {
        public const string ProductBrand = "Cuban Domino Club";
        public static string Name(PlayerPresentationState state, string locale, Func<string, string> localize = null) =>
            state?.Availability == PlayerPresentationAvailability.Ready && !string.IsNullOrWhiteSpace(state.DisplayName)
                ? state.DisplayName : Copy("profile.title", locale == "es" ? "Perfil de jugador" : "Player profile", localize);

        public static string Membership(PlayerPresentationState state, string locale, Func<string, string> localize = null)
        {
            if (state?.Availability != PlayerPresentationAvailability.Ready) return string.Empty;
            switch (state.Membership) {
                case PlayerMembershipKey.Free: return Copy("premium.free", locale == "es" ? "Gratis" : "Free", localize).ToUpperInvariant();
                case PlayerMembershipKey.PremiumLegacy: return Copy("premium.active", "Premium", localize).ToUpperInvariant();
                case PlayerMembershipKey.Gold: return "GOLD";
                case PlayerMembershipKey.Platinum: return "PLATINUM";
                case PlayerMembershipKey.Diamond: return "DIAMOND";
                case PlayerMembershipKey.Family: return "FAMILY";
                default: return string.Empty;
            }
        }
        static string Copy(string key, string fallback, Func<string, string> localize)
        {
            var value = localize?.Invoke(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
