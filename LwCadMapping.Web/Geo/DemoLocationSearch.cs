// Ported from Apps\LwCadMappingUX (the WPF mock-up) - approximate demo geography of Lowell.
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


namespace LwCadMapping.Web.Geo
{
    public class DemoLocationResult
    {
        public GeoPoint Point { get; set; }

        /// <summary>The matched text, as it is shown in the callout and the selection panel.</summary>
        public string Display { get; set; }

        /// <summary>Address, Intersection, Lat/Long, Premise, Place or Street.</summary>
        public string MatchType { get; set; }

        public string Sector { get; set; }
    }

    /// <summary>
    /// The mock-up's geocoder. It stands in for both legacy paths: LatLongFound
    /// (a coordinate pair typed in the box) and AddressToLocationFind (the
    /// ArcGIS World GeocodeServer, with the agency's default city and state).
    /// Here it resolves against the demo street file instead - the server-side
    /// tenant locator of ADR-007 is the production answer.
    /// </summary>
    public class DemoLocationSearch
    {
        private static readonly (string Long, string Short)[] Suffixes =
        {
            ("STREET", "ST"), ("AVENUE", "AVE"), ("ROAD", "RD"), ("BOULEVARD", "BLVD"),
            ("DRIVE", "DR"), ("HIGHWAY", "HWY"), ("LANE", "LN"), ("PLACE", "PL"),
        };

        private readonly Func<IEnumerable<Premise>> _premises;

        public DemoLocationSearch(Func<IEnumerable<Premise>> premises)
        {
            _premises = premises;
        }

        public string DefaultCity { get; set; } = "Lowell";
        public string DefaultState { get; set; } = "Massachusetts";
        public string DefaultZip { get; set; } = "01852";

        public DemoLocationResult Find(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // 1. A coordinate pair, as LatLongFound took it.
            if (GeoPoint.TryParse(text, out var latLong))
                return Result(latLong, latLong.ToDecimal(), "Lat/Long");

            var q = Normalise(text);

            // 2. An intersection: "CHELMSFORD ST & STEVENS ST", "x AND y", "x / y".
            var parts = Regex.Split(q, @"\s*(?:&|/|\bAND\b|\bAT\b)\s*").Where(s => s.Length > 0).ToArray();
            if (parts.Length == 2)
            {
                var a = DemoGeography.FindRoad(StripNumber(parts[0]));
                var b = DemoGeography.FindRoad(StripNumber(parts[1]));
                if (a != null && b != null && a != b)
                {
                    var p = Geo.ClosestApproach(a.Points, b.Points, out var gap);
                    if (gap < 80) return Result(p, $"{a.Name} & {b.Name}", "Intersection");
                }
            }

            // 3. A house number on a known street.
            var m = Regex.Match(q, @"^(\d+)[A-Z]?\s+(.+)$");
            if (m.Success)
            {
                var road = DemoGeography.FindRoad(m.Groups[2].Value);
                if (road != null)
                {
                    var no = int.Parse(m.Groups[1].Value);
                    return Result(Geocode(road, no), $"{no} {road.Name}", "Address");
                }
            }

            // 4. A premise by name or address.
            var premise = _premises()?.FirstOrDefault(p =>
                Normalise(p.Name).Contains(q) || Normalise(p.Address) == q);
            if (premise != null) return Result(premise.Point, $"{premise.Name} - {premise.Address}", "Premise");

            // 5. A place, school or park.
            var place = DemoGeography.Places.FirstOrDefault(p => Normalise(p.Text).Contains(q));
            if (place != null) return Result(place.Point, place.Text, "Place");

            var school = DemoGeography.Schools.FirstOrDefault(s => Normalise(s.Name).Contains(q));
            if (school != null) return Result(school.Point, school.Name, "Place");

            var park = DemoGeography.Parks.FirstOrDefault(p => Normalise(p.Name).Contains(q));
            if (park != null) return Result(park.LabelAt, park.Name, "Place");

            // 6. A street with no number: its middle.
            var street = DemoGeography.FindRoad(q);
            if (street != null) return Result(Geo.Along(street.Points, 0.5), street.Name, "Street");

            return null;
        }

        /// <summary>Address point for a house number: odd numbers on the left, even on the right.</summary>
        public static GeoPoint Geocode(Road road, int number)
        {
            var max = Math.Max(1, road.MaxNumber);
            return Geo.Along(road.Points, Math.Min(number, max) / (double)max, 16, number % 2 == 1);
        }

        /// <summary>
        /// The nearest street address to a point - what the mock-up shows for a
        /// marked location. Beyond 150 m of a named street it falls back to the
        /// neighbourhood.
        /// </summary>
        public DemoLocationResult Reverse(GeoPoint p)
        {
            Road bestRoad = null;
            double best = double.MaxValue, bestFraction = 0;
            bool bestLeft = true;

            foreach (var road in DemoGeography.Roads.Where(r => r.Name != null && r.MaxNumber > 0))
            {
                var d = Geo.Nearest(p, road.Points, out var f, out var left);
                if (d < best)
                {
                    best = d;
                    bestRoad = road;
                    bestFraction = f;
                    bestLeft = left;
                }
            }

            if (bestRoad != null && best <= 150)
            {
                var no = Math.Max(1, (int)Math.Round(bestFraction * bestRoad.MaxNumber));
                if ((no % 2 == 1) != bestLeft) no = Math.Max(1, no + 1);
                return Result(p, $"{no} {bestRoad.Name}", "Address");
            }

            var place = DemoGeography.Places.Where(x => x.Rank > 0)
                .OrderBy(x => x.Point.DistanceTo(p)).FirstOrDefault();
            return Result(p, place == null ? p.ToDecimal() : $"Near {place.Text}", "Lat/Long");
        }

        /// <summary>"16 Main St, Lowell, Massachusetts, 01852" - the legacy callout's address line.</summary>
        public string FullAddress(string street) =>
            $"{Geo.TitleCase(street)}, {DefaultCity}, {DefaultState}, {DefaultZip}";

        private static DemoLocationResult Result(GeoPoint p, string display, string type) =>
            new DemoLocationResult { Point = p, Display = display, MatchType = type, Sector = DemoGeography.SectorOf(p) };

        private static string StripNumber(string s) => Regex.Replace(s, @"^\d+[A-Z]?\s+", "");

        /// <summary>Upper case, no city / state tail, long suffixes shortened.</summary>
        public static string Normalise(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var s = text.ToUpperInvariant();
            var comma = s.IndexOf(',');
            if (comma > 0 && !GeoPoint.TryParse(text, out _)) s = s.Substring(0, comma);
            s = Regex.Replace(s, @"[\.#]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            foreach (var (l, sh) in Suffixes) s = Regex.Replace(s, $@"\b{l}\b", sh);
            return s;
        }
    }
}
