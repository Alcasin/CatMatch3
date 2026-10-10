# CatMatch3 — M2 resolution prototype

Unity **6000.3.8f1**, 2D Built-In Render Pipeline. Intended target: Android, portrait (9:16).
M0/M1 foundation, board and controls are preserved. M2 replaces match preview with
clear, gravity, refill, cascades and deadlock reshuffling. Special tiles, objectives,
move limits and win/fail flow are outside this milestone.

## Unity verification

1. Open the project normally and let Unity finish importing and compiling.
2. Run **CatMatch3 > Setup > Apply M2 Resolution** in Edit Mode. Save any pending
   Gameplay edits first; setup refuses to silently save unrelated unsaved work.
3. Open `Assets/_CatMatch3/Scenes/Gameplay.unity` on its own (double-click the
   scene in the Project window). Select a **9:16** aspect in the Game view and
   enter Play Mode. Expect 49 colored, labeled tiles, instructions and Reset Board.
4. Exit Play Mode. Open **Window > General > Test Runner**, select **EditMode**,
   and run the complete suite, including the original M0 smoke test.
5. Rerun M2 setup to check that the board root and EventSystem are not duplicated.

Unity import/compilation, menu execution, rendering and device input require Editor
verification. A standalone Mono/NUnit run can validate Core tests without launching
Unity. No headless/batchmode Unity workflow is used.

M2 validation: **35 tests passed in standalone Mono/NUnit**, including all 19
original tests. Core, Runtime (with and without Editor diagnostics) and Editor
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
  Test Reset during clear, fall, refill and shuffle. Resizing the Game view resets the prototype
  to avoid changing the gesture/animation coordinate basis mid-move.

For reproducible Editor-only diagnostics, select **M1 Board Prototype** in the
Hierarchy during Play Mode. On its **BoardPrototype** component, open the context
menu (three dots/right-click its header):

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
  `IRandomSource`/`SeededRandom` and `BoardShuffler`. The M0 marker is retained.
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
snapshots, unique match union, survivor movements and new tile spawns. Runtime
keeps input locked while it animates those records in order, then displays the
final snapshot. Fixed visual slots are rebound at each stage boundary after their
tiles reach the correct positions. The model may already hold the final state
while the view plays the intermediate states; input cannot act on it then.

Empty cells are tracked separately from the five cat types. `TryGetCell` returns
false for an empty cell; `GetCell` throws there. `Capture` supports empty cells;
`CopyCells` requires a full board. Empty cells break match runs. Clearing removes
the complete detected union once, gravity preserves bottom-to-top survivor order,
and refill visits columns left-to-right and vacant cells bottom-to-top. Refill
matches are allowed and become the next cascade wave.

`SeededRandom` uses a fixed xorshift32 algorithm with bounded integer draws;
seed 0 maps to `0x6d2b79f5`. Reset starts a fresh seed-1337 sequence. Tests can
substitute scripted draws, including a deliberately constructed two-wave cascade.
Invalid swaps do not call resolution or consume randomness.

After stability, legal-move detection checks right/up neighbor pairs using the
same endpoint-match and unequal-type rules as swapping. Each temporary exchange
is restored in `finally`. Deadlocks trigger at most **256** Fisher–Yates attempts
using the same random source and original tile multiset. Only a match-free,
playable candidate is committed. An obvious impossible count (more than 35 cats
of one type) fails immediately. Exhaustion leaves the board unchanged and reports
failure; there is no tile-replacement fallback or constraint solver.

At **100 waves**, remaining matches produce an explicit failed resolution with
the unresolved snapshot. Runtime enters **Error**, logs the reason and offers
Reset; it never presents that board as Ready. Faulty random sources or animation
exceptions also enter Error. These bounded safeguards favor a recoverable failure
over silently changing the rules. A bounded shuffle is not a proof that every
possible multiset can be rearranged.

## Setup behavior and safety

**Apply M2 Resolution** delegates to the existing **Apply M1 Board** command.
M2 requires no additional serialized configuration: the existing BoardPrototype
and BoardView supply the new behavior and create their transient visuals in Play
Mode. The scene itself has not been rewritten for M2.

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

Local Git is initialized without a commit or remote. `.gitignore` excludes Unity
caches, builds and generated IDE files while keeping assets and `.meta` files.
`.gitattributes` normalizes source/YAML text and identifies common binary assets;
Git LFS and a custom merge driver are not required.

Stop at M2 until the prototype is verified in Unity and the milestone is accepted.
