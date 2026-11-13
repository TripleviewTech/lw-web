// Ported from Apps\LwCadMappingUX (the WPF mock-up) - approximate demo geography of Lowell.
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;


namespace LwCadMapping.Web.Geo
{
    public enum RoadClass
    {
        Minor,
        Major,
        Highway,
    }

    /// <summary>A street centreline. Named streets carry a house-number range so they geocode.</summary>
    public class Road
    {
        public string Name { get; set; }
        public string Shield { get; set; }
        public RoadClass Class { get; set; }
        public GeoPoint[] Points { get; set; }

        /// <summary>Highest house number; numbers run evenly from the first point to the last.</summary>
        public int MaxNumber { get; set; }

        public string DisplayName => Name == null ? null : Geo.TitleCase(Name);
    }

    public class Area
    {
        public string Name { get; set; }
        public GeoPoint[] Ring { get; set; }
        public GeoPoint LabelAt { get; set; }
    }

    public class PlaceLabel
    {
        public string Text { get; set; }
        public GeoPoint Point { get; set; }

        /// <summary>0 town, 1 neighbourhood, 2 small feature.</summary>
        public int Rank { get; set; }
    }

    public class PointFeature
    {
        public string Name { get; set; }
        public GeoPoint Point { get; set; }
    }

    /// <summary>
    /// The mock-up's stand-in for the agency map package (.mpk). Lowell, MA
    /// drawn by hand from the legacy default extent (-71.382, 42.602 to -71.231,
    /// 42.669). Street lines are approximate and house numbers are synthetic -
    /// enough to geocode, search and route against, not survey grade.
    /// </summary>
    public static class DemoGeography
    {
        /// <summary>The legacy setExtent in CadMap.xaml.cs.</summary>
        public static readonly GeoPoint ExtentNorthWest = new GeoPoint(42.669491, -71.382294);
        public static readonly GeoPoint ExtentSouthEast = new GeoPoint(42.601802, -71.231232);
        public static readonly GeoPoint Home = new GeoPoint(42.6420, -71.3110);
        public const double HomeZoom = 13.4;

        public static List<Road> Roads { get; } = new List<Road>();
        public static List<GeoPoint[]> Rivers { get; } = new List<GeoPoint[]>();
        public static List<double> RiverWidths { get; } = new List<double>();
        public static List<Area> Parks { get; } = new List<Area>();
        public static List<PlaceLabel> Places { get; } = new List<PlaceLabel>();
        public static List<Area> PoliceSectors { get; } = new List<Area>();
        public static List<Area> FireDistricts { get; } = new List<Area>();
        public static List<PointFeature> Hydrants { get; } = new List<PointFeature>();
        public static List<PointFeature> Schools { get; } = new List<PointFeature>();

        private static readonly GeoPoint[] Merrimack =
        {
            P(42.6600, -71.3950), P(42.6560, -71.3700), P(42.6525, -71.3550), P(42.6510, -71.3420),
            P(42.6505, -71.3330), P(42.6490, -71.3230), P(42.6505, -71.3130), P(42.6500, -71.3050),
            P(42.6470, -71.2960), P(42.6420, -71.2880), P(42.6390, -71.2780), P(42.6400, -71.2650),
            P(42.6440, -71.2500), P(42.6490, -71.2300),
        };

        static DemoGeography()
        {
            Rivers.Add(Merrimack);
            RiverWidths.Add(190);
            Rivers.Add(new[]
            {
                P(42.6000, -71.2850), P(42.6150, -71.2930), P(42.6250, -71.2965),
                P(42.6340, -71.3000), P(42.6410, -71.3010), P(42.6478, -71.2990),
            });
            RiverWidths.Add(38);

            BuildHighways();
            BuildMajorRoads();
            // Minor street grids are texture only on the drawn mock-up map; the ArcGIS basemap has real streets.
            BuildParksAndPlaces();
            BuildSectors();
            BuildFeatures();
        }

        private static GeoPoint P(double lat, double lon) => new GeoPoint(lat, lon);

        private static void Road(string name, RoadClass cls, int maxNo, params GeoPoint[] pts) =>
            Roads.Add(new Road { Name = name, Class = cls, MaxNumber = maxNo, Points = pts });

        private static void BuildHighways()
        {
            Roads.Add(new Road
            {
                Name = "INTERSTATE 495", Shield = "495", Class = RoadClass.Highway,
                Points = new[]
                {
                    P(42.5960, -71.3800), P(42.6060, -71.3600), P(42.6130, -71.3420), P(42.6185, -71.3250),
                    P(42.6230, -71.3080), P(42.6270, -71.2920), P(42.6330, -71.2780), P(42.6430, -71.2620),
                    P(42.6560, -71.2450), P(42.6680, -71.2300),
                },
            });
            Roads.Add(new Road
            {
                Name = "US HIGHWAY 3", Shield = "3", Class = RoadClass.Highway,
                Points = new[]
                {
                    P(42.6900, -71.3950), P(42.6720, -71.3820), P(42.6520, -71.3700), P(42.6330, -71.3560),
                    P(42.6185, -71.3440), P(42.6060, -71.3330), P(42.5900, -71.3200),
                },
            });
            Roads.Add(new Road
            {
                Name = "LOWELL CONNECTOR", Class = RoadClass.Highway,
                Points = new[] { P(42.6150, -71.3400), P(42.6230, -71.3300), P(42.6300, -71.3230), P(42.6365, -71.3185) },
            });
        }

        private static void BuildMajorRoads()
        {
            var M = RoadClass.Major;
            Road("MERRIMACK ST", M, 700, P(42.6455, -71.3230), P(42.6460, -71.3160), P(42.6455, -71.3100), P(42.6452, -71.3060));
            Road("CENTRAL ST", M, 600, P(42.6452, -71.3060), P(42.6400, -71.3065), P(42.6340, -71.3060));
            Road("GORHAM ST", M, 1800, P(42.6340, -71.3060), P(42.6260, -71.3030), P(42.6150, -71.2990), P(42.6050, -71.2950));
            Road("BRIDGE ST", M, 900, P(42.6455, -71.3055), P(42.6530, -71.3045), P(42.6600, -71.3035), P(42.6700, -71.3020));
            Road("VFW HWY", M, 1200, P(42.6535, -71.3200), P(42.6535, -71.3100), P(42.6520, -71.3000), P(42.6480, -71.2900), P(42.6440, -71.2780), P(42.6440, -71.2650));
            Road("PAWTUCKET BLVD", M, 1400, P(42.6590, -71.3800), P(42.6575, -71.3600), P(42.6560, -71.3450), P(42.6545, -71.3330), P(42.6535, -71.3200));
            Road("MIDDLESEX ST", M, 1100, P(42.6440, -71.3150), P(42.6400, -71.3250), P(42.6350, -71.3380), P(42.6290, -71.3520));
            Road("WESTFORD ST", M, 1200, P(42.6460, -71.3230), P(42.6400, -71.3360), P(42.6350, -71.3480), P(42.6300, -71.3620));
            Road("CHELMSFORD ST", M, 1300, P(42.6370, -71.3180), P(42.6310, -71.3260), P(42.6240, -71.3370), P(42.6180, -71.3480));
            Road("THORNDIKE ST", M, 200, P(42.6440, -71.3130), P(42.6400, -71.3150), P(42.6365, -71.3185));
            Road("BROADWAY", M, 700, P(42.6465, -71.3150), P(42.6462, -71.3260), P(42.6470, -71.3360));
            Road("SCHOOL ST", M, 400, P(42.6462, -71.3260), P(42.6400, -71.3290), P(42.6340, -71.3300));
            Road("PAWTUCKET ST", M, 600, P(42.6462, -71.3262), P(42.6495, -71.3310), P(42.6550, -71.3300));
            Road("LAWRENCE ST", M, 1000, P(42.6400, -71.3030), P(42.6320, -71.2960), P(42.6240, -71.2870), P(42.6160, -71.2780));
            Road("ANDOVER ST", M, 1500, P(42.6420, -71.2995), P(42.6370, -71.2880), P(42.6330, -71.2760), P(42.6280, -71.2600));
            Road("ROGERS ST", M, 1500, P(42.6370, -71.2880), P(42.6290, -71.2780), P(42.6200, -71.2650));
            Road("VARNUM AVE", M, 1400, P(42.6550, -71.3300), P(42.6610, -71.3380), P(42.6660, -71.3480), P(42.6700, -71.3600));
            Road("MAMMOTH RD", M, 1500, P(42.6555, -71.3240), P(42.6640, -71.3230), P(42.6740, -71.3250), P(42.6850, -71.3270));
            Road("LAKEVIEW AVE", M, 900, P(42.6560, -71.3100), P(42.6630, -71.3110), P(42.6720, -71.3130));
            Road("STEVENS ST", M, 600, P(42.6400, -71.3360), P(42.6330, -71.3440), P(42.6150, -71.3450));
            Road("AIKEN ST", M, 400, P(42.6470, -71.3180), P(42.6540, -71.3170), P(42.6580, -71.3160));
            Road("FATHER MORISSETTE BLVD", M, 300, P(42.6475, -71.3200), P(42.6485, -71.3120), P(42.6480, -71.3060));
            Road("ARCAND DR", M, 100, P(42.6455, -71.3160), P(42.6440, -71.3130));
            Road("CHURCH ST", M, 300, P(42.6400, -71.3065), P(42.6410, -71.2995));
        }

        /// <summary>
        /// Neighbourhood street grids, unnamed, for texture. A segment is dropped
        /// when it would sit in the river.
        /// </summary>
        private static void BuildMinorGrids()
        {
            var grids = new[]
            {
                // centre lat, lon, angle, half-count, spacing m, half-length m
                (42.6450, -71.3120, 12.0, 4, 120.0, 520.0),   // Downtown
                (42.6470, -71.3230, 30.0, 3, 130.0, 420.0),   // Acre
                (42.6600, -71.3060, -8.0, 5, 150.0, 750.0),   // Centralville
                (42.6620, -71.3380, 25.0, 6, 160.0, 900.0),   // Pawtucketville
                (42.6360, -71.3400, 35.0, 6, 150.0, 900.0),   // Highlands
                (42.6400, -71.3260, 20.0, 3, 130.0, 450.0),   // Lower Highlands
                (42.6330, -71.3060, -5.0, 4, 140.0, 600.0),   // Back Central
                (42.6260, -71.3120, 10.0, 4, 150.0, 600.0),   // Sacred Heart
                (42.6190, -71.2950, 0.0, 4, 170.0, 700.0),    // South Lowell
                (42.6380, -71.2800, 40.0, 5, 150.0, 750.0),   // Belvidere
                (42.6730, -71.3150, 0.0, 4, 180.0, 700.0),    // Dracut line
            };

            foreach (var (lat, lon, angle, n, spacing, half) in grids)
            {
                var a = angle * Math.PI / 180.0;
                var dir = (X: Math.Cos(a), Y: Math.Sin(a));
                var nrm = (X: -dir.Y, Y: dir.X);

                for (int pass = 0; pass < 2; pass++)
                {
                    var along = pass == 0 ? dir : nrm;
                    var across = pass == 0 ? nrm : dir;

                    for (int i = -n; i <= n; i++)
                    {
                        var ox = across.X * i * spacing;
                        var oy = across.Y * i * spacing;
                        var steps = (int)(half * 2 / spacing);

                        for (int s = 0; s < steps; s++)
                        {
                            var t0 = -half + s * spacing;
                            var t1 = t0 + spacing;
                            var p0 = Geo.Offset(new GeoPoint(lat, lon), ox + along.X * t0, oy + along.Y * t0);
                            var p1 = Geo.Offset(new GeoPoint(lat, lon), ox + along.X * t1, oy + along.Y * t1);
                            var mid = new GeoPoint((p0.Latitude + p1.Latitude) / 2, (p0.Longitude + p1.Longitude) / 2);

                            if (Geo.DistanceToLine(mid, Merrimack) < 150) continue;
                            if (Geo.DistanceToLine(p0, Merrimack) < 105 || Geo.DistanceToLine(p1, Merrimack) < 105) continue;

                            Roads.Add(new Road { Class = RoadClass.Minor, Points = new[] { p0, p1 } });
                        }
                    }
                }
            }
        }

        private static void BuildParksAndPlaces()
        {
            Parks.Add(new Area
            {
                Name = "Lowell-Dracut-Tyngsboro State Forest",
                Ring = new[] { P(42.6690, -71.3720), P(42.6830, -71.3680), P(42.6850, -71.3460), P(42.6730, -71.3400), P(42.6660, -71.3520) },
                LabelAt = P(42.6765, -71.3560),
            });
            Parks.Add(new Area
            {
                Name = "Shedd Park",
                Ring = new[] { P(42.6290, -71.2880), P(42.6300, -71.2810), P(42.6240, -71.2800), P(42.6230, -71.2870) },
                LabelAt = P(42.6265, -71.2840),
            });
            Parks.Add(new Area
            {
                Name = "Fort Hill Park",
                Ring = new[] { P(42.6350, -71.2950), P(42.6365, -71.2905), P(42.6330, -71.2895), P(42.6318, -71.2940) },
                LabelAt = P(42.6340, -71.2922),
            });
            Parks.Add(new Area
            {
                Name = "Lowell Memorial Park",
                Ring = new[] { P(42.6600, -71.3260), P(42.6612, -71.3205), P(42.6580, -71.3195), P(42.6570, -71.3250) },
                LabelAt = P(42.6591, -71.3228),
            });

            void Place(string text, double lat, double lon, int rank) =>
                Places.Add(new PlaceLabel { Text = text, Point = P(lat, lon), Rank = rank });

            Place("Lowell", 42.6488, -71.3095, 0);
            Place("Dracut", 42.6790, -71.3000, 0);
            Place("Chelmsford", 42.6020, -71.3650, 0);
            Place("Tewksbury", 42.6120, -71.2420, 0);
            Place("Downtown", 42.6430, -71.3105, 1);
            Place("Acre", 42.6482, -71.3215, 1);
            Place("Centralville", 42.6585, -71.3040, 1);
            Place("Pawtucketville", 42.6640, -71.3360, 1);
            Place("Highlands", 42.6350, -71.3430, 1);
            Place("Lower Highlands", 42.6395, -71.3270, 1);
            Place("Back Central", 42.6330, -71.3045, 1);
            Place("Sacred Heart", 42.6255, -71.3125, 1);
            Place("South Lowell", 42.6180, -71.2960, 1);
            Place("Belvidere", 42.6395, -71.2790, 1);
            Place("Pawtucket Falls", 42.6522, -71.3330, 2);
            Place("Merrimack River", 42.6555, -71.3610, 2);
            Place("Concord River", 42.6180, -71.2915, 2);
        }

        /// <summary>
        /// Four police sectors - the Area column on the monitors. The river is the
        /// north / south line, Central and Bridge Streets the east / west one.
        /// </summary>
        private static void BuildSectors()
        {
            const double north = 42.6950, south = 42.5880, west = -71.3950, east = -71.2300, split = -71.3050;

            var riverWest = Merrimack.Where(p => p.Longitude <= split).ToList();
            var riverEast = Merrimack.Where(p => p.Longitude > split).ToList();
            var splitPoint = P(42.6500, split);

            PoliceSectors.Add(new Area
            {
                Name = "Sector 1",
                Ring = new[] { P(north, west), P(north, split), splitPoint }.Concat(Enumerable.Reverse(riverWest)).ToArray(),
                LabelAt = P(42.6700, -71.3500),
            });
            PoliceSectors.Add(new Area
            {
                Name = "Sector 2",
                Ring = new[] { P(north, split), P(north, east) }.Concat(Enumerable.Reverse(riverEast)).Concat(new[] { splitPoint }).ToArray(),
                LabelAt = P(42.6700, -71.2700),
            });
            PoliceSectors.Add(new Area
            {
                Name = "Sector 3",
                Ring = riverWest.Concat(new[] { splitPoint, P(south, split), P(south, west) }).ToArray(),
                LabelAt = P(42.6150, -71.3550),
            });
            PoliceSectors.Add(new Area
            {
                Name = "Sector 4",
                Ring = new[] { splitPoint }.Concat(riverEast).Concat(new[] { P(south, east), P(south, split) }).ToArray(),
                LabelAt = P(42.6100, -71.2700),
            });

            FireDistricts.Add(new Area { Name = "District 1", Ring = new[] { P(north, west), P(north, -71.3300), P(south, -71.3300), P(south, west) }, LabelAt = P(42.6260, -71.3700) });
            FireDistricts.Add(new Area { Name = "District 2", Ring = new[] { P(north, -71.3300), P(north, -71.2900), P(south, -71.2900), P(south, -71.3300) }, LabelAt = P(42.6100, -71.3150) });
            FireDistricts.Add(new Area { Name = "District 3", Ring = new[] { P(north, -71.2900), P(north, east), P(south, east), P(south, -71.2900) }, LabelAt = P(42.6260, -71.2500) });
        }

        private static void BuildFeatures()
        {
            int n = 0;
            foreach (var road in Roads.Where(r => r.Class == RoadClass.Major))
            {
                var length = Geo.Length(road.Points);
                for (double d = 90; d < length; d += 190)
                {
                    var p = Geo.Along(road.Points, d / length, 14, (n++ % 2) == 0);
                    Hydrants.Add(new PointFeature { Name = $"HYD-{1000 + n}", Point = p });
                }
            }

            Schools.Add(new PointFeature { Name = "Lowell High School", Point = P(42.6468, -71.3085) });
            Schools.Add(new PointFeature { Name = "Pawtucketville School", Point = P(42.6640, -71.3320) });
            Schools.Add(new PointFeature { Name = "Centralville School", Point = P(42.6575, -71.3080) });
            Schools.Add(new PointFeature { Name = "Highlands School", Point = P(42.6370, -71.3380) });
            Schools.Add(new PointFeature { Name = "Belvidere School", Point = P(42.6365, -71.2830) });
            Schools.Add(new PointFeature { Name = "Sacred Heart School", Point = P(42.6265, -71.3080) });
        }

        public static Road FindRoad(string normalisedName)
        {
            var named = Roads.Where(r => r.Name != null).ToList();
            return named.FirstOrDefault(r => r.Name == normalisedName)
                ?? named.FirstOrDefault(r => r.Name.StartsWith(normalisedName + " ", StringComparison.Ordinal))
                ?? named.FirstOrDefault(r => r.Name.Contains(normalisedName));
        }

        public static string SectorOf(GeoPoint p)
        {
            var hit = PoliceSectors.FirstOrDefault(s => Geo.Contains(s.Ring, p));
            return hit == null ? "" : hit.Name.Replace("Sector ", "");
        }
    }

    /// <summary>Small planar helpers. At city scale an equirectangular local frame is close enough.</summary>
    public static class Geo
    {
        private const double MetresPerDegLat = 110574.0;

        public static double MetresPerDegLon(double lat) => 111320.0 * Math.Cos(lat * Math.PI / 180.0);

        public static GeoPoint Offset(GeoPoint p, double eastMetres, double northMetres) =>
            new GeoPoint(p.Latitude + northMetres / MetresPerDegLat,
                         p.Longitude + eastMetres / MetresPerDegLon(p.Latitude));

        private static (double X, double Y) Local(GeoPoint p, double refLat) =>
            (p.Longitude * MetresPerDegLon(refLat), p.Latitude * MetresPerDegLat);

        public static double Length(GeoPoint[] line)
        {
            double sum = 0;
            for (int i = 1; i < line.Length; i++) sum += line[i - 1].DistanceTo(line[i]);
            return sum;
        }

        /// <summary>The point a fraction of the way along a line, set off to one side.</summary>
        public static GeoPoint Along(GeoPoint[] line, double fraction, double sideMetres = 0, bool left = true)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            var target = Length(line) * fraction;
            double run = 0;

            for (int i = 1; i < line.Length; i++)
            {
                var seg = line[i - 1].DistanceTo(line[i]);
                if (run + seg >= target || i == line.Length - 1)
                {
                    var t = seg <= 0 ? 0 : Math.Min(1, (target - run) / seg);
                    var a = line[i - 1];
                    var b = line[i];
                    var p = new GeoPoint(a.Latitude + (b.Latitude - a.Latitude) * t,
                                         a.Longitude + (b.Longitude - a.Longitude) * t);
                    if (sideMetres == 0) return p;

                    var (ax, ay) = Local(a, a.Latitude);
                    var (bx, by) = Local(b, a.Latitude);
                    var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    if (len <= 0) return p;
                    var nx = -(by - ay) / len * (left ? 1 : -1);
                    var ny = (bx - ax) / len * (left ? 1 : -1);
                    return Offset(p, nx * sideMetres, ny * sideMetres);
                }
                run += seg;
            }

            return line[line.Length - 1];
        }

        /// <summary>Distance to a polyline, plus the fraction along it of the nearest point and the side.</summary>
        public static double Nearest(GeoPoint p, GeoPoint[] line, out double fraction, out bool left)
        {
            var refLat = p.Latitude;
            var (px, py) = Local(p, refLat);
            double best = double.MaxValue, bestRun = 0, run = 0;
            left = true;

            for (int i = 1; i < line.Length; i++)
            {
                var (ax, ay) = Local(line[i - 1], refLat);
                var (bx, by) = Local(line[i], refLat);
                var dx = bx - ax;
                var dy = by - ay;
                var len2 = dx * dx + dy * dy;
                var t = len2 <= 0 ? 0 : Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / len2));
                var cx = ax + dx * t;
                var cy = ay + dy * t;
                var d = Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                var segLen = Math.Sqrt(len2);

                if (d < best)
                {
                    best = d;
                    bestRun = run + segLen * t;
                    left = dx * (py - ay) - dy * (px - ax) > 0;
                }
                run += segLen;
            }

            fraction = run <= 0 ? 0 : bestRun / run;
            return best;
        }

        public static double DistanceToLine(GeoPoint p, GeoPoint[] line) => Nearest(p, line, out _, out _);

        /// <summary>Where two streets meet, or the closest approach between them.</summary>
        public static GeoPoint ClosestApproach(GeoPoint[] a, GeoPoint[] b, out double gapMetres)
        {
            gapMetres = double.MaxValue;
            var best = a[0];

            // Sample a densely; good enough for a street pair.
            var length = Length(a);
            var steps = Math.Max(20, (int)(length / 10));
            for (int i = 0; i <= steps; i++)
            {
                var p = Along(a, (double)i / steps);
                var d = DistanceToLine(p, b);
                if (d < gapMetres)
                {
                    gapMetres = d;
                    best = p;
                }
            }

            return best;
        }

        public static bool Contains(GeoPoint[] ring, GeoPoint p)
        {
            bool inside = false;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                var a = ring[i];
                var b = ring[j];
                if ((a.Latitude > p.Latitude) != (b.Latitude > p.Latitude) &&
                    p.Longitude < (b.Longitude - a.Longitude) * (p.Latitude - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude)
                    inside = !inside;
            }
            return inside;
        }

        public static string TitleCase(string upper)
        {
            var words = upper.ToLowerInvariant().Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (w.Length == 0) continue;
                words[i] = w == "vfw" || w == "us" ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w.Substring(1);
            }
            return string.Join(" ", words);
        }
    }
}
