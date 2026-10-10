namespace CatMatch3.Core
{
    public enum SwapStatus { Accepted, OutOfBounds, NotAdjacent, NoMatch }

    public sealed class SwapResult
    {
        public SwapStatus Status { get; }
        public bool IsValid => Status == SwapStatus.Accepted;
        public MatchResult Matches { get; }

        internal SwapResult(SwapStatus status, MatchResult matches)
        {
            Status = status;
            Matches = matches;
        }
    }
}
