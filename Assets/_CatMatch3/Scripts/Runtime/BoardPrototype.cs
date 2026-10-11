using System;
using System.Collections;
using System.Collections.Generic;
using CatMatch3.Core;
using UnityEngine;

namespace CatMatch3.Runtime
{
    public enum BoardInteractionState { Ready, Swapping, Resolving, Reshuffling, Terminal, Error }

    /// <summary>Input and ordered presentation of Core resolution snapshots.</summary>
    [DisallowMultipleComponent]
    public sealed class BoardPrototype : MonoBehaviour
    {
        [SerializeField] private BoardView view;
        [SerializeField] private LevelDefinition level1;
        [SerializeField] private LevelDefinition level2;
        private GameplaySession session;
        private Board diagnosticBoard;
        private Board board => session != null ? session.Board : diagnosticBoard;
        private int currentLevel;
        private uint levelSeed;
        private bool started;
        private bool tutorialDismissed;
        private bool diagnostic;
        public const uint ResetSeed = 1337;
        private IRandomSource random;
        private ObjectiveTracker objectives;
        private M3QaScenario initialScenario = M3QaScenario.Original;
        private BoardPosition? selected;
        private bool pointerHeld;
        private int pointerId;
        private BoardPosition pressedCell;
        private Vector2 pressLocal;
        private float maxTravel;

        public BoardInteractionState State { get; private set; }
        public int LastWaveCount { get; private set; }
        public int LastCascadeCount => Math.Max(0, LastWaveCount - 1);

        private void Start()
        {
            if (view == null)
            {
                State = BoardInteractionState.Error;
                Debug.LogError("CatMatch3: BoardView reference is missing. Rerun Apply M4 Two-Level Gameplay.", this);
                enabled = false;
                return;
            }
            view.Build(this);
            started = true;
            LoadLevel(0);
        }

        public void ResetBoard()
        {
            if (!started) return;
            // The player Restart always leaves diagnostics and restores the selected production level.
            if (session != null && !diagnostic)
            {
                CancelPresentation();
                session.Restart();
                ShowFreshBoard();
            }
            else LoadLevel(currentLevel, false);
        }

        private void CancelPresentation()
        {
            StopAllCoroutines();
            pointerHeld = false;
            selected = null;
            view.HideResult();
            view.SetSelection(null);
        }

        private void LoadLevel(int index, bool chooseSeed = true)
        {
            CancelPresentation();
            diagnostic = false;
            initialScenario = M3QaScenario.Original;
            session = null;
            diagnosticBoard = null;
            objectives = null;
            try
            {
                if (level1 == null || level2 == null || level1 == level2)
                    throw new InvalidOperationException("Two distinct level assets are required. Run Apply M4 Two-Level Gameplay.");
                var first = level1.ToConfiguration();
                var second = level2.ToConfiguration();
                if (first.Identifier == second.Identifier) throw new InvalidOperationException("Level identifiers must be distinct.");
                var configuration = index == 0 ? first : second;
                currentLevel = index;
                if (!configuration.DevelopmentSeed.HasValue)
                    throw new InvalidOperationException("Production puzzles require a fixed development seed. Run Apply M4 Puzzle Revision.");
                if (chooseSeed) levelSeed = configuration.DevelopmentSeed.Value;
                session = new GameplaySession(configuration, levelSeed);
                diagnosticBoard = null;
                ShowFreshBoard();
            }
            catch (Exception exception) { EnterError(exception.Message); }
        }

        private ObjectiveSnapshot Progress => session != null ? session.Progress : objectives?.Capture();

        private void ShowFreshBoard()
        {
            LastWaveCount = 0;
            State = BoardInteractionState.Ready;
            view.ShowBoard(board);
            view.ShowObjectives(Progress);
            RefreshHud();
            ReadyStatus("Choose a tile");
        }

        private void RefreshHud()
        {
            view.ShowLevel(diagnostic ? "DIAGNOSTIC — " + (session != null ? session.Level.DisplayName : initialScenario.ToString()) :
                $"Level {currentLevel + 1}: {session.Level.DisplayName}", session?.RemainingMoves,
                !diagnostic && currentLevel == 0 && !tutorialDismissed);
        }

        public void ResultAction()
        {
            if (State != BoardInteractionState.Terminal || session == null) return;
            if (session.State == GameplayState.Fail) ResetBoard();
            else if (session.State == GameplayState.Win) LoadLevel(currentLevel == 0 ? 1 : 0);
        }

        private void ResetDiagnostic()
        {
            CancelPresentation();
            session = null;
            diagnostic = true;
            diagnosticBoard = M3PrototypeLayout.CreateBoard(initialScenario);
            objectives = M3PrototypeLayout.CreateObjectives();
            random = new SeededRandom(ResetSeed);
            ShowFreshBoard();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            pointerHeld = false;
        }

        private void OnEnable() { if (started) ResetBoard(); }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            pointerHeld = false;
            if (State == BoardInteractionState.Ready && board != null) view.SetSelection(selected);
        }

        public void BeginPointer(BoardPosition cell, int id, Vector2 screenPosition)
        {
            if (State != BoardInteractionState.Ready || pointerHeld || board == null || !Board.IsValidPosition(cell)) return;
            if (!view.TryLocalPoint(screenPosition, out pressLocal)) return;
            pointerHeld = true;
            pointerId = id;
            pressedCell = cell;
            maxTravel = 0f;
            view.SetSelection(cell);
        }

        public void DragPointer(int id, Vector2 screenPosition)
        {
            if (!pointerHeld || id != pointerId) return;
            if (view.TryLocalPoint(screenPosition, out var local))
                maxTravel = Mathf.Max(maxTravel, (local - pressLocal).magnitude);
        }

        public void EndPointer(int id, Vector2 screenPosition)
        {
            if (!pointerHeld || id != pointerId || State != BoardInteractionState.Ready) return;
            DragPointer(id, screenPosition);
            pointerHeld = false;
            if (!view.TryCell(screenPosition, out var destination) ||
                !view.TryLocalPoint(screenPosition, out var local))
            {
                view.SetSelection(selected);
                return;
            }

            if (maxTravel < view.CellSize * 0.18f && destination == pressedCell)
            {
                Tap(pressedCell);
                return;
            }

            var delta = local - pressLocal;
            float dominant = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            float minor = Mathf.Min(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            bool clearDirection = dominant >= view.CellSize * 0.30f && dominant >= minor * 1.4f;
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            bool directionMatches = horizontal
                ? destination.Y == pressedCell.Y && (destination.X - pressedCell.X) * delta.x > 0f
                : destination.X == pressedCell.X && (destination.Y - pressedCell.Y) * delta.y > 0f;
            if (clearDirection && directionMatches && pressedCell.IsAdjacentTo(destination))
                RequestSwap(pressedCell, destination);
            else
                view.SetSelection(selected);
        }

        private void Tap(BoardPosition cell)
        {
            if (!selected.HasValue) selected = cell;
            else if (selected.Value == cell) selected = null;
            else if (selected.Value.IsAdjacentTo(cell))
            {
                RequestSwap(selected.Value, cell);
                return;
            }
            else selected = cell;
            view.SetSelection(selected);
            ReadyStatus(selected.HasValue ? "Choose a neighbor" : "Choose a tile");
        }

        // Both tap and drag end here. Core decides validity; the view only animates the result.
        private void RequestSwap(BoardPosition first, BoardPosition second)
        {
            if (State != BoardInteractionState.Ready) return;
            State = BoardInteractionState.Swapping;
            pointerHeld = false;
            selected = null;
            view.SetSelection(null);
            view.SetStatus("SWAPPING  -  Checking match");
            StartCoroutine(RunSafely(PlaySwap(first, second)));
        }

        private IEnumerator PlaySwap(BoardPosition first, BoardPosition second)
        {
            ResolutionResult resolution = null;
            bool accepted;
            if (session != null)
            {
                var turn = session.TrySwap(first, second);
                accepted = turn.Accepted;
                resolution = turn.Resolution;
            }
            else
            {
                accepted = board.TrySwap(first, second).IsValid;
                if (accepted) resolution = BoardResolver.Resolve(board, random, objectives: objectives, swapDestination: second,
                    reshufflePolicy: ReshufflePolicy.Automatic);
            }
            if (accepted && !diagnostic) tutorialDismissed = true;
            RefreshHud();
            yield return view.AnimateExchange(first, second, false);
            if (!accepted)
            {
                yield return view.AnimateExchange(first, second, true);
                view.ShowBoard(board);
                State = BoardInteractionState.Ready;
                ReadyStatus("No match; swap returned");
                yield break;
            }

            view.ShowSnapshot(resolution.Initial);
            State = BoardInteractionState.Resolving;
            yield return PresentResolution(resolution);
        }

        private IEnumerator PresentResolution(ResolutionResult result)
        {
            view.ShowObjectives(result.InitialObjectives);
            for (int i = 0; i < result.Waves.Count; i++)
            {
                var wave = result.Waves[i];
                State = BoardInteractionState.Resolving;
                view.SetStatus($"RESOLVING  -  Wave {i + 1}/{result.WaveCount}  |  Cascades: {i}");
                view.ShowSnapshot(wave.Before);
                view.Highlight(wave.Matches);
                yield return new WaitForSecondsRealtime(0.12f);
                foreach (var activation in wave.Activations)
                {
                    view.SetStatus($"RESOLVING  -  Wave {i + 1}/{result.WaveCount}\n" +
                        $"{(activation.IsChained ? "CHAIN" : "STRIPE")}  -  {activation.Tile.Stripe} at {activation.Position}");
                    yield return view.AnimateActivation(activation);
                }
                yield return view.AnimateClear(wave.Removals);
                view.ShowSnapshot(wave.AfterClear);
                view.ShowObjectives(wave.ObjectivesAfter);
                yield return view.AnimateCreation(wave.Creations);
                yield return view.AnimateGravity(wave.Movements);
                view.ShowSnapshot(wave.AfterGravity);
                yield return view.AnimateRefill(wave.Spawns);
                view.ShowSnapshot(wave.AfterRefill);
            }
            if (result.Reshuffle != null)
            {
                State = BoardInteractionState.Reshuffling;
                view.SetStatus($"RESHUFFLING  -  No legal moves  |  Attempts: {result.Reshuffle.Attempts}");
                if (result.Reshuffle.Success) yield return view.AnimateReshuffle(result.Reshuffle);
            }
            view.ShowSnapshot(result.Final);
            view.ShowObjectives(result.FinalObjectives);
            if (!result.Success)
            {
                EnterError(result.Error);
                yield break;
            }
            LastWaveCount = result.WaveCount;
            RefreshHud();
            // Core has already decided; only expose the result after every wave and shuffle is visible.
            if (session != null && session.State != GameplayState.Active)
            {
                State = BoardInteractionState.Terminal;
                bool win = session.State == GameplayState.Win;
                bool noMoves = session.Failure == GameplayFailure.NoLegalMoves;
                string failure = noMoves ? "No more matches available.\nTry again!" : "Out of Moves";
                view.SetStatus(win ? "Objectives complete!" : noMoves ? "No legal swaps remaining" : "No moves remaining");
                view.ShowResult(win ? (currentLevel == 0 ? "Level Complete!" : "Game Complete!") : failure,
                    win ? (currentLevel == 0 ? "NEXT LEVEL" : "PLAY AGAIN") : "RETRY");
                yield break;
            }
            State = BoardInteractionState.Ready;
            ReadyStatus(result.Reshuffle != null ? "Reshuffled; choose a tile" : "Choose a tile");
        }

        private void ReadyStatus(string message)
        {
            view.SetStatus($"READY  -  {message}\nLast waves: {LastWaveCount}  |  Cascades: {LastCascadeCount}");
        }

        private void EnterError(string message)
        {
            State = BoardInteractionState.Error;
            pointerHeld = false;
            selected = null;
            Debug.LogError("CatMatch3: " + message + " Use Restart to recover.", this);
            if (board != null) view.ShowSnapshot(board.Capture());
            view.ShowObjectives(Progress);
            view.SetStatus("ERROR  -  " + message + "\nUse Restart to recover.");
        }

        // Advance nested animation iterators here so exceptions in either Core or a view animation
        // also enter Error. A plain yield-return child coroutine would bypass the parent's catch.
        private IEnumerator RunSafely(IEnumerator operation)
        {
            var pending = new Stack<IEnumerator>();
            pending.Push(operation);
            try
            {
                while (pending.Count > 0)
                {
                    object current = null;
                    Exception failure = null;
                    bool more = false;
                    try
                    {
                        more = pending.Peek().MoveNext();
                        if (more) current = pending.Peek().Current;
                    }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null) { EnterError(failure.Message); yield break; }
                    if (!more) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator child) pending.Push(child);
                    else yield return current;
                }
            }
            finally
            {
                while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("M2 QA/Run cascade demo")]
        private void RunCascadeDemo()
        {
            if (!Application.isPlaying || board == null || State != BoardInteractionState.Ready) return;
            initialScenario = M3QaScenario.Original;
            ResetDiagnostic();
            random = new SeededRandom(141); // Three waves (initial match plus two cascades).
            RequestSwap(new BoardPosition(1, 0), new BoardPosition(1, 1));
        }

        [ContextMenu("M2 QA/Run deadlock reshuffle demo")]
        private void RunReshuffleDemo()
        {
            if (!Application.isPlaying || board == null || State != BoardInteractionState.Ready) return;
            initialScenario = M3QaScenario.Original;
            ResetDiagnostic();
            diagnosticBoard = PrototypeLayout.CreateDeadlockedBoard();
            view.ShowBoard(board);
            State = BoardInteractionState.Reshuffling;
            StartCoroutine(RunSafely(ResolveDeadlockDemo()));
        }

        private IEnumerator ResolveDeadlockDemo()
        {
            view.SetStatus("RESHUFFLING  -  Stable board with no legal moves");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PresentResolution(BoardResolver.Resolve(board, random, objectives: objectives,
                reshufflePolicy: ReshufflePolicy.Automatic));
        }

        [ContextMenu("M3 QA/1a - Horizontal creation")]
        private void RunHorizontalCreation() { RunM3Demo(M3QaScenario.HorizontalCreation); }
        [ContextMenu("M3 QA/1b - Vertical creation")]
        private void RunVerticalCreation() { RunM3Demo(M3QaScenario.VerticalCreation); }
        [ContextMenu("M3 QA/2a - Horizontal activation")]
        private void RunHorizontalActivation() { RunM3Demo(M3QaScenario.HorizontalActivation); }
        [ContextMenu("M3 QA/2b - Vertical activation")]
        private void RunVerticalActivation() { RunM3Demo(M3QaScenario.VerticalActivation); }
        [ContextMenu("M3 QA/3 - Chain activation")]
        private void RunChainDemo() { RunM3Demo(M3QaScenario.Chain); }
        [ContextMenu("M3 QA/4 - Objective progress")]
        private void RunObjectivesDemo() { RunM3Demo(M3QaScenario.Objectives); }
        [ContextMenu("M3 QA/Return to original board")]
        private void ReturnToOriginal()
        {
            if (!Application.isPlaying || board == null || State != BoardInteractionState.Ready) return;
            initialScenario = M3QaScenario.Original;
            ResetDiagnostic();
        }

        private void RunM3Demo(M3QaScenario scenario)
        {
            if (!Application.isPlaying || board == null || State != BoardInteractionState.Ready) return;
            initialScenario = scenario;
            ResetDiagnostic();
            State = BoardInteractionState.Swapping;
            StartCoroutine(RunSafely(PlayM3Demo(scenario)));
        }

        private IEnumerator PlayM3Demo(M3QaScenario scenario)
        {
            view.SetStatus("M3 QA  -  " + scenario + "\nAutomatic swap in 0.6 seconds");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PlaySwap(new BoardPosition(1, 1), M3PrototypeLayout.SwapDestination(scenario));
        }

        [ContextMenu("Diagnostics/Reset current M2-M3 fixture")]
        private void ResetCurrentDiagnostic()
        {
            if (Application.isPlaying && diagnostic && session == null) ResetDiagnostic();
        }

        [ContextMenu("M4 QA/Win current level in one move")]
        private void RunWinDemo() { RunM4Demo(false, false); }
        [ContextMenu("M4 QA/Fail current level in one move")]
        private void RunFailDemo() { RunM4Demo(true, false); }
        [ContextMenu("M4 QA/Final move cascade Win")]
        private void RunLastMoveDemo() { RunM4Demo(false, true); }
        [ContextMenu("M4 Puzzle QA/No Legal Moves - genuine swap")]
        private void RunNoLegalMovesDemo()
        {
            if (!Application.isPlaying || !started) return;
            CancelPresentation();
            session = new GameplaySession(PuzzleDiagnostics.NoLegalMoves(), 0);
            diagnostic = true;
            ShowFreshBoard();
            RequestSwap(new BoardPosition(2, 5), new BoardPosition(2, 6));
        }
        [ContextMenu("M4 QA/Return to production level")]
        private void ReturnToProduction() { if (Application.isPlaying) ResetBoard(); }

        private void RunM4Demo(bool fail, bool cascade)
        {
            if (!Application.isPlaying || !started) return;
            CancelPresentation();
            // Transient diagnostic configuration: genuine swap/removals, never asset/count mutation.
            var cells = AuthoredLevels.Create(1).CopyCells();
            var targets = new Dictionary<CatType, int>();
            var paws = new List<BoardPosition>();
            if (fail) targets.Add(CatType.Orange, 999);
            else if (cascade) targets.Add(CatType.Orange, 6);
            else if (currentLevel == 0) targets.Add(CatType.Orange, 3);
            else { paws.Add(new BoardPosition(0, 0)); paws.Add(new BoardPosition(1, 0)); paws.Add(new BoardPosition(2, 0)); }
            var config = new LevelConfiguration("qa", "QA one-move fixture", 7, 7, cells, 1, targets, paws,
                cascade ? 141u : ResetSeed);
            session = new GameplaySession(config, ResetSeed);
            diagnostic = true;
            ShowFreshBoard();
            RequestSwap(new BoardPosition(1, 0), new BoardPosition(1, 1));
        }
#endif
    }
}
