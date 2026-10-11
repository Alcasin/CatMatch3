using System;

namespace CatMatch3.Core
{
    public enum GameplayState { Active, Win, Fail, Error }
    public enum GameplayFailure { None, OutOfMoves, NoLegalMoves }

    public sealed class GameplayTurn
    {
        public bool Accepted { get; }
        public ResolutionResult Resolution { get; }
        internal GameplayTurn(bool accepted, ResolutionResult resolution = null)
        { Accepted = accepted; Resolution = resolution; }
    }

    /// <summary>One board, tracker and random stream. Rules finish before Runtime presents the turn.</summary>
    public sealed class GameplaySession
    {
        private IRandomSource random;
        public LevelConfiguration Level { get; }
        public uint Seed { get; }
        public Board Board { get; private set; }
        public ObjectiveSnapshot Progress => objectives.Capture();
        private ObjectiveTracker objectives;
        public int RemainingMoves { get; private set; }
        public GameplayState State { get; private set; }
        public string Error { get; private set; }
        public GameplayFailure Failure { get; private set; }

        // Runtime selects a fallback once per level entry; Restart always reuses the chosen seed.
        public GameplaySession(LevelConfiguration level, uint fallbackSeed)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Seed = level.DevelopmentSeed ?? fallbackSeed;
            Restart();
        }

        public void Restart()
        {
            Board = Level.CreateBoard();
            objectives = Level.CreateObjectives();
            random = new SeededRandom(Seed);
            RemainingMoves = Level.MoveLimit;
            State = GameplayState.Active;
            Error = null;
            Failure = GameplayFailure.None;
        }

        public GameplayTurn TrySwap(BoardPosition first, BoardPosition second)
        {
            if (State != GameplayState.Active) return new GameplayTurn(false);
            try
            {
                if (!Board.TrySwap(first, second).IsValid) return new GameplayTurn(false);
                RemainingMoves--;
                // A player decision is an undirected pair. Use the lower flat-index endpoint as
                // the creation destination, so reversing tap/swipe notation cannot change a puzzle.
                var destination = first.Y * Board.Width + first.X < second.Y * Board.Width + second.X ? first : second;
                var resolution = BoardResolver.Resolve(Board, random, objectives: objectives, swapDestination: destination,
                    reshufflePolicy: ReshufflePolicy.Disabled);
                EvaluateStableResult(resolution);
                return new GameplayTurn(true, resolution);
            }
            catch (Exception exception)
            {
                State = GameplayState.Error;
                Error = exception.Message;
                throw;
            }
        }

        private void EvaluateStableResult(ResolutionResult resolution)
        {
            if (!resolution.Success) { State = GameplayState.Error; Error = resolution.Error; }
            else if (Progress.IsComplete) State = GameplayState.Win;
            else if (RemainingMoves == 0) { State = GameplayState.Fail; Failure = GameplayFailure.OutOfMoves; }
            else if (!Board.HasLegalMove()) { State = GameplayState.Fail; Failure = GameplayFailure.NoLegalMoves; }
        }
    }
}
