# AUTH-02A — Welcome alignment contract

Welcome is independent from secondary Auth header alignment. This correction changes only ProductionWelcomeView presentation and its Editor validation.

Source diagnosis: the Welcome content container explicitly used Justify.Center (vertical centering), contrary to the corrected upper-page contract. Header text alignment was implicit. Provider buttons did not override built-in horizontal margins, so their outer edges were not explicitly constrained to match labels. The previous Email/Register centering edit did not touch Welcome.

Applied: FlexStart vertical flow, explicit left-aligned header/support labels, zero header/provider horizontal margins. One existing ThemeStyles.Page column remains, with 24 padding on each side and centered maximum outer width 620. Provider icons, colors, heights, internal layout, footer callback, routing and auth remain unchanged. The separator has a diagnostic name only. The inline footer retains its independent centered layout.

```text
WELCOME_SINGLE_CONTENT_COLUMN=YES
WELCOME_HEADER_ALIGNMENT=LEFT
WELCOME_TITLE_ALIGNMENT=LEFT
WELCOME_SUBTITLE_ALIGNMENT=LEFT
WELCOME_CONTENT_LEFT=24_AT_393
WELCOME_CONTENT_RIGHT=369_AT_393
WELCOME_CONTENT_WIDTH=345_AT_393
PROVIDER_ROW_WIDTH=345_AT_393
ALL_LEFT_EDGES_EQUAL_WITHIN_TOLERANCE=YES
PROVIDER_WIDTHS_EQUAL=YES
OR_SEPARATOR_CENTERED_WITHIN_COLUMN=YES
WELCOME_SIGNIN_GROUP_CENTERED=PRESERVED
STALE_COMING_SOON_AFTER_NAVIGATION=NO_EXISTING_ROUTER_TEST_PASS
WELCOME_COLUMN_ALIGNMENT=8/8_PASS
WELCOME_CENTERING_OF_COLUMN=8/8_PASS
WELCOME_OVERFLOW=8/8_PASS
WELCOME_REACHABILITY=8/8_PASS
LOCAL_COMPILER_ERRORS=0
AUTH_FUNCTIONAL_BEHAVIOR_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL_WELCOME_ALIGNMENT_REVIEW
```

At logical viewport 393, with no horizontal safe inset, expected content bounds are 24..369 (width 345); these are contract expectations, not measured results. On large viewports the existing outer page cap includes its padding. Current validation records viewport, safe viewport, content/title/provider left and right edges and checks a tolerance below one logical unit. Refresh is pending; historical results are not reused. Final isolated validator mount is Welcome 393x852 with fresh hidden status. No real Guest operation was performed.

## Current Unity evidence

Fresh corrected validator: CHECKS=816_PASS, FAIL=0. All eight presets include the new logical-edge assertions, left header alignment, upper-page flow, footer and provider regressions. At 393x852: viewport/safe width 393; content/title/provider left 24, right 369, width 345. At tablets, outer Body remains capped at 620 including two 24-unit paddings (inner width 572). Fresh Welcome has hidden status. Final isolated Welcome 393x852 request issued after successful validation. Earlier pending notes above describe pre-import status only.

Final source imported again through Assets Refresh after setting provider secondary text explicitly to MiddleLeft. Fresh rerun at 11:49:07: 816 PASS / 0 FAIL. Final Welcome preview request issued again. All alignment edits are scoped to Welcome; secondary Auth title alignment remains independent.
