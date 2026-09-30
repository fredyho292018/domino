# APP-SHELL-01 LEARN ROOT TAB

Learn is the center root tab. Its SVG is an original rounded graduation cap with the same 24×24 viewport, 2-unit stroke and UI-controlled tint as the existing toolbar. Five equal 20% touch targets replace the four-column layout. Labels use Source Sans 3 Semibold (600); primary actions retain Bold (700).

Learn uses the onboarding-selected coach, with David as the existing-account mock default. It displays Selected Coach, Continue Learning, a next lesson card, Lessons, Puzzles, Coach Games, Game Review, Move Explanations and Training Progress. All destinations are mock. Puzzles remains an independent root; lessons/reviews are placeholders; Coach Games retains the existing mock page. Home links to Learn without duplicating its contents. Menu → Coach remains available.

```text
BOTTOM_NAV_COUNT=5
BOTTOM_NAV_ORDER=HOME,PUZZLES,LEARN,WATCH,MENU
LEARN_POSITION=CENTER
LEARN_ICON=GRADUATION_CAP
LEARN_SVG=Assets/_Domino/AppShellMock/Resources/AppShellMockIcons/icon_nav_learn.svg
LEARN_PAGE=IMPLEMENTED_MOCK
SELECTED_COACH_VISIBLE=YES
CONTINUE_LEARNING_VISIBLE=YES
LESSONS_VISIBLE=YES_IN_SCROLL_CONTENT
PUZZLES_ENTRY_VISIBLE=YES_IN_SCROLL_CONTENT
COACH_GAMES_VISIBLE=YES_IN_SCROLL_CONTENT
LEARN_ROOT_BACK_ICON=NO
HOME_ACTIVE_COLOR=#FBFAFA
PUZZLES_ACTIVE_COLOR=#FBFAFA
LEARN_ACTIVE_COLOR=#FBFAFA
WATCH_ACTIVE_COLOR=#FBFAFA
MENU_ACTIVE_COLOR=#FBFAFA
INACTIVE_COLOR=#969495
SMALL_PHONE_VALIDATION=PASS_GEOMETRY_375x667
TALL_PHONE_VALIDATION=PASS_GEOMETRY_393x852
ANDROID_VALIDATION=PASS_GEOMETRY_412x915
SAFE_AREA_VALIDATION=PASS_SIMULATED_PREVIEW_INSETS
NAVIGATION_TESTS=85_PASS
TOOLBAR_NAVIGATION_TESTS=17_PASS
TOOLBAR_LAYOUT_CHECKS=195_PASS
PREVIEW_ROUTING_TESTS=16_PASS
THEME_TESTS=12_PASS
BACK_ICON_PAGES=31_PASS
COMPILER_ERRORS=0
CONSOLE_ERRORS=NO_NEW_VALIDATION_ERRORS; HISTORICAL_ERRORS_RETAINED
PREEXISTING_102_FILES_MODIFIED=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL LEARN + TOOLBAR REVIEW
```

The old baseline grows from 84 to 85 because Learn is now included in root-tab navigation. Additional navigation checks verify the selected coach remains unchanged and Learn subpages return to Learn. Mounted Unity tests verify order, center active state, five imported VectorImages, exact icon/label tint, semibold font, target sizes, equal widths (one pixel rounding permitted), bounds and bottom inset in all three viewports. Physical device/safe-area testing and optical recognition remain manual review, not automatically approved.

Evidence: Library/AppShellToolbar.result.txt (all three devices PASS, 195 checks), Library/AppShellMockPreviewValidation.result.txt (16/12/31 checks), navigation runner (85/17), protected inventory hash comparison (102 identical). The validation ends by calling OpenLearn. It does not enter Play, contact a backend, or invoke billing/AI.
