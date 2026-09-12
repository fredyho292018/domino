# Firebase Bootstrap + Guest Authentication V1

Source: `3edb841a80c4ab869a33a4765e0c41b2586f3c7f`.

## Audit

- CURRENT_BOOTSTRAP_FOUND=NO. `DominoClientController.Start` initializes scene presentation, localization and rules; it is not an application-wide service root.
- CURRENT_FIREBASE_CODE_FOUND=NO in Domino scripts. Firebase SDK 13.16.0 was already imported.
- Main scene: `Assets/_Domino/Scenes/DominoClient.unity`. No existing global persistent services or Domino asmdefs.
- Configuration verified without modification: `teamfho-domino`, Android `com.teamfho.domino`, `Assets/google-services.json` present.

## Integration

`ApplicationServices` is the single runtime composition root. Before scene load it creates the SDK adapter, dependency bootstrap and identity service. It does not create scene objects, change the controller, or gate offline play on the network.

Consumers use `await ApplicationServices.Identity.InitializeAsync()`. The result is an immutable `PlayerIdentity` containing only UID and IsAnonymous, without Firebase SDK references. State and Error expose startup/authentication failures; `ApplicationServices.Firebase.State` distinguishes dependency initialization from authentication failure.

Both services cache their initialization task, including failure. Repeated calls share the same operation. Authentication reads CurrentUser only after dependencies are Available and the default app exists. A present user is reused, including a future non-anonymous identity. Otherwise exactly one anonymous sign-in is issued. A missing result/current user fails explicitly.

The runtime entry point runs on Unity's main thread. Ordinary await captures UnitySynchronizationContext; there is no ConfigureAwait(false), background Unity API call, async void startup, or unobserved startup failure. Application exit cancels the lifetime, preventing follow-up initialization/publication after shutdown. SubsystemRegistration resets managed references even with domain reload disabled. No Firebase session is signed out, deleted or copied into PlayerPrefs. Firebase persists its own session.

No Firestore, profiles, wallet, coins, ads, account linking, other providers, backend, game rules, scoring, bots, UI or board layout were implemented/modified.

## Validation

- `RunGuestAuthTests.ps1`: 43 checks PASS. Existing anonymous/non-anonymous reuse, dependency gate, UID propagation, shared tasks before/during/after login, faults, null user, and shutdown cancellation. Uses an in-memory adapter, no Firebase requests.
- `RunTests.ps1`: existing domain suite PASS, 2,981,089 checks / 1,000 games; configuration 82 checks; scoring 32,679 checks; unchanged regression traces and all four seat perspectives.
- Static compilation PASS (three existing CS0649 serialized-field warnings).
- Opt-in Unity `Domino.Editor.FirebaseGuestValidation.Run`: two Play Mode entries in Unity 6000.0.41f1. First run CurrentUser absent, anonymous sign-in successful. Second run reused existing user. Same UID `lAMH…MqS2`, anonymous both times; dependencies Available; SUCCESS, console errors 0.
- Raw integration evidence: `Generated/firebase-guest-editor.txt` and `.log` (ignored by Git).
- Existing Unity Play Mode full-match smoke PASS: six rounds, final score 215:45, target 200, console errors 0. Checks dealing, reserve, teams, selection/drag, legal plays, passes, turns and restart with Firebase active. Evidence: `Generated/firebase-existing-smoke-result.txt`, `.log` and `firebase-existing-smoke-chain.png`.
- Firebase Console user list: PENDING_MANUAL. Check Authentication > Users for the UID from the local log. No manual user creation is needed.
- Android module/ADB present, but no device/emulator attached; installation and cold-restart persistence NOT_RUN.

## Limitations

A failed initialization remains failed for that application lifetime; automatic retry is intentionally absent to avoid accidental duplicate guest requests. Restart retries, checking Firebase's persisted user first. No request timeout is imposed on Firebase's native operation. Offline gameplay remains available while authentication is pending or failed; failures are visible in Console and service Error.

UID persistence was verified across Play stops/starts, not an Android process restart. No SDK cache was cleared or user signed out to manufacture a first run. Uninstalling/clearing app data can remove a guest session. The debug integration runner never runs on ordinary app startup and exists only in an Editor folder.

References: [Firebase anonymous authentication](https://firebase.google.com/docs/auth/unity/anonymous-auth), [Firebase Auth startup](https://firebase.google.com/docs/auth/unity/start), [Unity async continuation guidance](https://firebase.googleblog.com/2019/07/firebase-and-tasks-how-to-deal-with.html).

No commit or push in this task.
