# APP-SHELL-01 Coach avatar update

Ten original illustrated portraits replace the generic coach avatars. The catalog includes five women and five men, with young, middle-aged and older adults. Generated using the built-in imagegen tool; the exact prompt set is recorded in APP_SHELL_01_COACH_PORTRAIT_PROMPTS.md. All ten image files were visually inspected.

Assets: client/DominoGame/Assets/_Domino/AppShellMock/Resources/AppShellMockCoaches/coach_<id>.png
IDs: lucia, elena, amara, mei, sofia, david, mateo, gabriel, leo, omar.

The selection page retains two responsive columns and vertical scrolling. Each entire card selects its coach without rebuilding the page or resetting the scroll position. Avatars use a large circular crop; names are centered, near-white, Semibold 600, without radio prefixes. Both card states use #41403C; selection adds a 3px #71A84B border. The greeting updates on selection and Continue retains #71A84B / #FBFAFA / Bold 700. Existing safe-area handling remains unchanged.

Validation completed:
- C# compilation against Unity assemblies: 0 errors.
- Navigation tests: 85 PASS; toolbar navigation: 17 PASS.
- Protected preexisting files: 102 checked, 0 changed.
- Generated portraits: 10 visually inspected.

Pending: Unity asset refresh, mounted coach-card checks on the three simulated phone sizes, and manual visual review. The validation request is queued; it will leave the coach selection preview open after passing. No automatic visual approval is claimed.

No Coach AI, backend, production Auth, gameplay, commit, push or deployment changes.
