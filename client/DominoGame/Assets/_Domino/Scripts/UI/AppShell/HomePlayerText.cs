using System;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // Presentation copy follows the selected production locale, as the existing onboarding/Menu adapters do.
    public static class HomePlayerText
    {
        public static string Greeting(PlayerPresentationState state,string locale,Func<string,string> localize=null) =>
            state?.Availability==PlayerPresentationAvailability.Ready && !string.IsNullOrWhiteSpace(state.DisplayName)
                ? (locale=="es"?"Hola, ":"Hello, ")+state.DisplayName+"."
                : MenuPlayerText.Name(null,locale,localize);
        public static string CoachStatus(PlayerCoachPresentation coach,string locale) =>
            coach?.Availability==PlayerCoachAvailability.Loading
                ? (locale=="es"?"Cargando entrenador…":"Loading coach…")
                : (locale=="es"?"Entrenador no disponible.":"Coach unavailable.");
    }
}
