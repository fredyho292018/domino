using System;
using System.Globalization;
using System.Linq;
using Domino.Player;

namespace Domino.UI.AppShell
{
    public static class ProfilePlayerText
    {
        public static string CountryCode(PlayerPresentationState state) =>
            state?.Availability==PlayerPresentationAvailability.Ready && BasicProfileRules.Countries.Contains(state.CountryCode)
                ? state.CountryCode : null;
        public static string Country(PlayerPresentationState state,string locale)
        {
            var code=CountryCode(state);
            if(code==null)return locale=="es"?"País no disponible":"Country unavailable";
            // Existing ISO metadata and globalization conventions, without changing process/UI culture.
            try{return new RegionInfo((locale=="es"?"es":"en")+"-"+code).NativeName;}
            catch(ArgumentException){try{return new RegionInfo(code).EnglishName;}catch(ArgumentException){return code;}}
        }
        public static string Joined(PlayerPresentationState state,string locale)
        {
            if(state?.Availability!=PlayerPresentationAvailability.Ready || !state.CreatedAt.HasValue)
                return locale=="es"?"Fecha de ingreso no disponible":"Joined date unavailable";
            // Date-only display uses the stored instant's UTC date, never a machine-local conversion.
            var date=state.CreatedAt.Value.UtcDateTime;
            return locale=="es"?"Se unió el "+date.ToString("d 'de' MMMM 'de' yyyy",CultureInfo.GetCultureInfo("es-ES")):
                "Joined "+date.ToString("MMMM d, yyyy",CultureInfo.GetCultureInfo("en-US"));
        }
        public static string History(string locale)=>locale=="es"?"Historial de partidas no disponible por ahora.":"Game history is not available yet.";
    }
}
