using System;

namespace LwCadSystemUX
{
    /// <summary>How a <see cref="GridLength"/> is measured, as WPF's GridUnitType.</summary>
    public enum GridUnitType { Auto, Pixel, Star }

    /// <summary>
    /// A UI-free stand-in for WPF's GridLength, so the monitor sizing logic in
    /// CallListViewModel ports unchanged. The Blazor board turns it into a CSS
    /// grid track: Auto -> "auto", Star -> "{n}fr", Pixel -> "{n}px".
    /// </summary>
    public readonly struct GridLength : IEquatable<GridLength>
    {
        public GridLength(double value, GridUnitType unitType = GridUnitType.Pixel)
        {
            Value = value;
            GridUnitType = unitType;
        }

        public static GridLength Auto => new GridLength(1, GridUnitType.Auto);

        public double Value { get; }
        public GridUnitType GridUnitType { get; }
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;

        /// <summary>The CSS grid track for this length.</summary>
        public string ToCss() => GridUnitType switch
        {
            GridUnitType.Auto => "auto",
            GridUnitType.Star => Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "fr",
            _ => Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "px"
        };

        public bool Equals(GridLength other) => GridUnitType == other.GridUnitType && Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is GridLength g && Equals(g);
        public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);
        public static bool operator ==(GridLength a, GridLength b) => a.Equals(b);
        public static bool operator !=(GridLength a, GridLength b) => !a.Equals(b);
    }
}
