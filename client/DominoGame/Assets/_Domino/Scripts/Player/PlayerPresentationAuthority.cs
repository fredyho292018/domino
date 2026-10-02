using System;
using Domino.Infrastructure.Api;

namespace Domino.Player
{
    public sealed partial class PlayerService
    {
        long? confirmedProfileRevision, confirmedPreferencesRevision;
        bool requireNewerProfile;
        public string PreferredLocale { get; private set; }
        public bool IsCurrentSession => !disposed && !lifetime.IsCancellationRequested &&
            HasConfirmedSnapshots && Player.Uid == sessionUid && identity.Current?.Uid == sessionUid;
        public event Action PresentationInvalidated;

        // Consume the already validated routing GET. No fetch, write, draft, or second Player store.
        // The caller belongs to the router epoch; the snapshot reference also guards owner replacement.
        internal bool ReceiveConfirmedProfile(PlayerSnapshot expected, BasicProfileDto profile, OnboardingDomainRevisionsDto revisions)
        {
            lock (gate)
            {
                if (!IsCurrentSession || State != PlayerSyncState.SYNCED || !ReferenceEquals(expected, Player)) return false;
                if (profile == null) return true; // v1/legacy responses may not expose Basic Profile.
                if (revisions == null || revisions.profile < 0 || revisions.preferences < 0 ||
                    string.IsNullOrWhiteSpace(profile.displayName) ||
                    (profile.preferredLocale != "en" && profile.preferredLocale != "es")) return false;
                if (confirmedProfileRevision.HasValue && (revisions.profile < confirmedProfileRevision.Value ||
                    revisions.profile == confirmedProfileRevision.Value && (requireNewerProfile || profile.displayName != Player.DisplayName))) return false;
                if (confirmedPreferencesRevision.HasValue && (revisions.preferences < confirmedPreferencesRevision.Value ||
                    revisions.preferences == confirmedPreferencesRevision.Value && profile.preferredLocale != PreferredLocale)) return false;
                bool changed = Player.DisplayName != profile.displayName || PreferredLocale != profile.preferredLocale;
                if (Player.DisplayName != profile.displayName)
                    Player = new PlayerSnapshot(Player.Uid, Player.AccountType, profile.displayName, Player.Language, Player.Status);
                PreferredLocale = profile.preferredLocale;
                confirmedProfileRevision = revisions.profile;
                confirmedPreferencesRevision = revisions.preferences;
                requireNewerProfile = false;
                if (changed) Notify(SnapshotChanged, Player);
                return true;
            }
        }

        void ClearPresentationAuthority()
        {
            PreferredLocale = null;
            confirmedProfileRevision = confirmedPreferencesRevision = null;
            requireNewerProfile = false;
            var listeners = PresentationInvalidated;
            PresentationInvalidated = null;
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
                try { listener(); } catch { /* Teardown must finish for all consumers. */ }
        }
    }
}
