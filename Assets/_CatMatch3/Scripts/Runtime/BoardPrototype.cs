using System;
using System.Collections;
using System.Collections.Generic;
using CatMatch3.Core;
using UnityEngine;

namespace CatMatch3.Runtime
{
    public enum BoardInteractionState { Ready, Swapping, Resolving, Reshuffling, Error }

    /// <summary>Input and ordered presentation of Core resolution snapshots.</summary>
    [DisallowMultipleComponent]
    public sealed class BoardPrototype : MonoBehaviour
    {
        [SerializeField] private BoardView view;
        private Board board;
        public const uint ResetSeed = 1337;
        private IRandomSource random;
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
                Debug.LogError("CatMatch3: BoardView reference is missing. Rerun Apply M2 Resolution.", this);
                enabled = false;
                return;
            }
            view.Build(this);
            ResetBoard();
        }

        public void ResetBoard()
        {
            StopAllCoroutines();
            pointerHeld = false;
            selected = null;
            board = PrototypeLayout.CreateBoard();
            random = new SeededRandom(ResetSeed);
            LastWaveCount = 0;
            State = BoardInteractionState.Ready;
            view.ShowBoard(board);
            ReadyStatus("Choose a tile");
        }

        private void OnDisable()
        {
            if (board != null && view != null) ResetBoard();
        }

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
            var result = board.TrySwap(first, second);
            yield return view.AnimateExchange(first, second, false);
            if (!result.IsValid)
            {
                yield return view.AnimateExchange(first, second, true);
                view.ShowBoard(board);
                State = BoardInteractionState.Ready;
                ReadyStatus("No match; swap returned");
                yield break;
            }

            view.ShowBoard(board);
            State = BoardInteractionState.Resolving;
            yield return PresentResolution(BoardResolver.Resolve(board, random));
        }

        private IEnumerator PresentResolution(ResolutionResult result)
        {
            for (int i = 0; i < result.Waves.Count; i++)
            {
                var wave = result.Waves[i];
                State = BoardInteractionState.Resolving;
                view.SetStatus($"RESOLVING  -  Wave {i + 1}/{result.WaveCount}  |  Cascades: {i}");
                view.ShowSnapshot(wave.Before);
                view.Highlight(wave.Matches);
                yield return new WaitForSecondsRealtime(0.12f);
                yield return view.AnimateClear(wave.Matches);
                view.ShowSnapshot(wave.AfterClear);
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
            if (!result.Success)
            {
                EnterError(result.Error);
                yield break;
            }
            LastWaveCount = result.WaveCount;
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
            Debug.LogError("CatMatch3 M2: " + message + " Use Reset Board to recover.", this);
            view.ShowSnapshot(board.Capture());
            view.SetStatus("ERROR  -  " + message + "\nUse Reset Board to recover.");
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
            ResetBoard();
            random = new SeededRandom(141); // Three waves (initial match plus two cascades).
            RequestSwap(new BoardPosition(1, 0), new BoardPosition(1, 1));
        }

        [ContextMenu("M2 QA/Run deadlock reshuffle demo")]
        private void RunReshuffleDemo()
        {
            if (!Application.isPlaying || board == null || State != BoardInteractionState.Ready) return;
            ResetBoard();
            board = PrototypeLayout.CreateDeadlockedBoard();
            view.ShowBoard(board);
            State = BoardInteractionState.Reshuffling;
            StartCoroutine(RunSafely(ResolveDeadlockDemo()));
        }

        private IEnumerator ResolveDeadlockDemo()
        {
            view.SetStatus("RESHUFFLING  -  Stable board with no legal moves");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PresentResolution(BoardResolver.Resolve(board, random));
        }
#endif
    }
}
