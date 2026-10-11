using System;

namespace CatMatch3.Core
{
    public enum StripeOrientation { None, Horizontal, Vertical }

    /// <summary>Movable tile identity. Cell objectives deliberately do not live here.</summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        public CatType Type { get; }
        public StripeOrientation Stripe { get; }
        public bool IsSpecial => Stripe != StripeOrientation.None;

        public Tile(CatType type, StripeOrientation stripe = StripeOrientation.None)
        {
            if (!Enum.IsDefined(typeof(CatType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(StripeOrientation), stripe)) throw new ArgumentOutOfRangeException(nameof(stripe));
            Type = type; Stripe = stripe;
        }

        public bool Equals(Tile other) { return Type == other.Type && Stripe == other.Stripe; }
        public override bool Equals(object obj) { return obj is Tile other && Equals(other); }
        public override int GetHashCode() { return (int)Type * 397 ^ (int)Stripe; }
        public override string ToString() { return IsSpecial ? Type + " / " + Stripe : Type.ToString(); }
    }
}
