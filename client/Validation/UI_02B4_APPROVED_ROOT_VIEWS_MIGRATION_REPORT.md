# UI-02B.4 Approved root views migration

Base: fa9f970d6b9ea5c5dd59c2534dd66fdeafdbe0e6.

## Audit and scope

Historical mock Puzzles offered a Daily Puzzle card/preview; Learn provided selected coach, avatar/greeting and training entries; Watch was an intentionally undefined navigation preview. Current authorization expands only the specified restrained demo content. Production previously had title/Coming Soon placeholders. No mock runtime code is reused.

ProductionContentRoots.cs replaces the three root placeholders using ProductionRootPage and shared ThemeProvider/AppTheme/ThemeStyles/ThemeButton. ProductionContentRoot provides consistent eyebrow/title/subtitle, token-styled cards, centered620 max-width,24-unit padding and hidden vertical scrollbar with functional scrolling. The existing shell alone owns navigation.

RootContentData.cs isolates PuzzleSummary/PuzzleCategorySummary, IPuzzlesDataSource/DemoPuzzlesDataSource; ILearnDataSource/DemoLearnDataSource; WatchGameSummary/IWatchDataSource/DemoWatchDataSource. Learn receives the existing HomeCoachSummary from DemoHomeDataSource at shell composition, sharing Amara ID/name/greeting/avatar without depending on the Home view or adding a conflicting coach model. Home's source and data remain unchanged. Watch contains three fictional match summaries, mixed1v1/2v2 and demo durations. Puzzles has one daily challenge, Intermediate difficulty, progress and Opening/Strategy/Counting/Endgame categories.

RootDestination adds only Puzzle/Lesson/CoachGames/WatchGame placeholders. Public entry methods enforce the expected parent tab; Back restores that tab. Menu/Profile paths and placeholder behavior remain intact. Preview resize captures the root destination as well as existing Menu/Profile state. Learn Puzzles switches tabs; Home Continue Learning enters the real Learn root. No services, engines, playback or AI are connected.

## Validation

Fresh Unity validation completed 2026-09-30 00:02–00:04 local, after the current import. Root resize 41 PASS; Home/shell 680 PASS; Menu 977 PASS; Profile 1019 PASS; Puzzles/Learn/Watch 1576 PASS. Total 4333 checks, zero failures. All 24 root/size cases passed centering, overflow, reachability and routes. Final preview is Puzzles at 393×852; Learn and Watch are accessible through the toolbar. Manual Puzzles, Learn and Watch reviews approved by the user.

Legacy regression: Guest/Auth 43 PASS, matchmaking 75 PASS, Replay 1595 PASS (62 retained matches, 31061 events, no Firestore calls), Social 89 PASS. Unity compiled the updated Editor assembly at 00:00:51. Compiler errors zero; nine distinct preexisting CS0067 unused-event warnings in unrelated test tooling; no blocking exception during this execution. Candidate secret-indicator scan passed across ten files; no mojibake markers. This is an indicator scan, not a guarantee against every possible secret format.

Protected102 hash check PASS; Home/Menu/Profile view and data files unchanged, historical mock unchanged. New files: RootContentData.cs, ProductionContentRoots.cs, Editor/ProductionRootContentValidation.cs plus metadata. Modified: ProductionAppShell.cs composition/guarded root routes, Editor/ProductionShellPreview.cs route preservation, Editor/ProductionProfileValidation.cs test-chain invocation. This report completes ten candidate files. No assets added/changed.

COMMIT=CHECKPOINT_COMMIT_CONTAINING_THIS_REPORT
PUSH=SEE_POST_CHECKPOINT_RECEIPT
DEPLOY=NO
NEXT=FUNCTIONAL MIGRATION PHASE REVIEW

## Final result

```text
BASE_SHA=fa9f970d6b9ea5c5dd59c2534dd66fdeafdbe0e6
PUZZLES_ROOT=PASS
PUZZLES_DATA_SOURCE=DemoPuzzlesDataSource
PUZZLES_TEMP_DATA_ISOLATED=YES
DAILY_PUZZLE=PASS
PUZZLE_CATEGORIES=4_PASS
START_PUZZLE_PLACEHOLDER=PASS
LEARN_ROOT=PASS
LEARN_DATA_SOURCE=DemoLearnDataSource
LEARN_TEMP_DATA_ISOLATED=YES
LEARN_COACH=Amara
COACH_GREETING=PASS
CONTINUE_LEARNING=PASS
LESSONS_ENTRY=PASS
PUZZLES_ENTRY=PASS
COACH_GAMES_ENTRY=PASS
WATCH_ROOT=PASS
WATCH_DATA_SOURCE=DemoWatchDataSource
WATCH_TEMP_DATA_ISOLATED=YES
FEATURED=PASS
RECENT_GAMES=PASS
WATCH_GAME_PLACEHOLDER=PASS
HOME_COACH=Amara
COACH_CONSISTENCY=PASS
PUZZLES_CENTERING=8/8_PASS
PUZZLES_OVERFLOW=8/8_PASS
PUZZLES_REACHABILITY=8/8_PASS
LEARN_CENTERING=8/8_PASS
LEARN_OVERFLOW=8/8_PASS
LEARN_REACHABILITY=8/8_PASS
WATCH_CENTERING=8/8_PASS
WATCH_OVERFLOW=8/8_PASS
WATCH_REACHABILITY=8/8_PASS
PUZZLES_ROUTE_PRESERVATION=PASS
LEARN_ROUTE_PRESERVATION=PASS
WATCH_ROUTE_PRESERVATION=PASS
SCROLLBAR_CONSUMES_CONTENT_WIDTH=NO
HOME_REGRESSION=PASS
HOME_UTF8_REGRESSION=PASS
MENU_REGRESSION=PASS
PROFILE_REGRESSION=PASS
SHELL_REGRESSION=PASS
LEGACY_REGRESSION=PASS
BOTTOM_NAV_COUNT=5
BOTTOM_NAV_ORDER=HOME,PUZZLES,LEARN,WATCH,MENU
LEARN_POSITION=CENTER
BOTTOM_NAV_FIXED=PASS
SAFE_AREA=PASS
ROOT_BACK_ICON_PRESENT=NO
ROOT_BOTTOM_NAV_DUPLICATES=0
PRODUCTION_PUZZLES_DEPENDS_ON_MOCK=NO
PRODUCTION_LEARN_DEPENDS_ON_MOCK=NO
PRODUCTION_WATCH_DEPENDS_ON_MOCK=NO
PUZZLES_HARDCODED_THEME_COLORS=0
LEARN_HARDCODED_THEME_COLORS=0
WATCH_HARDCODED_THEME_COLORS=0
MOJIBAKE_MARKERS=0
PUZZLE_ENGINE_CALLS=0
LESSON_SERVICE_CALLS=0
COACH_SERVICE_CALLS=0
WATCH_REAL_SERVICE_CALLS=0
REPLAY_CALLS_FROM_WATCH=0
SOCIAL_CALLS=0
NEW_BACKEND_CALLS=0
BACKEND_CHANGED=NO
NEW_ENDPOINTS=0
FIRESTORE_SCHEMA_CHANGED=NO
REDIS_CHANGED=NO
PRODUCTION_HOME_CHANGED=NO
PRODUCTION_MENU_CHANGED=NO
PRODUCTION_PROFILE_CHANGED=NO
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
FILES_CHANGED=10
COMMIT=CHECKPOINT_COMMIT_CONTAINING_THIS_REPORT
PUSH=SEE_POST_CHECKPOINT_RECEIPT
DEPLOY=NO
UI_02B4_SUCCESS=YES
MANUAL_VISUAL_APPROVAL=APPROVED
NEXT=FUNCTIONAL MIGRATION PHASE REVIEW
```

## Authorized final checkpoint

Manual Puzzles, Learn and Watch reviews: APPROVED. No production source or visual changes during checkpoint preparation. Retained validation: 4333 Unity checks passed, all eight presets per root and all documented regressions. Home, Puzzles, Learn, Watch and Menu are production root views; Profile is a production subpage. ModernSocialPremium, one PageHost and fixed shell-owned BottomNavigation remain shared.

### Explicit inventory

| Files | Classification |
| --- | --- |
| ProductionContentRoots.cs and metadata | Puzzles / Learn / Watch production source |
| RootContentData.cs and metadata | Isolated temporary data and presentation models for all three roots |
| ProductionAppShell.cs | Composition and placeholder subpage routes |
| Editor/ProductionShellPreview.cs | Preview route preservation |
| Editor/ProductionProfileValidation.cs | Validation chain |
| Editor/ProductionRootContentValidation.cs and metadata | Responsive and routing tests |
| This report | Validation report and checkpoint record |

Exactly ten files. Generated evidence and all 102 protected files are excluded. Home/Menu/Profile and historical mock source remain unchanged. Candidate and staged scans check private keys, credentials, tokens, identity indicators and UTF-8. The staged bytes must match the approved source snapshot.

Commit message: `feat: migrate approved production root views`.

The functional migration baseline is the commit containing this finalized report (`git log -1 --format=%H -- client/Validation/UI_02B4_APPROVED_ROOT_VIEWS_MIGRATION_REPORT.md`). Its exact SHA, push result and remote SHA are recorded after publication in the local `Generated/UI02B4/checkpoint-receipt.json` and final response; a commit cannot contain its own SHA. No Auth/backend integration is started. No deploy.
