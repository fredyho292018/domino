# STARTER TILE VISUAL UX REPORT

SOURCE_SHA=65a4cb70b880f72f961543dcf035e8f9b8d9cfe0

## Audit before implementation

- GAMEPLAY_TABLE_ROOT=`Domino Canvas/Safe area/Landscape composition`
- GAMEPLAY_BOARD_VIEW=`Domino.UI.BoardView`
- GAMEPLAY_TABLE_BACKGROUND=`BoardView` creates `Backdrop/SoftBackdrop`; the
  existing `Playing surface/Felt lighting` uses `SoftBackdrop.TableSurface`.
- GAMEPLAY_TABLE_MATERIAL=existing Unity UI material/stencil pipeline;
  `UiKit.Rounded` supplies the existing sliced rounded sprite. No separate
  table material or sprite was added.
- GAMEPLAY_TABLE_THEME_SOURCE=`DominoVisualTheme`, used by `BoardView`,
  `SoftBackdrop`, and `UiKit`.
- GAMEPLAY_TILE_RENDERER=`DominoTileView` + `DominoFace`, using the existing
  `Assets/_Domino/Prefabs/DominoTile.prefab`.
- GAMEPLAY_TILE_THEME_SOURCE=`TileStyles.Current` / `TileStyles.Changed`.
- SAFE_AREA=`BoardView.safe` / `SafeArea`; existing portrait layout in
  `PortraitBoardPresentation.Fit` controls the table dimensions.
- TABLE_LAYERS=Table shadow, Table outer rail, Brass inlay, Felt, Inner stitch,
  Playing surface. All six layers are the original gameplay objects.

The local shared-device controller already creates this BoardView before
starter selection. The opaque `SharedDevicePrompt/Private handoff` panel was
covering it. The new HIGH_TILE_SELECTION presentation hides that panel and
parents only its title, instructions and two tile views to `BoardSurface`.

## Implementation

STARTER_REUSES_GAMEPLAY_TABLE=YES
STARTER_TABLE_THEME_SOURCE=GAMEPLAY_TABLE_THEME_SOURCE
STARTER_TILE_THEME_SOURCE=GAMEPLAY_TILE_THEME_SOURCE
TABLE_MATCHES_GAMEPLAY=PASS
GAMEPLAY_TABLE_VISIBLE_DURING_STARTER=PASS
GENERIC_BACKGROUND_REMOVED=YES (hidden during HIGH_TILE_SELECTION only)
REAL_TILES_ON_TABLE=PASS
GENERIC_TILE_BUTTONS_VISIBLE=NO
DIRECT_TILE_INTERACTION=PASS
SELECTION_FEEDBACK=PASS
TABLE_VISUAL_CONTINUITY=PASS
STARTER_SELECTION_TABLE_INSTANCE_EQUALS_GAMEPLAY_TABLE_INSTANCE=YES
REFERENCE_IMAGE_COPIED=NO
NEW_TABLE_ASSET_CREATED=NO

`StarterTilePresentation` creates two reusable instances of the actual tile
prefab, with the same 96:46 renderer proportions and a responsive scale
targeting 1.45 times the local hand scale, capped to the available table.
They use native Button hit targets on their real mesh, with no button panel.
The existing selected-tile mesh supplies the gold outline/shadow; elevation
and 1.07 scale feedback interpolate using unscaled time.

The presentation never reads hidden candidates. After the first tap the
chosen tile remains concealed and cannot be selected again. Only after the
unchanged model exposes FirstRevealed/SecondRevealed are the faces rendered,
with a short flip. The result stays on the table for 1.35 seconds, then the
existing deal starts on the same BoardView. No extra Continue form is shown
for this HIGH_TILE_SELECTION result.

Equal sums use the existing localized repeat message, retain the revealed
pair briefly, then reuse the same two views for the next concealed attempt.
The existing model still decides PIP_SUM / REPEAT, RNG, seat and winner.

Player labels use the known selecting seat, via the existing `duel.handoff`
key. No personal ownership of a candidate is assumed. All visible strings
reuse official EN/ES localization entries; no new string table is needed.

## Validation

STARTER_VISUAL_PLAY_MODE=PASS
STARTER_VISUAL_CHECKS=311 PASS
PORTRAIT=PASS (9 sizes)
LOCALIZATION_EN_ES=PASS
TILE_THEMES=PASS (TeamFhoIvory, CubaBlue, TeamFhoWhite)
REVEAL_PRIVACY=PASS
TIE_REPEAT=PASS
TABLE_INSTANCE_CONTINUITY=PASS
DEAL_10_TILES_EACH=PASS
DUEL_1V1=PASS
DUEL_FULL_PLAY_MODE_REGRESSION=238 checks PASS (9 sizes, privacy and full round)
DUEL_GAMEPLAY_REGRESSION=14,720 checks / 200 rounds PASS
PARTNERS_2V2=PASS (shared-engine regression)
CATALOG_GAMEPLAY_REGRESSION=251,685 checks / 250 differential rounds / 100 golden matches PASS
UNITY_STATIC_COMPILATION=SUCCESS
UNITY_COMPILATION=PASS (Unity 6000.0.41f1)
PLAY_MODE=PASS
CONSOLE_ERRORS=0

Validation ran in the isolated `client/Validation/Generated/M2Unity` copy.
Pointer tests raycast the real tile and dispatch a normal pointer click;
they do not bypass the Button handler. Bounds checks cover the safe area
and table coordinates. Captures of all three skins, winner, equal-sum pair,
and gameplay were visually inspected. The tie test initially clicked before
the refreshed Canvas had rendered; waiting for the rendered state fixed the
test without a gameplay change.

Evidence (ignored, not source assets):

- `Generated/StarterTable/result.txt`
- `Generated/StarterTable/starter-TeamFhoWhite.png`
- `Generated/StarterTable/starter-TeamFhoIvory.png`
- `Generated/StarterTable/starter-CubaBlue.png`
- `Generated/StarterTable/result.png`, `tie.png`, `gameplay.png`
- `Generated/m3-unity-result.txt`
- `Generated/starter-table-final.log`, `starter-duel-regression.log`

## Scope and preserved state

LOGIC_CHANGED=NO
HIGH_TILE_COMPARE=PIP_SUM
HIGH_TILE_EQUAL=REPEAT
RNG_CHANGED=NO
RULESET_CHANGED=NO
SCORING_CHANGED=NO
BACKEND_CHANGED=NO
FIRESTORE_CHANGED=NO
I1_I2_RUNTIME_CHANGED=NO
I2_CHANGED=NO
COMMIT=NONE
PUSH=NONE

The change applies to local/shared-device HIGH_TILE_SELECTION. EVEN_ODD_GUESS,
the opaque privacy handoff used after dealing, and the online I1/I2 starter
flow are intentionally unchanged. This is not a fix for the previously
reported normal remote catalogue/mode-selector issue.

User-local AdsSettings.asset and ApiSettings.asset were not edited, staged
or reverted. SHA256 values remain respectively:
`C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6`
and `AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37`.

Files changed: `DominoClientController.cs`, `BoardView.cs`, `SharedDevicePrompt.cs`.
Files added: `StarterTilePresentation.cs` + meta, `StarterTileValidation.cs` + meta,
and this report. No art, prefab, scene, package or material asset was added.
