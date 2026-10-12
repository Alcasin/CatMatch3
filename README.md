# CatMatch3 — M4 deterministic puzzle revision

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
Square cell roots/hitboxes, selection/match highlighting, cyan paw frames and
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
