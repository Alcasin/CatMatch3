# Historical Development and QA Notes

This document preserves the previous CatMatch3 README in full as historical
development and QA documentation. Milestone-specific setup instructions, validation
statements and pending QA notes reflect the project state when they were written;
they are not a current release or testing report. Paths in the preserved notes are
relative to the repository root.

For the current overview and run instructions, see the [project README](../README.md).

---

# CatMatch3 — Gameplay presentation

## M5-D2 sound feedback

After Unity imports/compiles, run **CatMatch3 > Setup > Apply M5-D2 Audio** once
in Edit Mode. Save or revert pending Gameplay edits first. The command imports the
seven generated mono WAVs as preloaded PCM, adds/reuses one BoardAudio on the
existing BoardView object, binds every clip and the view reference, and saves
Gameplay. It preserves custom bindings, restores the active scene and closes only
the scene it opened. Reruns do not duplicate components or change levels/settings.
The Editor command has not been executed by this implementation session.

BoardAudio creates two reusable 2D AudioSources once in Play Mode and uses the
existing camera AudioListener. Short cues use PlayOneShot; a separate result source
lets a terminal jingle replace active effect tails. No runtime AssetDatabase,
Resources lookup, per-tile sources, delayed audio callbacks or audio coroutines
are used. Until setup binds the references, gameplay stays silent and the footer
SFX control is disabled.

| Cue | Character / length | Presentation event |
| --- | --- | --- |
| SFX_Select | Soft high tick, 0.055 s | Selected cell changes; repeated refresh/deselection is silent |
| SFX_Invalid | Low rounded boop, 0.160 s | Invalid swap begins returning |
| SFX_Match | Short pitched/noisy pop, 0.120 s | Clear animation starts, once per nonempty wave |
| SFX_Striped | Filtered airy sweep, 0.220 s | Each real SpecialActivation sweep starts |
| SFX_Combo | Rising two-tone chime, 0.260 s | Clear starts on a later wave, alongside the pop |
| SFX_Win | C/E/G three-note jingle, 0.620 s | Win panel appears, once per terminal result |
| SFX_Fail | Gentle descending two-note cue, 0.440 s | Fail panel appears, once per terminal result |

All WAVs live in `Assets/_CatMatch3/Art/Audio/SFX/`: original procedural mono
PCM16/22050 Hz, 82,996 bytes total. Soft envelopes, filtered noise and shaped tones
have zero-valued endpoints, negligible DC and peaks limited to 55% of full scale. No third-party
samples or downloads are used. `python Tools/generate_sfx.py` deterministically
regenerates them; `--check` verifies their exact bytes and waveform bounds without
writing. The script leaves Unity GUIDs/metadata untouched.

The master source volume defaults to 0.6 with per-cue gains of 0.22-0.48. The
rounded **SFX ON/OFF** control fits to the right of Restart in the existing footer;
HUD/board sizing and Restart placement are unchanged. Its PlayerPrefs key is
`CatMatch3.AudioEnabled`, enabled by default, saved immediately on toggle. Muting
stops active sounds; unmuting never replays an old event. Selection/stripe rate
limits bound stacking, and terminal events latch until cancellation/reset even
while muted. Restart/Retry/level changes, errors, disable and app pause stop active
playback. No sound delays or alters gameplay transitions or animation timing.

Validation: Player/Runtime and Editor compilation against installed Unity
6000.3.8f1 assemblies passed, as did all 101 standalone NUnit tests. Run
`powershell -File Tools/validate_audio.ps1` for repeatable WAV, clip/gain mapping,
cooldown, terminal/error mapping, 200 result/reset duplicate checks and seven footer
layout checks. Unity import/setup, actual playback, PlayerPrefs persistence and
Unity Test Runner still require in-Editor verification.

Manual QA: apply setup twice and reopen Gameplay to confirm persistent references
and one BoardAudio. Check selection/deselection, invalid rollback, clears, both
stripe directions/chains and later-wave chimes. Verify exactly one win/fail cue,
including final-move cascade Win, OutOfMoves and NoLegalMoves. Restart during
effects, Retry, Next Level, Play Again and diagnostic changes must stop old sounds
without accumulating sources. Toggle SFX during resolution and confirm silence;
exit/reenter Play Mode (and relaunch a build) to verify the preference. Audition at
ordinary phone volume, check the footer on small/tall portrait safe areas, and run
all 101 Unity EditMode tests. No haptics are included.

## M5-D1 gameplay feedback

No Editor setup is needed. The existing ordered snapshot/coroutine presentation
now gives matched cats a quick 5% pop followed by a smooth fade and gentle shrink
within the original 0.22-second clear. Later waves use an 8% pop and a small,
outlined `Combo!` that rises over the first clearing cell and fades in the same
clear slot. The cascade flag comes from the existing wave index, with no scoring
or objective changes.

Gravity and refill retain their original paths and durations (0.30/0.38 seconds),
with monotonic cubic ease-out for a responsive fall and soft stop. No bounce or
extra landing phase is added. Valid/invalid swap motion, special-creation pulse
and diagnostic reshuffle animation remain unchanged.

The existing single stripe Image now sweeps outward from the activated cat along
its row or column in soft cyan/turquoise, at 18% cell thickness and at most 32%
opacity. Position/direction come directly from each SpecialActivation, including
chain entries. It still takes 0.22 seconds and does not replace or move stripe
indicators.

The combo Text is created once with raycasts disabled; the stripe Image is reused.
No objects, textures or callbacks are created per effect frame. All effects run
inside the existing BoardPrototype coroutine wrapper, with no separate tween or
background coroutine. Iterator disposal hides temporary feedback, and explicit
cleanup also runs on cancellation, disable, error, snapshots and terminal results.
Restart/Retry/level changes still cancel before restoring the board; snapshots
restore tile alpha, scale, positions and emphasis. Input gating and final-result
priority are unchanged.

Validation: player/Editor compilation against Unity 6000.3.8f1 and all 101 standalone
NUnit tests passed. Managed checks passed for 201 easing/pop samples, five real
horizontal/vertical/chain activations at three board sizes, the three-wave cascade
fixture and 100 nested-iterator cancellations. These cancellation checks exercise
the managed wrapper, not Unity's native StopAllCoroutines or UI rendering.

Manual Unity QA: check valid/invalid tap and swipe swaps, small clear pops and
smooth falls/refills; run M2 cascade, M3 horizontal/vertical creation/activation
and chain fixtures. Confirm Combo appears only on later waves and all temporary
feedback disappears. Restart during each animation phase and switch diagnostic
fixtures; check Retry, Next Level and Play Again repeatedly for stale effects or
hierarchy growth. Replay both winning routes and M4 final-move cascade Win, plus
both failure fixtures. Check input remains gated during resolution, terminal
panels appear after effects end, and paw/stripe/selection visuals remain intact.
Run all 101 Unity EditMode tests. Unity Test Runner, native cancellation and visual
Play Mode verification were not run here.

## M5-C result panels

No Editor setup or scene assignments are required. A single reusable rounded cream
card sits in the device safe area over a full-screen slate scrim (42% opacity), so
the finished board remains visible. It reuses the existing rounded UI sprite,
LegacyRuntime font and cat artwork: Orange on a warm portrait well for success,
Gray on a soft blue-gray well for retry. Ink titles, muted secondary text and a
rounded amber primary button match the gameplay HUD. No entrance animation or
new artwork/texture is added.

| Result | Title | Message | Primary action |
| --- | --- | --- | --- |
| Level 1 Win | Level Complete! | Purrfect! | NEXT LEVEL |
| Level 2 Win | Game Complete! | Purrfect! | PLAY AGAIN |
| OutOfMoves | Try Again! | Out of Moves | RETRY |
| NoLegalMoves | Try Again! | No more matches available | RETRY |

Core still decides the terminal state. BoardPrototype presents it after the final
wave, refill and objective update, then delegates actions to its existing campaign
flow. The scrim blocks background pointer events; the existing Terminal state
rejects swaps. One button listener disables itself before invoking the action,
and ShowResult resets its availability for the next result. Retry, deterministic
boards, final-move win priority and diagnostic fixtures remain unchanged.

The reference card is 720x640 UI units with a 624x128 action target. It scales from
safe-area width/height and centers within those bounds. No gameplay HUD or board
layout was changed. Compilation passed for player/Editor configurations against
Unity 6000.3.8f1, and all 101 standalone NUnit tests passed. Additional managed
checks verified six mappings from real terminal sessions (both failures at both
levels and both final-move wins), plus 30 portrait/safe-area layout cases. These
checks cover containment, spacing and button dimensions, not Unity rendering.

Manual Unity QA: complete both known puzzle routes and check NEXT LEVEL / PLAY
AGAIN; use M4 QA Win/Fail/Final move cascade Win and M4 Puzzle QA No Legal Moves.
Confirm failure explanations, completed objective counts, dimmed board visibility,
blocked background taps/swipes/Restart, and repeated action taps causing only one
transition. Check Retry restores the current level, Play Again starts Level 1,
Restart still works during gameplay, and M2/M3/M4 diagnostics still work. Inspect
9:16, tall Android and small portrait Game views with safe-area insets for readable
titles/messages, unclipped panels and comfortable buttons. Run all 101 Unity
EditMode tests. Unity Test Runner and visual Play Mode QA were not run here.

## M5-B3 selection feedback

Selected tiles now use a transparent rounded outline in deep crimson (#8B1E2D),
about 2.5 reference pixels thick and scaled with the cell (capped at 3). It uses
the existing cell gutter outside the striped edge accents and corner paw motif,
with no golden selection fill over cats or paw
targets. The non-interactive frame follows the tile's existing visibility/scale
and resets through the existing selection/snapshot lifecycle. Special creation
retains its scale pulse without borrowing the selection highlight. No Editor
setup, new artwork or serialized references are required.

Validation: player and Editor compilation against Unity 6000.3.8f1 assemblies and
all 101 standalone NUnit tests passed. Managed geometry checks passed for the
selection color, transparent interior, thickness, paw/stripe clearance and rebuild
cleanup at four cell sizes, plus the existing paw mesh and 60 layout checks.
Unity Test Runner and visual Play Mode verification were not run.

Manual QA: select/deselect normal, honey paw and both striped variants; check
tap-to-swap and swipes, invalid rollback, valid swaps/cascades, special creation,
Restart/Retry/Next Level/Play Again, diagnostic fixtures and all 101 Unity EditMode
tests. Confirm no stale outline, hidden paw motif or overlapping stripe band.

## Paw target visibility refinement

No Editor setup or asset assignments are required. Remaining paw targets now use
a honey-colored rounded cell, a darker warm border and a small five-pad paw motif
in the lower-right corner. The graphic stays behind cats on the fixed board cell;
marked tiles reveal it through their background while retaining a translucent
match tint and the separate crimson selection outline. Existing objective snapshots
remove completed targets and
restore the ordinary tile background. Cat sprites, stripe bands and hitboxes are
unchanged. The motif uses UI geometry, with no new texture or sprite references.

Validation: player/Editor compilation and all 101 standalone NUnit tests passed.
Managed checks also passed for paw mesh bounds/palette/rebuild cleanup at four
cell sizes and 60 existing layout cases, including all 49 cell-center coordinates.
Unity Test Runner and in-Editor visual QA still need verification. In Level 2,
check all eight starting targets remain readable under cats, progress during
ordinary matches and striped activations, and removal of completed targets.
Replay the known four-move winning route; confirm Level 1 remains unchanged,
Restart/Retry/Next Level/Play Again work, and all 101 Unity EditMode tests pass.

## M5-B1 gameplay layout and HUD polish

No additional Editor setup is required for the accepted M5-A scene. Let Unity
compile and enter Play Mode. BoardView builds the HUD with the existing saved cat
sprite references. Rounded UI panels are generated once in memory, reused through
9-sliced Images, and released when the view is destroyed; no artwork/import changes
or new asset assignments are required.

The screen uses a mint background (#E7F1EC), cream panels (#FFF8EE), pastel blue
accents (#BBD3DC), a warm Restart button (#E8AD67), and slate text (#425B6D).
The compact header separates LEVEL 01/02 and the authored title from a prominent
MOVES panel. Collection cards reuse the corresponding cat sprites on contrasting
icon backgrounds; the paw card uses a temporary PAW text badge. Requirements and
counts come from ObjectiveSnapshot, including wave-by-wave progress and mixed QA
objectives. Cards are pooled across Retry and level transitions.

Layout uses the device safe-area rectangle, scales HUD dimensions from its width
and available height, and reserves separate header/objective/footer regions before
sizing the square board. At the 900x1600 reference, the board is 830 units wide
(92.2% of safe width), with an 846-unit rounded surround. Height constrains the board
on shorter screens; taller screens retain width-based sizing and balanced spacing.
Goal cards wrap into rows for larger diagnostic configurations. All 49 cells still
use the same board rectangle and cell step for drawing, movement and hit testing.
The accepted resize behavior still cancels presentation and restarts the puzzle.

Normal gameplay hides technical READY/wave/cascade messages. Existing QA context
menus enable a diagnostic HUD with the complete status stream and unlimited-moves
indicator where applicable. Production errors show a short Restart prompt while
the existing Console retains details. The result panel, board cells, paw marks,
stripe bands, input gating and animation sequence remain as accepted.

Validation for this change: Core/Runtime player and Editor configurations plus
the Editor assembly compiled against installed Unity 6000.3.8f1 assemblies;
101/101 standalone NUnit tests passed. The actual shared layout calculation passed
60 geometry cases covering 9:16, tall portrait, small-device equivalents, shorter
portrait, safe-area insets and 0/1/2/3/6 objective cards. These check safe boundaries,
HUD separation, text-container widths and all cell-center hit coordinates; they
do not substitute for Unity text rendering or device visual QA.

Manual QA: inspect 9:16, 19.5:9/20:9 and a small portrait Game view with safe-area
insets. Check header/title and large move count, Orange/Gray icons and counts, paw
progress, unclipped text, board margins and Restart touch feedback. Replay both
winning routes; watch cascade/special progress, then Retry/Restart/Next Level/Play
Again. Exercise M2/M3/M4 diagnostics (technical status visible there, hidden after
returning to production), Restart during animation and tap/swipe hit alignment.
Confirm the existing result panels and all 101 Unity EditMode tests. Unity visual
QA and Unity Test Runner were not executed by this implementation session.

## M5-A cat sprite integration

After compilation, run **CatMatch3 > Setup > Apply M5-A Cat Sprites** once in
Edit Mode, then open Gameplay in 9:16 and enter Play Mode. This command binds
the five existing `Art/Sprites/Cats/Cat_<Type>.png` Single Sprites to serialized
fields on the existing BoardView and saves Gameplay through Unity Editor APIs.
It validates all five imports and existing scene wiring before changing anything,
preserves custom bindings, and is safe to rerun. Save or revert unsaved Gameplay
edits before applying it. No per-tile assignment or M4 level migration is needed.

All tile snapshots and refill/diagnostic visuals use the same explicit
Orange/Black/White/Gray/Siamese mapping. UI Images preserve aspect ratio, use
white tint and the complete sprite rectangle, and leave a 6% margin on each side.
Square cell roots/hitboxes, selection/match highlighting, fixed paw targets and
horizontal/vertical stripe indicators remain. Special cats use thin bright edge
bands: left/right for horizontal (row clear), top/bottom for vertical (column clear),
outside the cat artwork.
The bands have a subtle dark outline and clear corners, and follow the tile through
existing animations. This refinement requires no additional setup. Cat-name/coordinate
text is removed from tiles; coordinate
Hierarchy names, status information and development context-menu fixtures remain.
Missing bindings produce a setup error instead of silently displaying placeholders.
PNGs, importer metadata, Core, levels, animation timing and HUD layout are unchanged.

Manual QA: check all five cats and unclipped ears/whiskers; valid and invalid swaps
using tap/swipe; stripe creation/activation and paw clearing; gravity/refill/cascades;
Restart during animation, Retry, Next Level and Play Again. Run the existing M2/M3
and M4 context-menu fixtures and all 101 EditMode tests. Exit Play Mode, reopen
Gameplay, and confirm the five sprite references remain saved; rerun setup to
confirm it creates no duplicate content.

For edge-band QA, use BoardPrototype's **M3 QA** context menus in Play Mode:
**1a/1b** horizontal/vertical creation, **2a/2b** horizontal/vertical activation,
and **3** chain activation. Check both directions remain clear during selection,
swaps/rollback, clearing, falling, refill and cascades; normal cats have no bands.
Complete both production levels and test Restart/Retry/Next Level/Play Again,
then run all 101 Unity EditMode tests.

Unity **6000.3.8f1**, 2D Built-In, Android portrait. Two short authored puzzles now
have **exhaustively verified single winning routes**. Production no longer
reshuffles: unfinished objectives plus zero moves or a stable deadlock means
Fail. All ordinary valid swaps still resolve; there is no scripted wrong-move
filter, runtime search, replacement board or hidden refill adjustment.

## Unity setup and verification

1. Let Unity import/compile. Save or revert pending Gameplay/level-asset edits.
2. Run **CatMatch3 > Setup > Apply M4 Puzzle Revision** in Edit Mode.
3. Open **Assets/_CatMatch3/Scenes/Gameplay.unity alone**, choose **9:16**, enter
   Play Mode. Level 1 should show **3 moves, Orange 0/6, Gray 0/9**.
4. Replay the ordinary routes below. Next Level loads Level 2 with **4 moves and
   8 paws**. Win → Game Complete → Play Again returns to Level 1.
5. Exit Play Mode; run **Window > General > Test Runner > EditMode**, the full suite.
6. Run **CatMatch3 > Diagnostics > Verify Authored Puzzle Assets** to reproduce
   proof logs against the actual serialized asset data. Rerun puzzle setup to
   verify idempotency and preserved references/build entries.

The existing Level1.asset and Level2.asset were inspected: both match the known
previous M4 generated baseline, and Gameplay references their GUIDs correctly.
They have not been rewritten outside Unity. The revision menu migrates them
through supported Editor APIs, retaining GUIDs/references. Until it runs, their
persisted data still contains the previous 18/22-move content.

Setup preflights BOTH assets before editing either. Known previous M4 content is
upgraded; already-current puzzles are reused. Unexpected custom data, dirty assets,
wrong asset types, orphaned metadata and conflicting scene references fail clearly
without overwriting custom content. Both baseline and current configurations are
compared by actual tile identities, objectives, paws, moves, names and seed.
Missing assets are created by the existing M4 setup using the revised defaults.
Completed upgrades can safely remain after a later infrastructure failure; reruns
reuse them. Scene edits use the existing Undo/rollback/cleanup workflow.

The existing Gameplay camera, input, controller, HUD, panels and unrelated objects
are preserved. Gameplay remains the first enabled shared build scene; SampleScene
and other entries retain relative order. Scene-specific Editor creation/opening
needs no temporary/preview-scene activation. Finally blocks close only scenes
opened by setup and restore the previous valid loaded active scene. No scene YAML
is manually rewritten. A custom Build Profile overriding the shared scene list
still needs its own Gameplay startup selection.

## Production levels

The fixed opening layouts remain deliberately authored. Short move limits, revised
objectives, paw placement and seeds turn them into constrained puzzles. No authored
refill tape was needed: the existing xorshift32 stream provides reproducible results.

O/B/W/G/S mean Orange/Black/White/Gray/Siamese. Diagrams read **top row first**.
Assets store **bottom row first**, left to right: **index = y * 7 + x**. (0,0) is
bottom-left, X points right and Y points upward. Opening tiles are normal cats.

    Level 1             Level 2
    WGSOBGG             BWGSOBW
    OBWGGOB             SOBWGSO
    GSOBWGS             WGSOBGG
    BWGSOBW             GSOBWGS
    SOBWGSO             OBWGGOB
    WOSOBWG             WOSOBWG
    OBOGSOB             OBOGSOB

| Level | Objectives | Move limit | Seed |
| --- | --- | --- | --- |
| 1 — Collect the Cats | 6 Orange **and** 9 Gray | 3 | 1339 |
| 2 — Clear the Paw Prints | All 8 fixed paws | 4 | 141 |

Level 2 paws: **(0,0), (1,0), (0,1), (1,1), (0,2), (1,2), (3,3), (3,4)**.
They deliberately form two lower-column groups plus two preparation targets;
this makes the eventual vertical stripe useful. Eight paws were retained.

### Exact winning routes — development QA only

Wait for Ready after each pair. The pair can be tapped/swiped in either direction.
Do not insert other valid moves. Invalid swaps do not affect the route.

| Move | Level 1 | Level 2 |
| --- | --- | --- |
| 1 | (2,0) ↔ (2,1) | (0,1) ↔ (1,1) |
| 2 | (4,5) ↔ (4,6) | (1,3) ↔ (1,4) |
| 3 | (4,5) ↔ (4,6), again after refill | (2,3) ↔ (2,4) |
| 4 | — | (0,3) ↔ (1,3) |

Level 1 logic: first collect the visible Orange triple in row 1. The next move
makes a four-Gray run across the top row, retaining a horizontal stripe at (3,6)
and collecting three Gray cats. Reusing the coordinate pair after refill forms
an Orange triple, drops that stripe and activates it through a Gray cascade.
The final three-wave turn finishes at **7 Orange / 9 Gray**, with zero moves.
Intermediate totals are **3/0 → 3/3 → 7/9**. The HUD caps Orange display at 6.

Level 2 logic: clear the three left-column paws, with the cascade creating a
vertical Black stripe at (0,3). The second move clears a Siamese row and prepares
the board; it gives no paw credit. The third creates an Orange stripe and removes
the (3,4) paw. The last move carries the Black stripe into a Black match in row 3,
clearing column 1 and the remaining row-3 mark. Paw totals are **3 → 3 → 4 → 8**.
The untouched Orange stripe survives. This uses existing special mechanics.

The winning routes are not stored as runtime restrictions or displayed in player
UI. Every legal off-route move is accepted, spends one move and resolves normally.

## Verification evidence and method

| Level | Status | Winning routes | States | Transitions | Replayed moves | Exhaustive |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Unique | 1 | 333 | 332 | 931 | Yes |
| 2 | Unique | 1 | 2,420 | 2,419 | 9,294 | Yes |

Both searches produced **zero resolution errors**, reached the full move limit
without pruning unexplored paths, and exhausted every legal continuation. Typical
standalone times were about **46 ms / 533 ms** on this machine; timing varies.
These are complete route counts, not merely known or shortest solutions.

PuzzleVerifier is compiled only for **UNITY_EDITOR or CATMATCH3_OFFLINE**. It is
absent from the player Core assembly. It enumerates each adjacent right/up pair
once and uses actual GameplaySession/BoardResolver rules for every candidate.
Routes stop immediately at Win/Fail. Invalid swaps are omitted.

Each search branch is represented losslessly by the immutable level/initial seed
and its full successful-swap history. A fresh session replays that history before
the next move. This reconstructs the complete current board, stripe orientations,
objective counts, remaining paws, moves, RNG state and terminal state, without
sharing mutable board/tracker/RNG instances. There is no approximate resolver,
state hashing, memoization or merging of paths; distinct histories remain distinct
even if they converge to the same logical state. Replay costs are explicitly bounded.

Default per-verification limits: **50,000 transitions**, **500,000 replayed moves**,
**10 seconds**, and a **16-move depth cap**, additionally bounded by session moves.
The 3/4-move production puzzles finish well inside these limits. Outcomes:

- **Unsolvable:** exhaustive, zero wins.
- **Unique:** exhaustive, exactly one win, no unresolved/error branches.
- **Multiple:** at least two distinct winning routes found; search stops at two,
  so the count is a lower bound and Exhaustive is false.
- **Inconclusive:** work/time/depth budget or resolution failure prevents proof.
  One discovered winning route is never promoted to Unique in this case.

NUnit includes small zero/one/multiple fixtures, an incomplete search that has
already found one route, replay/depth budget checks, unordered-pair enumeration,
branch isolation, reversal equivalence and full production uniqueness proofs.
The asset-verification menu uses the same utility on LevelDefinition.ToConfiguration.
Standalone Core compilation can enable CATMATCH3_OFFLINE to call the same verifier.
No solver, route checking or uniqueness verification runs during player gameplay.

### Undirected swap identity

The old resolver preferred the supplied swap destination for special creation;
reversing a gesture could therefore produce a different anchor. Puzzle sessions
normalize this choice to the endpoint with the lower **y * 7 + x** index. Thus
A↔B is one decision with identical outcomes in either direction, including future
refills. All normal validity, creation, activation, gravity and cascade rules
stay in the existing engine. Legacy M2/M3 diagnostics retain their former
supplied-destination behavior. This is tested using actual surviving stripes.

## Gameplay, failure and presentation

BoardResolver has an explicit ReshufflePolicy. **GameplaySession always passes
Disabled**. Legacy diagnostics explicitly pass Automatic; existing shuffler tests
continue exercising Automatic. There is one resolver and one production controller.

After a valid swap the session spends exactly one move, resolves every wave,
then evaluates **resolution error → objective Win → OutOfMoves → NoLegalMoves**.
Win takes precedence when objectives complete at zero moves and/or a deadlock.
NoLegalMoves never draws shuffle randomness, changes progress, restores moves or
replaces the board. A stable no-move resolution itself is successful; the session
then supplies the loss reason. Fail blocks further input and offers Retry.

BoardPrototype presents all immutable wave snapshots, special effects, clear,
gravity, refill and cascades before showing the result panel. NoLegalMoves uses
“No more matches available. Try again!”; move exhaustion uses “Out of Moves”.
Production never enters the reshuffle animation. The existing HUD and result-panel
infrastructure are reused, with no final-art/UI overhaul.

Normal production loading requires a fixed asset seed. Restart/Retry restores
that exact seed and authored opening, full moves, original paws/targets, zero
progress and Ready. No refill cursor/tape or fallback is added. Core retains its
previous optional fallback-seed constructor for diagnostics/regression fixtures;
production refuses unseeded authoring. SeededRandom's implementation is unchanged.

Restart first cancels all controller coroutines, then restores model and snapshots,
selection, highlights, transforms, pointer capture and HUD. Nested view iterators
have no independent delayed callbacks. Level transition and re-enable use the
same cancellation behavior. Game-view resize/safe-area changes still intentionally
restart the current production level to preserve its gesture coordinate basis.

Flow stays Level 1 Win → Next Level → Level 2 Win → Game Complete → Play Again →
Level 1. Retry restarts the current production level. Tutorial remains nonblocking,
Level 1 only, dismissed by the first valid production swap. Once dismissed it does
not repeat during Restart/Retry/Play Again in the same Play Mode session.

## Exact Unity QA

1. **Normal completion:** run the revision setup, play the routes above, check
   per-turn progress and Win at zero moves. Next Level gives fresh 4-move Level 2;
   Game Complete/Play Again gives fresh 3-move Level 1.
2. **Invalid swap:** fresh Level 1, (3,0) ↔ (4,0) rolls back; moves remain 3.
3. **Incorrect valid move:** fresh Level 1, (1,0) ↔ (1,1) resolves normally and
   leaves 2 moves, Orange 3 / Gray 0. Continue (5,4) ↔ (5,5), then (1,5) ↔ (1,6).
   Each is valid. Finish Orange 3 / Gray 6, zero moves, Fail → Retry. Exhaustive
   verification also rules out every other winning continuation of that first move.
4. **NoLegalMoves:** on BoardPrototype's Play Mode context menu, select
   **M4 Puzzle QA / No Legal Moves - genuine swap**. A seed-300 transient fixture
   automatically swaps (2,5) ↔ (2,6), makes a four-run, preserves an Orange stripe
   and refills normally. Expect Orange 3/99, **2 moves left**, a stable deadlock,
   no fade/shuffle/replacement and Fail with the no-matches explanation. Retry
   restores the selected production puzzle.
5. **Last-move cascade Win:** **M4 QA / Final move cascade Win** still uses a
   one-move transient fixture with seed 141 and target Orange 6. Expect 3 waves,
   Orange 3 after the first two, then 6 and Win at zero moves. Also verify the real
   Level 1 final move's three waves before its panel appears.
6. **Restart/input:** press Restart during exchange, stripe creation/activation,
   clear, fall, refill and cascades. Expect the identical production opening and
   seed; subsequent winning route must replay unchanged. Rapid taps/drags and
   extra pointers during resolution or terminal states must not alter board/moves.
7. **Legacy diagnostics:** M2 cascade, M2 deadlock reshuffle (attempt 5), and all
   M3 creation/activation/chain/objective fixtures remain. They show DIAGNOSTIC
   with unlimited moves. **Diagnostics / Reset current M2-M3 fixture** resets that
   fixture; visible Restart returns to production. M4 QA Win/Fail commands still
   exercise Next Level/Play Again/Retry through transient goals. Transitions clear
   diagnostics and load real asset data. All context menus are excluded from players.
8. **Setup/UI:** rerun revision setup in Edit Mode, confirm no duplicates and
   preserved asset GUIDs/scene references. Verify portrait readability, safe areas,
   mouse/touch controls, result button bindings and the complete EditMode suite.

## Static validation and limits

**101/101 standalone Mono/NUnit tests passed.** All 88 previous cases remain;
authored-content expectations/routes were updated to the new approved design,
and legacy reshuffle assertions now explicitly select Automatic. Thirteen focused
puzzle cases were added. Matching, swap validation, gravity/refill, special chains,
objective semantics, last-move priority and the infrastructure smoke test pass.

Core (Editor/offline and player), Runtime (Editor and player), and Editor compile
against the installed Unity 6000.3.8f1 assemblies. Only CS0649 warnings for fields
assigned by Unity serialization remain. Core stays free of engine references;
no assembly structure/package/render-pipeline/input/project-setting changes were
introduced by this revision. Existing user-generated Gameplay, build settings
and level assets were inspected and preserved on disk pending the Editor migration.

Static review covers verifier budgets/counting/replay, production policy, terminal
ordering, input gating, cancellation, asset conversion/migration/idempotency,
metadata/whitespace, scene references, startup build order and Git scope.

Unity import, menu execution, asset migration, Test Runner, rendering, coroutine
cancellation inside Unity and Android touch have **not** been executed here.
Uniqueness proves the logical puzzle under the documented fixed rules/seeds, not
human readability or fairness. Refill is deterministic but upcoming cats are not
previewed; whether the short routes feel sufficiently discoverable still needs
manual playtesting. Limits/objectives/seed changes require fresh exhaustive proof.

No M5 art, extra levels/mechanics, runtime hints, batchmode, commit, push, tag or
publish. Await the user's Unity QA and explicit acceptance.
