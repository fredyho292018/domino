# APP-SHELL-01 Membership visual polish

Presentation-only refinement: unified 52px billing segment container with selected Bold face, centered billing column, separate plan/trial/future-charge hierarchy, semantic plan and feature icon colors. Price, feature catalog, trial duration and mock actions are unchanged.

Encoding root cause: prior Python reads without an explicit encoding used Windows cp1252 for UTF-8 bytes, then persisted the misdecoded text. This produced literal mojibake in MockShellView.cs (including SAVE). Repaired reversible encoding sequences in that mock view, saved UTF-8 with BOM, and encoded critical Membership middle dots as C# U+00B7 escapes. Restoring other corrupted literals in this same file restores their original text; no other screen was redesigned.

LOCAL_COMPILE_ERRORS=0
MEMBERSHIP_STATE_TESTS=31_PASS
NAVIGATION_TESTS=85_PASS
TOOLBAR_TESTS=17_PASS
CONTACT_RESULTS_TESTS=11_PASS
PREEXISTING_102_FILES_MODIFIED=0

MOUNTED_PREMIUM_CHECKS=636_PASS across three phone sizes and all eight plan/billing combinations. Verified middle dot and no mojibake, centered billing labels, plan icon colors, centered plan labels, aligned feature rows, selection state, exact prices and summaries. Evidence: Library/AppShellPremium.result.txt. Preview left open at Diamond + Yearly. Manual visual review pending.

REAL_BILLING=NO
PRICES_CHANGED=NO
FEATURES_CHANGED=NO
TRIAL_DURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL MEMBERSHIP VISUAL REVIEW

