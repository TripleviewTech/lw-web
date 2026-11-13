// Ported from Apps\LwCadMappingUX (the WPF mock-up) - approximate demo geography of Lowell.
#nullable disable
using System;
using System.Globalization;

namespace LwCadMapping.Web.Geo
{
    /// <summary>
    /// A WGS84 position (spatial reference 4326), as the legacy map sets its
    /// extent in CadMap.xaml.cs. Formats it the three ways the legacy coordinate
    /// box shows it: decimal, degrees and Web Mercator map coordinates (102100).
    /// </summary>
    public readonly struct GeoPoint : IEquatable<GeoPoint>
    {
        private const double EarthRadius = 6378137.0;

        public GeoPoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }
        public double Longitude { get; }

        public bool IsEmpty => Latitude == 0 && Longitude == 0;

        public string ToDecimal() =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.000000}, {1:0.000000}", Latitude, Longitude);

        public string ToDegrees() => $"{Dms(Latitude, "N", "S")}  {Dms(Longitude, "E", "W")}";

        public string ToMapCoordinates()
        {
            var (x, y) = ToWebMercator();
            return string.Format(CultureInfo.InvariantCulture, "X {0:#,0.0}  Y {1:#,0.0}", x, y);
        }

        public (double X, double Y) ToWebMercator()
        {
            var x = EarthRadius * Longitude * Math.PI / 180.0;
            var y = EarthRadius * Math.Log(Math.Tan(Math.PI / 4 + Latitude * Math.PI / 360.0));
            return (x, y);
        }

        /// <summary>Great-circle distance in metres.</summary>
        public double DistanceTo(GeoPoint other)
        {
            const double r = 6371008.8;
            var dLat = (other.Latitude - Latitude) * Math.PI / 180.0;
            var dLon = (other.Longitude - Longitude) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Latitude * Math.PI / 180.0) * Math.Cos(other.Latitude * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        /// <summary>Moves toward a target by at most the given metres.</summary>
        public GeoPoint StepToward(GeoPoint target, double metres)
        {
            var d = DistanceTo(target);
            if (d <= metres || d < 0.5) return target;
            var f = metres / d;
            return new GeoPoint(Latitude + (target.Latitude - Latitude) * f,
                                Longitude + (target.Longitude - Longitude) * f);
        }

        /// <summary>
        /// "42.6459 -71.3106", "42.6459, -71.3106" or "-71.3106 42.6459" - the
        /// legacy LatLongFound accepted a coordinate pair typed into the search box.
        /// </summary>
        public static bool TryParse(string text, out GeoPoint point)
        {
            point = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                return false;

            // Latitude is the one inside +-90; a longitude-first pair is swapped.
            // Agencies are all in North America (north latitude, west longitude),
            // so a negative-then-positive pair is longitude first too.
            if ((Math.Abs(a) > 90 && Math.Abs(b) <= 90) || (a < 0 && b > 0)) (a, b) = (b, a);
            if (Math.Abs(a) > 90 || Math.Abs(b) > 180) return false;

            point = new GeoPoint(a, b);
            return true;
        }

        private static string Dms(double value, string positive, string negative)
        {
            var hemi = value >= 0 ? positive : negative;
            value = Math.Abs(value);
            var d = (int)value;
            var mFull = (value - d) * 60;
            var m = (int)mFull;
            var s = (mFull - m) * 60;
            return string.Format(CultureInfo.InvariantCulture, "{0}°{1:00}'{2:00.0}\"{3}", d, m, s, hemi);
        }

        public bool Equals(GeoPoint other) => Latitude == other.Latitude && Longitude == other.Longitude;
        public override bool Equals(object obj) => obj is GeoPoint g && Equals(g);
        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
        public static bool operator ==(GeoPoint a, GeoPoint b) => a.Equals(b);
        public static bool operator !=(GeoPoint a, GeoPoint b) => !a.Equals(b);
        public override string ToString() => ToDecimal();
    }
}
