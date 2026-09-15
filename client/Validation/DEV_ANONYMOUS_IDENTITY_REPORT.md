# DEV ANONYMOUS IDENTITY TOOL REPORT

EDITOR_UID=SHA256 fingerprint ED63AA59A290EE7E
STANDALONE_UID=SHA256 fingerprint 0B6A7F7A5D61B850
UIDS_DIFFERENT=PASS
EDITOR_DISPLAY_NAME=FHO-A
STANDALONE_DISPLAY_NAME=FHO-B
NEW_PLAYER_BOOTSTRAP=PASS
DUEL_CREATE_JOIN=PASS
BACKEND_SECURITY_CHANGED=NO
FIRESTORE_RULES_CHANGED=NO
PRODUCTION_UI_EXPOSED=NO
RESET_ANONYMOUS_IDENTITY_USER_VISIBLE_IN_RELEASE=NO
COMMIT=NONE
PUSH=NONE

UID fields deliberately show SHA256 fingerprints, not tokens or raw identifiers. Actual comparison OLD_UID != NEW_UID occurs against full Firebase SDK UIDs in the reset operation and test.

## Usage

- Editor in Play Mode: Domino > Development > Authentication > Reset Anonymous Identity opens the development panel; press Reset Anonymous Identity in that panel.
- Windows Development build: click DEV • Authentication (top left), then Reset Anonymous Identity.
- Wait for the new identity/bootstrap confirmation. Enter FHO-A or FHO-B and press Save Display Name. This calls the existing authenticated PlayerService.UpdateDisplayNameAsync endpoint.
- Only use the reset from the main menu/mode selector. It is unavailable in online entry or a match, for non-anonymous users, and outside LOCAL/DEVELOPMENT/TEST.
- Start both clients, then reset only B. Firebase desktop persistence is shared by default application/profile: future process launches may reuse the latest persisted guest. The tested live Editor A retained its identity while B changed; persistent independently named profiles were not added. No SDK-storage workaround was needed.

Updated standalone: D:\Fredy\development\2026\domino\builds\duel-client-b-auth\DominoGame.exe. This separate complete build includes current source and Addressables; the previous build remains unchanged.

## Implementation

DevelopmentAuthentication is compiled only with UNITY_EDITOR or DEVELOPMENT_BUILD. Its allowlist also rejects PRODUCTION, PROD and unconfigured environments, including in a Development build. No UID input is provided. Duplicate reset calls share one task.

ApplicationServices.ResetDevelopmentAnonymousIdentityAsync stops the old session's realtime, player/reward services and lifecycle objects; calls FirebaseAuth.SignOut then SignInAnonymouslyAsync; requires a different SDK UID; reconstructs the normal composition root and reloads the active scene so profile listeners bind to the new PlayerService. Player documents are bootstrapped exclusively through the existing backend. No Firebase user deletion, PlayerPrefs-wide deletion, manual Firestore writes or cache-file manipulation occurs.

The existing asynchronous startup continuation now retains references to its own session and cancellation token. This prevents an old canceled startup from operating on newly constructed session services. Display-name changes use the existing server-confirmed profile update, not Firebase UID or local wallet changes.

On bootstrap failure the tool reports failure and retains the normal new session/offline behavior; it does not silently create another guest as a retry or fabricate a profile.

## Validation

UNITY_EDITOR_COMPILATION=PASS
WINDOWS_DEVELOPMENT_BUILD=PASS
RELEASE_STATIC_COMPILATION=PASS
RELEASE_TOOL_SYMBOLS_ABSENT=PASS
PLAYER_FOUNDATION_REGRESSION=469 PASS
RESET_SINGLE_FLIGHT=PASS (two calls return the same task)
OLD_UID_DIFFERENT_FROM_NEW=PASS
OTHER_PROCESS_IDENTITY_PRESERVED=PASS
RESET_BLOCKED_IN_MATCH=PASS
FINAL_PLAY_MODE_RUNTIME_EXCEPTIONS=0
FINAL_STANDALONE_RUNTIME_EXCEPTIONS=0

Real validation used a Unity Editor Play Mode process in the synchronized isolated project and an actual Windows standalone, both using normal ApplicationServices and the default Firebase app (no fake auth or named-app substitution). A renamed its server profile, created a DUEL via the real menu; B reset, bootstrapped, renamed its profile and joined using the normal online API. Match: 5fb39616-61a7-4d7a-873b-a7da0476ecb0 (also recorded in client/Validation/Generated/DevAuth/final/A-match.txt). Both clients checked distinct identity fingerprints and joined state.

Evidence: client/Validation/Generated/DevAuth/final/A-result.txt, B-result.txt, editor.log, standalone.log, build.log; regression output in DevAuth/player-tests.log. The opt-in validation harness only runs through the isolated Editor validation method or --dev-auth-check-b with an explicit evidence directory. Normal app startup never resets identity automatically.

The initial test passed identity/join but exposed an existing OnDestroy callback error when abruptly quitting during an online match (OnlineEntryView returning to an already destroyed StartMenuView). Gameplay/menu behavior was not changed for this task. The final harness closes the match view through its existing Close method before quitting; final logs have no runtime exceptions. Unity licensing client diagnostics in the Editor startup log did not prevent compilation/Play Mode and are separate from runtime Console errors.

## Files

- Modified: Scripts/Infrastructure/ApplicationServices.cs.
- Added: Scripts/Infrastructure/DevelopmentAuthentication.cs (+ meta).
- Added: Editor/DevelopmentAuthenticationMenu.cs (+ meta).
- Added: Scripts/Online/Development/AnonymousIdentityValidation.cs (+ meta).
- Added: Editor/AnonymousIdentityValidationEditor.cs (+ meta).
- Added: this report.

Paths are relative to client/DominoGame/Assets/_Domino unless specified. Existing pending changes preserved. AdsSettings.asset and ApiSettings.asset hashes remain unchanged. No backend, security rules, economy or gameplay source changes were made in this task.
