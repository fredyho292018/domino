using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;

namespace Domino.Player
{
    // Read-only catalog seam shared with onboarding, not a Player fetch or a Home store.
    public interface IPlayerPresentationCatalogs
    {
        Task<OnboardingCatalogDto> ReadPinnedOnboardingCatalogAsync(string locale, int version, CancellationToken token);
        Task<CoachCatalogDto> ReadPinnedCoachCatalogAsync(string locale, int version, CancellationToken token);
    }

    public enum PlayerCoachAvailability { Unavailable, Loading, Ready }
    public sealed class PlayerCoachPresentation
    {
        public PlayerCoachAvailability Availability { get; }
        public string Reason { get; }
        public string Key { get; }
        public int? OnboardingVersion { get; }
        public int? CatalogVersion { get; }
        public string Name { get; }
        public string Description { get; }
        public string AvatarKey { get; }
        public int AvatarVersion { get; }
        public string AvatarPath { get; }
        public bool Selectable { get; }
        public string Locale { get; }

        internal PlayerCoachPresentation(PlayerCoachAvailability availability, string reason, string key = null,
            int? onboardingVersion = null, CoachCatalogDto catalog = null, CoachDto coach = null)
        {
            Availability=availability; Reason=reason; Key=key; OnboardingVersion=onboardingVersion;
            CatalogVersion=catalog?.catalogVersion; Locale=catalog?.resolvedLocale;
            Name=coach?.name; Description=coach?.shortDescription;
            AvatarKey=coach?.avatar?.key; AvatarVersion=coach?.avatar?.assetVersion ?? 0;
            AvatarPath=coach?.avatar?.storagePath; Selectable=coach?.selectable ?? false;
        }
    }

    public static class PlayerCoachResolution
    {
        // The server maps COACH_SELECTION from DominoProfile.preferredCoachKey, not a client draft.
        public static string SavedKey(OnboardingStateDto state)
        {
            var answers=state?.answers?.Where(x=>x?.questionKey=="COACH_SELECTION").ToArray();
            return answers?.Length==1 && answers[0].type=="COACH_SELECT" &&
                System.Text.RegularExpressions.Regex.IsMatch(answers[0].optionKey ?? "", @"^[A-Z][A-Z0-9_]{0,63}$")
                ? answers[0].optionKey : null;
        }
        public static PlayerCoachPresentation Unavailable(string reason, OnboardingStateDto state = null) =>
            new PlayerCoachPresentation(PlayerCoachAvailability.Unavailable,reason,SavedKey(state),state?.catalogVersion);

        public static async Task<PlayerCoachPresentation> ResolveAsync(OnboardingStateDto state, string locale,
            IPlayerPresentationCatalogs catalogs, CancellationToken token)
        {
            string key=SavedKey(state);
            if(key==null)return Unavailable("NO_VALID_SAVED_SELECTION",state);
            if((state.status!="COMPLETED" && state.status!="IN_PROGRESS") || !(state.catalogVersion>0) || state.domainRevisions==null)
                return Unavailable("MISSING_PINNED_CONTEXT",state);
            if(catalogs==null)return Unavailable("CATALOG_READER_UNAVAILABLE",state);
            var pinned=await catalogs.ReadPinnedOnboardingCatalogAsync(locale,state.catalogVersion.Value,token);
            token.ThrowIfCancellationRequested();
            if(pinned?.catalogVersion!=state.catalogVersion || !(pinned.coachCatalogVersion>0) ||
                pinned.steps?.Where(s=>s?.key=="COACH_STEP").SelectMany(s=>s.questions ?? Array.Empty<OnboardingQuestionDto>())
                    .Count(q=>q?.key=="COACH_SELECTION" && q.type=="COACH_SELECT")!=1)
                return Unavailable("PINNED_COACH_REFERENCE_INVALID",state);
            // Explicit reference. Never equate onboarding version with Coach version or read the current pointer.
            var catalog=await catalogs.ReadPinnedCoachCatalogAsync(locale,pinned.coachCatalogVersion.Value,token);
            token.ThrowIfCancellationRequested();
            if(catalog?.catalogVersion!=pinned.coachCatalogVersion || catalog.items==null ||
                (catalog.resolvedLocale!="en" && catalog.resolvedLocale!="es") || catalog.items.Any(x=>x==null) ||
                catalog.items.Select(x=>x.key).Distinct(StringComparer.Ordinal).Count()!=catalog.items.Length)
                return Unavailable("COACH_CATALOG_CONTRACT_INVALID",state);
            var entry=catalog.items.SingleOrDefault(x=>x.key==key);
            if(entry==null || string.IsNullOrWhiteSpace(entry.name))return Unavailable("SAVED_COACH_UNAVAILABLE",state);
            // Historical inactive entries remain valid presentation; selectable only controls new selection.
            return new PlayerCoachPresentation(PlayerCoachAvailability.Ready,null,key,state.catalogVersion,catalog,entry);
        }
    }
}
