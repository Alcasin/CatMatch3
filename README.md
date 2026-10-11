# CatMatch3 — M3 specials and objectives prototype

Unity **6000.3.8f1**, 2D Built-In Render Pipeline. Intended target: Android, portrait (9:16).
M0–M2 foundation, board, controls and resolution are preserved. M3 adds horizontal
and vertical Striped Cats, chained clearing, normal-cat collection and fixed paw
cell objectives. Final levels, move limits, Win/Fail and progression remain outside
this milestone.

## Unity verification

1. Open the project normally and let Unity finish importing and compiling.
2. Run **CatMatch3 > Setup > Apply M3 Specials & Objectives** in Edit Mode. Save any pending
   Gameplay edits first; setup refuses to silently save unrelated unsaved work.
3. Open `Assets/_CatMatch3/Scenes/Gameplay.unity` on its own (double-click the
   scene in the Project window). Select a **9:16** aspect in the Game view and
   enter Play Mode. Expect 49 colored, labeled tiles, instructions and Reset Board.
4. Exit Play Mode. Open **Window > General > Test Runner**, select **EditMode**,
   and run the complete suite, including the original M0 smoke test.
5. Rerun M3 setup to check that the board root and EventSystem are not duplicated.

Unity import/compilation, menu execution, rendering and device input require Editor
verification. A standalone Mono/NUnit run can validate Core tests without launching
Unity. No headless/batchmode Unity workflow is used.

M3 validation: **55 tests passed in standalone Mono/NUnit**, including all 35
unchanged M0–M2 tests. Core, Runtime (with and without Editor diagnostics) and Editor
sources compile against the installed Unity 6000.3.8f1 assemblies. This does not
confirm Unity Test Runner, menu execution, visual behavior or Android touch.

## Trying the prototype

Coordinates printed on tiles use `(x,y)`, with `(0,0)` at the bottom-left.

- Tap a tile, then tap an orthogonal neighbor. Tap the selected tile again to
  deselect; tap a nonneighbor to select that tile instead.
- Alternatively, press a tile, drag clearly onto an orthogonal neighbor, and
  release. Diagonal/ambiguous gestures, releases outside the board and jumps over
  more than one cell are ignored. One pointer is captured at a time.
- Valid example: swap `(1,0)` with `(1,1)` to form three Orange cats in the bottom
  row. Another valid example after reset: `(4,5)` with `(5,5)` forms a Gray column.
- Invalid example from the initial layout: `(3,0)` with `(4,0)`. The tiles exchange
  visually and return, with the logical board unchanged.
- Swaps are blocked in **Swapping**, **Resolving** and **Reshuffling**. Accepted
  swaps highlight matches in mint, clear all groups together, compact columns,
  refill from above and repeat for cascades. **Ready** returns only after a full,
  match-free board with legal moves is verified. Try several consecutive swaps.
- Status reports the current wave and the previous successful swap's total waves
  and cascades (waves minus the initial match wave). A deadlock briefly fades and
  shrinks the board, then reveals a verified reshuffle.
- Reset restores the exact initial board, **seed 1337**, highlights and animation
  transforms/visibility. It also cancels a running animation or recovers **Error**.
  After selecting an M3 QA scenario, Reset restores that scenario's starting tiles
  (including its initial stripes), original objectives and all eight paw marks.
  Use **M3 QA > Return to original board** to restore the M1 layout without stripes.
  Test Reset during clear, fall, refill and shuffle. Resizing the Game view resets the prototype
  to avoid changing the gesture/animation coordinate basis mid-move.

For reproducible Editor-only diagnostics, select **M1 Board Prototype** in the
Hierarchy during Play Mode. On its **BoardPrototype** component, open the context
menu (three dots/right-click its header). Each numbered M3 command loads a stable fixture
and automatically swaps after 0.6 seconds, using seed 1337. Only Ready accepts QA
commands; Reset remains usable throughout the animation.

| Context menu command | Expected result |
| --- | --- |
| **M3 QA > 1a - Horizontal creation** | `(1,1)` → `(1,0)` creates an Orange horizontal four-run. Three tiles clear; a horizontal stripe pulses and stays at `(1,0)`. |
| **M3 QA > 1b - Vertical creation** | `(1,1)` → `(0,1)` creates a vertical four-run. The new vertical stripe appears at `(0,1)`, then falls to `(0,0)` and survives. |
| **M3 QA > 2a - Horizontal activation** | An existing Orange stripe participates in the match and clears all seven cells of row 0. |
| **M3 QA > 2b - Vertical activation** | The transposed fixture clears all seven cells of column 0. |
| **M3 QA > 3 - Chain activation** | Stripes at `(0,0)` horizontal, `(4,0)` vertical and `(4,4)` horizontal activate in order. Their union removes 19 tiles once. Three cascade waves follow (four total waves), then Ready. |
| **M3 QA > 4 - Objective progress** | Horizontal activation collects **3 Orange and 1 Gray**, clears **7/8 paws**, and leaves the fixed mark at `(1,1)` while tiles fall. |
| **M3 QA > Return to original board** | Restores the ordinary M1 layout, zero objective progress and seed 1337. |

Stripes use a contrasting horizontal or vertical band over the original cat
color. Cyan frames behind tiles identify fixed paw cells. The development display
targets **12 Orange**, **10 Gray**, and **8 paws** (bottom row plus `(1,1)`). It updates
after each visible clear. Creating a stripe does not collect its preserved cat or
clear its paw: in horizontal creation, expect **3 Orange, 0 Gray and 3/8 paws**,
with the `(1,0)` paw still present. Consumed existing stripes give paw credit but
no normal-cat collection credit. Fulfilled objectives display a message without
ending gameplay. Try another swap after each demo and reset during creation,
activation, chained effects, fall and refill. Rapid taps/drags must be ignored
until the complete sequence finishes.

The existing M2 diagnostics remain available:

- **M2 QA > Run cascade demo** resets the original layout, uses seed **141** and
  swaps `(1,0)` / `(1,1)`: exactly **three waves, two cascades**. Observe each clear,
  fall and refill; rapid taps/drags during these stages must be ignored. After Ready,
  play another move normally. Reset switches back to seed 1337.
- **M2 QA > Run deadlock reshuffle demo** shows a fixed match-free board with no
  legal moves for 0.6 seconds, then reshuffles with seed 1337. The deterministic
  search accepts attempt **5**. Input stays blocked until the new board appears.
  Tile counts are preserved (also checked in Core tests).

Diagnostics only run while Ready, are omitted from player builds, and add no
production UI or serialized scene objects. Exit Play Mode and run the EditMode
suite after gameplay checks. Open Gameplay alone to use its existing EventSystem.

Mouse and Android touch go through one uGUI EventSystem with the already installed
Input System. No legacy mouse polling or separate simulated-touch path is used.

## Layout and dependencies

All custom assets live in `Assets/_CatMatch3/`:

- `Scenes/`: Gameplay, created through Editor APIs by the setup command.
- `Scripts/Core/`: `CatMatch3.Core`, with No Engine References enabled. Fixed 7x7
  `Board`, `BoardPosition`, five `CatType` values, `MatchFinder`, immutable match
  groups/results, swap results and `PrototypeLayout`. M2 adds explicit occupancy,
  immutable `BoardSnapshot`, movement/spawn records, `BoardResolver` wave results,
  `IRandomSource`/`SeededRandom` and `BoardShuffler`. M3 adds immutable `Tile`
  identity, special effect records, `ObjectiveTracker`/`ObjectiveSnapshot`, and
  explicit `M3PrototypeLayout` QA fixtures. The M0 marker is retained.
- `Scripts/Runtime/`: `CatMatch3.Runtime` references Core and Unity UI.
  `BoardPrototype` owns interaction states and the single Core swap call;
  `BoardView` generates/resizes the temporary UI and animates it;
  `BoardTileView` displays a cell and relays pointer events without knowing rules.
- `Editor/`: Editor-only `CatMatch3.Editor` references Runtime, Unity UI and the
  existing Input System to create scene objects and wire persistent UI actions.
- `Tests/EditMode/`: Editor-only `CatMatch3.Tests.EditMode` references Core;
  `TestAssemblies` supplies NUnit and Unity test-runner references.
- `Art/`, `Audio/`, `Prefabs/`, `Data/`: reserved asset folders, tracked via metadata.

Core has no Unity or presentation dependencies. Runtime depends on Core, never
the reverse. Editor and test assemblies are excluded from player builds.
Match detection scans maximal horizontal/vertical runs, retains orientation,
type and length, and also returns a unique coordinate union. Invalid swaps roll
back; accepted swaps must produce a match involving a swapped endpoint. Same-cat
swaps cannot legalize existing matches. Layout inputs and board snapshots are copied.
Unity Test Framework 1.6.0 and Input System 1.18.0 were already in the template;
the package manifest and lockfile are preserved without additions or upgrades.

## Resolution and deterministic randomness

Core resolves synchronously and records every wave's before/clear/gravity/refill
snapshots, unique removals with original tile identities, ordered special
activations, creations, objective progress, survivor movements and new tile spawns. Runtime
keeps input locked while it animates those records in order, then displays the
final snapshot. Fixed visual slots are rebound at each stage boundary after their
tiles reach the correct positions. The model may already hold the final state
while the view plays the intermediate states; input cannot act on it then.

`Board` owns one `Tile[,]` identity array plus occupancy flags. Every tile retains
one of the five cat types and a stripe orientation (None, Horizontal, Vertical).
`GetCell`/`TryGetCell`/`CopyCells` remain color-only compatibility APIs; use
`GetTile`/`TryGetTile`/`CopyTiles` to preserve specials when copying a board.
Snapshots and movement records retain full identities. Model state is created in
Play Mode, not serialized in Gameplay; the existing scene's only controller
reference remains its BoardView. No scene serialization migration is needed.

Empty cells are tracked separately from the five cat types. `TryGetCell` returns
false for an empty cell; `GetCell` throws there. `Capture` supports empty cells;
`CopyCells` requires a full board. Empty cells break match runs. Clearing removes
the complete clear union once except preserved creation anchors, gravity preserves bottom-to-top survivor order,
and refill visits columns left-to-right and vacant cells bottom-to-top. Refill
matches are allowed and become the next cascade wave.

`SeededRandom` uses a fixed xorshift32 algorithm with bounded integer draws;
seed 0 maps to `0x6d2b79f5`. Reset starts a fresh seed-1337 sequence. Tests can
substitute scripted draws, including a deliberately constructed two-wave cascade.
Invalid swaps do not call resolution or consume randomness.

After stability, legal-move detection checks right/up neighbor pairs using the
same endpoint-match and unequal-type rules as swapping. Each temporary exchange
is restored in `finally`. Deadlocks trigger at most **256** Fisher–Yates attempts
using the same random source and original full tile multiset, including stripes,
orientations and cat types. Paw state and objective counts are untouched. Only a match-free,
playable candidate is committed. An obvious impossible count (more than 35 cats
of one type) fails immediately. Exhaustion leaves the board unchanged and reports
failure; there is no tile-replacement fallback or constraint solver.

At **100 waves**, remaining matches produce an explicit failed resolution with
the unresolved snapshot. Runtime enters **Error**, logs the reason and offers
Reset; it never presents that board as Ready. Faulty random sources or animation
exceptions also enter Error. These bounded safeguards favor a recoverable failure
over silently changing the rules. A bounded shuffle is not a proof that every
possible multiset can be rearranged.

## Special and objective rules

Matching uses CatType regardless of stripe orientation. Swapping a stripe still
requires the normal endpoint-match rules; identical-color swaps remain invalid,
even between two different stripes. There is no tap activation or special-pair
bonus.

Each maximal straight run of **4 or more** can create **one** stripe of the run's
orientation and CatType. A run containing an existing stripe activates it and
creates no replacement from that run. New stripes are excluded from the activation
queue of their creation wave, but can activate through later cascade matches.

Creation placement and overlap rules:

1. On the first wave after an accepted swap, prefer the second swap coordinate
   (destination) if it belongs to the qualifying run.
2. Otherwise, including every cascade wave, use the leftmost horizontal or
   bottommost vertical member.
3. Process horizontal groups bottom-to-top, then vertical groups left-to-right,
   following MatchFinder order. If two proposals share an anchor, the first wins;
   distinct anchors may both survive an overlap.
4. Any existing stripe effect hitting the proposed anchor cancels that creation;
   no alternative anchor is searched. Effects take priority. Ordinary overlapping
   match membership alone does not remove an accepted creation anchor.
5. Preserve accepted anchors as tiles, transform them into stripes, and let them
   fall normally with the other survivors.

Existing stripes in the complete match union are queued in deterministic
first-detected order. Expand the queue breadth-first, visiting row hits
left-to-right and column hits bottom-to-top. Each existing special is queued
once, so cycles and intersections terminate after at most 49 activations. The
union of ordinary matches and effects is removed once, after creation conflicts
are decided. Runtime displays matched tiles, each activation/chain sweep, the
simultaneous clear and objective update, creation pulses, fall and refill in order.

`ObjectiveTracker` copies ordinary target/mark data and persists across swaps.
It receives the wave's actual unique removals. Only removed **normal** cats count
toward collection; counts are actual totals and may exceed targets. Existing
specials, preserved creation anchors, swapping, falling and refill give no cat
credit. Any removed tile, including a consumed special, clears its cell's paw
once. Remaining marks belong to fixed coordinates, never movable Tile identity.
Read-only progress snapshots expose each target, collected count, remaining marks,
paw totals and fulfillment. Old snapshots remain immutable after further swaps or
Reset. No score, move counter or completion transition is implemented.

## Setup behavior and safety

**Apply M3 Specials & Objectives** and **Apply M2 Resolution** delegate to the
existing **Apply M1 Board** command. M3 requires no additional serialized configuration: the existing BoardPrototype
and BoardView supply the new behavior and create their transient visuals in Play
Mode. The scene itself has not been rewritten for M3.

**Apply M1 Board** opens Gameplay additively only if needed and creates one scene
root with Canvas, CanvasScaler, GraphicRaycaster, BoardView and BoardPrototype.
The controller's serialized view reference is assigned automatically. An owned
EventSystem is created only when Gameplay has none, using persistent action
references from the existing `Assets/InputSystem_Actions.inputactions` asset.
An existing compatible user EventSystem is reused without rewriting its wiring.
The 49 tile visuals, texts and reset button are created in Play Mode; no sprites,
prefabs or placeholder assets need to be imported.

Reruns reuse the BoardPrototype root and repair missing generated components or
references. Conflicting roots, multiple boards/EventSystems, incompatible UI
configuration and dirty loaded Gameplay scenes fail clearly. The command groups
its edits for Undo and rolls them back on failure. It saves Gameplay, closes only
scenes it opened, and restores the previously active scene if needed. It never
requires activation of the scene being edited. Cameras, unrelated objects, build
entries, project-wide input settings and package dependencies are preserved.

**Apply M0 Foundation** remains available and unchanged:

The command creates Gameplay only if the destination is unused. The new scene
contains one tagged orthographic Main Camera at `(0, 0, -10)`, size 5, with a solid
background and an AudioListener. It marks and saves the new scene, then closes
only that temporary scene and restores the previously active scene. No user
scene is closed or resaved. An existing Gameplay scene is retained exactly;
rerunning setup deliberately does not repair or reset later user edits.
Conflicting/unimported files and orphaned metadata produce Console errors.

Gameplay is appended to the shared build scene list if missing, or its existing
entry is enabled. Other entries and their order are preserved, including
SampleScene. Therefore SampleScene remains the first build scene in this fresh
project; Gameplay is not silently promoted to the startup scene. A future custom
Build Profile that overrides the shared scene list must include Gameplay itself.

Setup sets the supported mobile default orientation to Portrait (also used by
Android) and saves assets/settings. It does not switch platforms, install Android
modules, force camera aspect ratios or change the render pipeline. The 9:16 Game
view setting is an Editor preview choice, not a restriction on device resolution.
Setup runs only from the menu, never automatically on script import.

## Git

The repository's existing Git configuration is preserved. M3 work is left
uncommitted; no commit or push is performed. `.gitignore` excludes Unity
caches, builds and generated IDE files while keeping assets and `.meta` files.
`.gitattributes` normalizes source/YAML text and identifies common binary assets;
Git LFS and a custom merge driver are not required.

Stop at M3 until the prototype is verified in Unity and the milestone is accepted.
