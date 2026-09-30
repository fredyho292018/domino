# APP-SHELL-01 PREVIEW ROUTING

2026-09-29. Compilation after the Unity restart contains no CS compiler errors. The mock is an isolated UI Toolkit EditorWindow, not the Game/Simulator view of DominoClient.unity. The previous entry retained static session state and did not explicitly focus/reset the window. Open now creates a fresh NO_SESSION state, rebuilds the mock root, and shows/focuses the preview. No scene, GameObject, Canvas or UIDocument is created by this editor entry.

Changed for this fix: MockShellPreview.cs (reset/rebuild/focus and root name), MockShellView.cs (root identity only), MockShellPreviewValidation.cs and its Unity metadata (explicit opt-in editor validation). No layout redesign.

The user confirmed that App Shell · MOCK appeared. Unity's validation reported 16 passing routing checks and a mounted panel with nonzero geometry. The checks inspect the actual mounted MockShellView: Welcome route, no session, WelcomeAuthRoot, six auth actions, absence of four legacy actions, nine development states, and reset on reopening. This establishes routing/content, not visual design approval or a screenshot-based assertion that all controls fit simultaneously without scrolling.

```text
COMPILER_ERRORS=0
PREVIEW_ENTRY_METHOD=Domino.Editor.MockShellPreview.Open
PREVIEW_SCENE=NONE_EDITOR_WINDOW
PREVIEW_ROOT_OBJECT=AppShellMockPreview/MockPhone/WelcomeAuthRoot
PREVIEW_INITIAL_STATE=NO_SESSION
LEGACY_UI_VISIBLE_IN_PREVIEW=NO
APP_SHELL_MOCK_VISIBLE=YES_USER_CONFIRMED_AND_PANEL_MOUNTED
WELCOME_VISIBLE=YES
GOOGLE_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
FACEBOOK_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
EMAIL_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
PHONE_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
GUEST_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
SIGN_IN_VISIBLE=YES_IN_MOUNTED_WELCOME_CONTENT
DEFAULT_THEME=ModernSocialPremium
NAVIGATION_TESTS=84_PASS
PREVIEW_ROUTING_TESTS=16_PASS
CONSOLE_ERRORS=2_PREEXISTING_DEVICE_SIMULATOR_LAYOUT_EXCEPTIONS; NO_MOCK_ERROR_OBSERVED
PREEXISTING_102_FILES_MODIFIED=0
PRODUCTION_UI_CHANGED=NO
BACKEND_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
VISUAL_REVIEW_READY=YES
VISUAL_REVIEW_APPROVED=NO
NEXT=MANUAL REVIEW — WELCOME/AUTH
```

Evidence: Unity Editor.log from the restarted session; Library/AppShellMockPreviewValidation.result.txt (16_PASS, PANEL_MOUNTED=True, NO_SESSION); navigation runner output (84_PASS); SHA-256 comparison of all 102 protected inventory entries (zero changes). The two earlier exceptions originate in UnityEditor.DeviceSimulation.UserInterfaceController.StoreSerializedStates while saving window layouts, outside the mock; no fix to Device Simulator was attempted.

The preview remains on Welcome/Auth. No onboarding navigation or real authentication was performed. The local validation request was consumed/removed; importing the project again does not automatically launch validation without an explicit request.
