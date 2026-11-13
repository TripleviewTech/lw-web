using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LaTableGeo1;
using LaTableGeo2;
using LwTablePmm1;
using LwTablePmm6;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// Location validation - a port of the shared LA location code Take Call, Self
    /// Init and Traffic Stop all call (Git\Missouri\LA\Apps):
    ///
    ///   LaGeo040 ValidateLocation / FormatLocation   parsing, type detection, format
    ///   LaGeo041  Address        GEO1 range by side and parity, STR1 aliases
    ///   LaGeo042  Premise   P/   PMM1_SPACELESS
    ///   LaGeo043  Intersection   GEO1 + GEO2 at the segment ends, STR1 aliases
    ///   LaGeo044  Alarm     A/   PMM6_ROUTING_ID
    ///   LaGeo045  Common name C/ GEO1_C_NAME
    ///
    /// plus what TakeCall.cs does with the answer (TC:1943-2299): the B/ box
    /// lookup, the GEO1 areas by side, the premise's overrides and coordinates, the
    /// RUN1 routing override, the alarm company, and the premise search at the
    /// address.
    ///
    /// A single match resolves at once, exactly as each LaGeo04x ValidateLocation
    /// closes without showing itself. Anything else returns a pick list, which the
    /// console shows as the legacy window would, and then comes back through
    /// <see cref="Choose"/> or <see cref="Force"/>.
    ///
    /// Legacy defects deliberately not carried over are marked "Fix:" below.
    /// </summary>
    public sealed class LocationEngine
    {
        private readonly IGeoStore _store;
        private readonly LocationSettings _settings;

        public LocationEngine(IGeoStore store, LocationSettings settings)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? new LocationSettings();
        }

        public LocationSettings Settings => _settings;

        // ================================================================ validate

        public LocationCheck Validate(LocationRequest request)
        {
            var text = Normalize(request?.Text);
            var jur = (request?.Jurisdiction ?? string.Empty).Trim().ToUpperInvariant();
            var forced = request == null || request.Forced == ' ' || request.Forced == '\0' ? 'N' : char.ToUpperInvariant(request.Forced);

            // LaGeo040 ValidateInput.
            if (text.Length == 0) return LocationCheck.Invalid("Location must be entered.");
            if (int.TryParse(text, out _)) return LocationCheck.Invalid("Location entered is not valid.");

            // Fix: a one-character entry threw in GetLocationType's Substring(0, 2).
            if (text.Length < 2 && forced != 'X') return LocationCheck.Invalid("Street Address is invalid.");

            // TakeCall_FindBox runs before LaGeo040 is ever called.
            if (forced != 'X' && text.StartsWith("B/", StringComparison.Ordinal) && text.Length > 2)
            {
                return FindBox(text, jur);
            }

            var query = Parse(text, jur, forced, out var error);
            if (query == null) return LocationCheck.Invalid(error);

            switch (query.Kind)
            {
                case LocationKind.AsTyped:
                case LocationKind.MilePost:
                    return Finish(AsParsed(query, query.Kind == LocationKind.AsTyped ? 'X' : query.Forced), query);
            }

            // SearchForHit only runs when the entry is not forced.
            if (query.Forced == 'Y') return Finish(AsParsed(query, 'Y'), query);

            switch (query.Kind)
            {
                case LocationKind.Premise: return SearchPremise(query);
                case LocationKind.Alarm: return SearchAlarm(query);
                case LocationKind.Intersection: return SearchIntersection(query);
                case LocationKind.CommonName: return SearchCommonName(query);
                default: return SearchAddress(query);
            }
        }

        /// <summary>
        /// A row picked from a list - each LaGeo04x okToClose. For a split
        /// intersection the side comes back from the L / R question.
        /// </summary>
        public LocationCheck Choose(LocationQuery query, LocationCandidate candidate, char side = ' ')
        {
            if (query == null || candidate == null) return LocationCheck.Invalid("Select a location from the list.");

            switch (query.Kind)
            {
                case LocationKind.Premise:
                    return ResolvePremise(query, candidate.PremiseNo, string.Empty);
                case LocationKind.Alarm:
                    return ResolvePremise(query, candidate.PremiseNo, candidate.RoutingId);
                case LocationKind.Intersection:
                    return ResolveIntersection(query, candidate, side);
                case LocationKind.CommonName:
                    return ResolveCommonName(query, candidate.GeoKey, candidate.CrossName);
                case LocationKind.PremiseAtLocation:
                    return LocationCheck.Invalid("Premise lists are resolved with ChoosePremise.");
                default:
                    return ResolveAddress(query, candidate.GeoKey);
            }
        }

        /// <summary>
        /// The picker's Force button - accept what was typed with DSE1_FORCED 'Y'
        /// and no geocode, as every LaGeo04x btn_Force_Click does.
        /// </summary>
        public LocationCheck Force(LocationQuery query)
        {
            if (query == null) return LocationCheck.Invalid("Nothing to force.");

            var result = AsParsed(query, 'Y');
            if (query.Kind == LocationKind.Premise || query.Kind == LocationKind.Alarm ||
                query.Kind == LocationKind.CommonName)
            {
                // The prefix entries have no street; LaGeo040 formats them from the text.
                result.Street = string.Empty;
                result.Cross = query.Search;
                result.Formatted = query.Search;
            }

            return Finish(result, query);
        }

        // ================================================================= parsing

        /// <summary>LaGeo040 GetLocationType and the Test*Valid parsers.</summary>
        public LocationQuery Parse(string text, string jurisdiction, char forced, out string error)
        {
            error = string.Empty;
            text = Normalize(text);

            var q = new LocationQuery
            {
                Original = text,
                Jurisdiction = (jurisdiction ?? string.Empty).Trim().ToUpperInvariant(),
                Forced = forced == 'Y' || forced == 'X' ? forced : 'N'
            };

            if (q.Forced == 'X')
            {
                q.Kind = LocationKind.AsTyped;
                q.Cross = text;
                return q;
            }

            var prefix = text.Length >= 2 ? text.Substring(0, 2) : string.Empty;
            switch (prefix)
            {
                case "C/": q.Kind = LocationKind.CommonName; q.Forced = 'N'; break;
                case "A/": q.Kind = LocationKind.Alarm; q.Forced = 'N'; break;
                case "P/": q.Kind = LocationKind.Premise; q.Forced = 'N'; break;
                default:
                    q.Kind = text.Contains('/') && !text.Contains('-') ? LocationKind.Intersection : LocationKind.Address;
                    break;
            }

            if (q.Kind == LocationKind.CommonName || q.Kind == LocationKind.Alarm || q.Kind == LocationKind.Premise)
            {
                q.Search = text.Substring(2).Trim();
                q.Cross = q.Search;
                if (q.Search.Length == 0)
                {
                    error = "Enter what to search for after " + prefix + ".";
                    return null;
                }

                return q;
            }

            if (q.Kind == LocationKind.Intersection)
            {
                // TestCROSValid: a blank side becomes %, which lists every cross street.
                var parts = text.Split('/');
                q.Street = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                q.Cross = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                if (q.Street.Length == 0) q.Street = "%";
                if (q.Cross.Length == 0) q.Cross = "%";
                return q;
            }

            // TestADDRValid.
            var space = text.IndexOf(' ');
            if (space < 0)
            {
                error = "Street Address is invalid.";
                return null;
            }

            var number = text.Substring(0, space);
            if (number.Contains('-'))
            {
                var bits = number.Split('-');
                decimal.TryParse(bits[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var no);
                q.StreetNo = no;
                if (bits[1].Length > 4)
                {
                    error = "Sub-house identifier is too long.";
                    return null;
                }

                q.Sub = bits[1];
            }
            else
            {
                decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var no);
                q.StreetNo = no;
            }

            var street = text.Substring(space).Trim();
            if (street.Length == 0 || street.StartsWith("(", StringComparison.Ordinal))
            {
                error = "Street name must be entered.";
                return null;
            }

            var open = street.IndexOf('(');
            if (open >= 0)
            {
                var unit = street.Substring(open).Trim();
                if (!unit.EndsWith(")", StringComparison.Ordinal) || unit.Length < 3)
                {
                    error = "Unit number (APT, SUT, BLD) is invalid.";
                    return null;
                }

                q.Street = street.Substring(0, open).Trim();
                q.Unit = unit.TrimStart('(').TrimEnd(')').Trim();
            }
            else
            {
                q.Street = street;
            }

            // ADR2 (jurisdiction "----") and MLP1 / MLP2 (a decimal address) are not looked up.
            if (q.Jurisdiction == "----" || q.StreetNo != decimal.Truncate(q.StreetNo))
            {
                q.Kind = LocationKind.MilePost;
            }

            return q;
        }

        private static string Normalize(string text)
        {
            var t = (text ?? string.Empty).Trim().ToUpperInvariant();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t;
        }

        // ================================================================= address

        private LocationCheck SearchAddress(LocationQuery q)
        {
            var street = AliasExact(q.Street, q.Jurisdiction);
            var phrase = SearchPhrase.StartsWith(street);

            // Geo041 FindMatches: the segments whose range holds the number.
            var rows = _store.SegmentsByStreet(phrase.Prefix)
                .Where(g => phrase.Matches(g.GEO1_LOC_STR_ZIP) && IsBlank(g.GEO1_C_NAME) && g.GEO1_ADDR_TYPE != 'M')
                .Where(g => InSide(g, q.Jurisdiction, q.StreetNo, left: true) || InSide(g, q.Jurisdiction, q.StreetNo, left: false))
                .ToList();

            var exact = rows.Where(g => ExactAddress(g, q.StreetNo, q.AddressIsEven)).ToList();
            if (exact.Count == 1) return ResolveAddress(q, exact[0].GEO1_P_KEY);

            return Pick(q, "Location Validation - Address",
                        new[] { "L.Low", "L.High", "R.Low", "R.High", "Street Name", "Low Street", "High Street",
                                "L.Jur", "L.Police", "L.Fire", "L.EMS", "R.Jur", "R.Police", "R.Fire", "R.EMS" },
                        AddressList(q, street));
        }

        /// <summary>Geo041 LoadList, first attempt then the closest-street second attempt.</summary>
        private List<LocationCandidate> AddressList(LocationQuery q, string street)
        {
            var phrase = SearchPhrase.StartsWith(street);
            var even = q.AddressIsEven;

            var first = _store.SegmentsByStreet(phrase.Prefix)
                .Where(g => phrase.Matches(g.GEO1_LOC_STR_ZIP) && IsBlank(g.GEO1_C_NAME) && g.GEO1_ADDR_TYPE != 'M')
                .Where(g =>
                    (JurOk(g.GEO1_LOC_JUR_L, q.Jurisdiction) && Between(q.StreetNo, g.GEO1_LOC_ADD_LO_L, g.GEO1_LOC_ADD_HI_L) &&
                     IsEven(g.GEO1_LOC_ADD_LO_L) == even && g.GEO1_LOC_ADD_HI_L > 0) ||
                    (JurOk(g.GEO1_LOC_JUR_R, q.Jurisdiction) && Between(q.StreetNo, g.GEO1_LOC_ADD_LO_R, g.GEO1_LOC_ADD_HI_R) &&
                     IsEven(g.GEO1_LOC_ADD_LO_R) == even && g.GEO1_LOC_ADD_LO_R > 0))
                .ToList();

            if (first.Count == 0)
            {
                var closest = ClosestStreet(street);
                first = _store.SegmentsFromStreet(closest, _settings.ListLimit)
                    .Where(g => IsBlank(g.GEO1_C_NAME) && g.GEO1_ADDR_TYPE != 'M')
                    .Where(g => q.Jurisdiction.Length == 0 ||
                                JurOk(g.GEO1_LOC_JUR_L, q.Jurisdiction) || JurOk(g.GEO1_LOC_JUR_R, q.Jurisdiction))
                    .ToList();
            }

            var ordered = first.OrderBy(g => g.GEO1_LOC_STR).ThenBy(g => g.GEO1_LOC_ADD_LO_L).Take(_settings.ListLimit).ToList();
            var ends = _store.CrossStreets(ordered.Select(g => g.GEO1_P_KEY)).ToLookup(x => x.GEO2_P_KEY);

            return ordered.Select(g => new LocationCandidate
            {
                GeoKey = g.GEO1_P_KEY,
                Cells = new[]
                {
                    Whole(g.GEO1_LOC_ADD_LO_L), Whole(g.GEO1_LOC_ADD_HI_L), Whole(g.GEO1_LOC_ADD_LO_R), Whole(g.GEO1_LOC_ADD_HI_R),
                    T(g.GEO1_LOC_STR),
                    T(ends[g.GEO1_P_KEY].FirstOrDefault(x => x.GEO2_TYPE == 'L')?.GEO2_NAME),
                    T(ends[g.GEO1_P_KEY].FirstOrDefault(x => x.GEO2_TYPE == 'H')?.GEO2_NAME),
                    T(g.GEO1_LOC_JUR_L), T(g.GEO1_POLICE_1L), T(g.GEO1_FIRE_1L), T(g.GEO1_EMS_1L),
                    T(g.GEO1_LOC_JUR_R), T(g.GEO1_POLICE_1R), T(g.GEO1_FIRE_1R), T(g.GEO1_EMS_1R)
                }
            }).ToList();
        }

        /// <summary>Geo041 okToClose: the segment, its side by parity, forced when outside the range.</summary>
        private LocationCheck ResolveAddress(LocationQuery q, int geoKey)
        {
            var geo = _store.Segment(geoKey);
            if (geo == null) return LocationCheck.Invalid("Unable to access GeoCode record.");

            var result = AsParsed(q, 'N');
            result.GeoKey = geo.GEO1_P_KEY;
            result.Street = T(geo.GEO1_LOC_STR);
            ApplySide(result, geo, q.StreetNo);

            return Finish(result, q);
        }

        /// <summary>
        /// Geo041's exact-match count, which decides the side from the parity of the
        /// number against the parity of each side's range start.
        /// </summary>
        private static bool ExactAddress(GEO1 g, decimal addr, bool even)
        {
            bool Left() => addr >= g.GEO1_LOC_ADD_LO_L && addr <= g.GEO1_LOC_ADD_HI_L;
            bool Right() => addr >= g.GEO1_LOC_ADD_LO_R && addr <= g.GEO1_LOC_ADD_HI_R;

            if (!even) return IsEven(g.GEO1_LOC_ADD_LO_L) ? Right() : Left();

            if (g.GEO1_LOC_ADD_LO_L > 0 || g.GEO1_LOC_ADD_HI_L > 0)
            {
                if (IsEven(g.GEO1_LOC_ADD_LO_L)) return Left();
                return (g.GEO1_LOC_ADD_LO_R > 0 || g.GEO1_LOC_ADD_HI_R > 0) && Right();
            }

            return (g.GEO1_LOC_ADD_LO_R > 0 || g.GEO1_LOC_ADD_HI_R > 0) && IsEven(g.GEO1_LOC_ADD_LO_R) && Right();
        }

        private static bool InSide(GEO1 g, string jur, decimal addr, bool left) =>
            left
                ? JurOk(g.GEO1_LOC_JUR_L, jur) && Between(addr, g.GEO1_LOC_ADD_LO_L, g.GEO1_LOC_ADD_HI_L)
                : JurOk(g.GEO1_LOC_JUR_R, jur) && Between(addr, g.GEO1_LOC_ADD_LO_R, g.GEO1_LOC_ADD_HI_R);

        /// <summary>
        /// The side test Geo041, Geo042 and Geo044 share: the address is on the left
        /// when its parity matches the parity of the left range's low number, and it
        /// is forced ('Y') when it falls outside that side's range.
        /// </summary>
        private static void ApplySide(LocationResult result, GEO1 geo, decimal addr)
        {
            var left = IsEven(geo.GEO1_LOC_ADD_LO_L) == IsEven(addr);

            if (left)
            {
                result.Side = 'L';
                result.Jurisdiction = T(geo.GEO1_LOC_JUR_L);
                result.Forced = addr < geo.GEO1_LOC_ADD_LO_L || addr > geo.GEO1_LOC_ADD_HI_L ? 'Y' : 'N';
            }
            else
            {
                result.Side = 'R';
                result.Jurisdiction = T(geo.GEO1_LOC_JUR_R);
                result.Forced = addr < geo.GEO1_LOC_ADD_LO_R || addr > geo.GEO1_LOC_ADD_HI_R ? 'Y' : 'N';
            }
        }

        /// <summary>Geo041 FindMatches' STR1 alias: an exact ASORT match renames the street.</summary>
        private string AliasExact(string street, string jur)
        {
            var hit = _store.StreetNames(SearchPhrase.Squash(street), exact: true, jur).LastOrDefault();
            return hit == null || IsBlank(hit.STR1_STR_NM) ? street : T(hit.STR1_STR_NM);
        }

        /// <summary>Geo043 MatchFoundForStreet: one exact alias keeps the street, a single match renames it.</summary>
        private string AliasLike(string street, string jur)
        {
            if (street == "%") return street;

            var hits = _store.StreetNames(SearchPhrase.Squash(street).Replace("%", string.Empty), exact: false, jur);
            if (hits.Count(h => string.Equals(T(h.STR1_STR_NM), street, StringComparison.OrdinalIgnoreCase)) == 1) return street;
            if (hits.Count == 1) return T(hits[0].STR1_STR_NM);
            return street;
        }

        /// <summary>Geo041 GetClosestStreetMatch: shorten the street until some segment starts with it.</summary>
        private string ClosestStreet(string street)
        {
            var s = SearchPhrase.Squash(street).Replace("'", string.Empty).Replace("%", string.Empty);
            for (var i = s.Length; i > 0; i--)
            {
                if (_store.AnySegmentStreet(s.Substring(0, i))) return s.Substring(0, i);
            }

            return string.Empty;
        }

        // ================================================================ premise

        private LocationCheck SearchPremise(LocationQuery q)
        {
            var search = SearchPhrase.Squash(q.Search);

            // Geo042 FindMatches: exactly one active premise starting with the text.
            if (search != "?")
            {
                var phrase = new SearchPhrase(search + "%");
                var matches = _store.PremisesByName(phrase.Prefix)
                    .Where(p => p.PMM1_INACTIVE != 'Y' && phrase.Matches(p.PMM1_SPACELESS)).ToList();
                if (matches.Count == 1) return ResolvePremise(q, matches[0].PMM1_FOP, string.Empty);
            }

            return Pick(q, "Location Validation - Premise",
                        new[] { "Premise Name", "Location", "Jurisdiction" }, PremiseList(search));
        }

        /// <summary>Geo042 LoadList: the premises matching, else everything on and after the text.</summary>
        private List<LocationCandidate> PremiseList(string search)
        {
            List<PMM1> rows;

            if (search == "?" || search.Length == 0)
            {
                rows = _store.PremisesFromName(string.Empty, _settings.ListLimit).ToList();
            }
            else
            {
                var phrase = new SearchPhrase(search + "%");
                rows = _store.PremisesByName(phrase.Prefix).Where(p => phrase.Matches(p.PMM1_SPACELESS)).ToList();
                if (rows.Count == 0) rows = _store.PremisesFromName(search, _settings.ListLimit).ToList();
            }

            return rows.Where(p => p.PMM1_INACTIVE != 'Y')
                .OrderBy(p => p.PMM1_SPACELESS)
                .Take(_settings.ListLimit)
                .Select(p => new LocationCandidate
                {
                    PremiseNo = p.PMM1_FOP,
                    Cells = new[] { PremiseListName(p), T(p.PMM1_LOC_FORMAT), T(p.PMM1_LOC_JUR) }
                }).ToList();
        }

        /// <summary>
        /// Geo042 / Geo044 okToClose: the premise's location, and - when it has a
        /// geocode - the segment's side and jurisdiction.
        /// </summary>
        private LocationCheck ResolvePremise(LocationQuery q, int fop, string routingId)
        {
            var pmm = _store.Premise(fop);
            if (pmm == null) return LocationCheck.Invalid("Premise record could not be accessed.");

            var result = new LocationResult
            {
                Kind = q.Kind,
                PremiseNo = pmm.PMM1_FOP,
                GeoKey = pmm.PMM1_GEO_NO,
                Jurisdiction = T(pmm.PMM1_LOC_JUR),
                Street = T(pmm.PMM1_LOC_STR),
                StreetNo = pmm.PMM1_LOC_ADDR,
                Sub = T(pmm.PMM1_LOC_SUB_NO),
                Unit = T(pmm.PMM1_LOC_UNI_NO),
                Cross = T(pmm.PMM1_LOC_X_ST),
                RoutingId = (routingId ?? string.Empty).Trim(),
                Forced = 'N'
            };

            if (result.GeoKey > 0)
            {
                var geo = _store.Segment(result.GeoKey);
                if (geo != null)
                {
                    ApplySide(result, geo, result.StreetNo);
                }
                else
                {
                    // Fix: Geo042 refused the premise outright when its geocode had been
                    // deleted ("Sometimes they delete Geocode records"), leaving the
                    // operator unable to pick it. It is taken without a geocode instead.
                    result.GeoKey = 0;
                    result.Forced = 'Y';
                }
            }

            return Finish(result, q);
        }

        private static string PremiseListName(PMM1 p) =>
            !IsBlank(p.PMM1_NAME_F_M) ? T(p.PMM1_NAME_L) + ", " + T(p.PMM1_NAME_F_M) : T(p.PMM1_NAME_L);

        // ================================================================== alarm

        private LocationCheck SearchAlarm(LocationQuery q)
        {
            var id = q.Search.Trim();

            // Geo044 FindMatches: the routing id, counting active premises only.
            if (id != "?")
            {
                var hits = _store.Alarms(id, exact: true)
                    .Select(a => (Alarm: a, Premise: _store.Premise(a.PMM6_FOP)))
                    .Where(x => x.Premise != null && x.Premise.PMM1_INACTIVE != 'Y')
                    .ToList();

                if (hits.Count == 1) return ResolvePremise(q, hits[0].Alarm.PMM6_FOP, hits[0].Alarm.PMM6_ROUTING_ID);
            }

            // Geo044's second form ("Brookline dual entry": number plus a type letter
            // against PMM1_FIRE_BOX / PERM_TYPE) is not carried: LW's PMM1 has neither column.

            return Pick(q, "Location Validation - Alarm Number",
                        new[] { "Routing Id", "Premise Name", "Location", "Jurisdiction", "Active", "Response" },
                        AlarmList(id));
        }

        private List<LocationCandidate> AlarmList(string id)
        {
            List<PMM6> rows;
            if (id == "?" || id.Length == 0)
            {
                rows = _store.AlarmsFrom(string.Empty, _settings.ListLimit).ToList();
            }
            else
            {
                rows = _store.Alarms(id, exact: false).ToList();
                if (rows.Count == 0) rows = _store.AlarmsFrom(id, _settings.ListLimit).ToList();
            }

            var list = new List<LocationCandidate>();
            foreach (var a in rows.OrderBy(a => a.PMM6_ROUTING_ID).Take(_settings.ListLimit))
            {
                var p = _store.Premise(a.PMM6_FOP);
                if (p == null) continue;

                list.Add(new LocationCandidate
                {
                    PremiseNo = a.PMM6_FOP,
                    RoutingId = T(a.PMM6_ROUTING_ID),
                    Cells = new[]
                    {
                        T(a.PMM6_ROUTING_ID), PremiseListName(p), T(p.PMM1_LOC_FORMAT), T(p.PMM1_LOC_JUR),
                        a.PMM6_INACTIVE == 'Y' || p.PMM1_INACTIVE == 'Y' ? "N" : "Y",
                        a.PMM6_RESPONSE == ' ' ? string.Empty : a.PMM6_RESPONSE.ToString()
                    }
                });
            }

            return list;
        }

        // ============================================================= intersection

        private LocationCheck SearchIntersection(LocationQuery q)
        {
            var s1 = AliasLike(q.Street, q.Jurisdiction);
            var s2 = AliasLike(q.Cross, q.Jurisdiction);

            // Geo043 GeocodeRecordFound: exactly one distinct corner, in either order.
            var corners = Corners(s1, s2, q.Jurisdiction, bothOrders: true, crossFirst: true);
            var groups = corners.GroupBy(c => c.Group).ToList();
            if (groups.Count == 1)
            {
                var only = groups[0].Last();
                return ResolveIntersection(q, only.Candidate, ' ');
            }

            // Geo043 LoadList: as typed, then the aliases both ways, then without the jurisdiction.
            var rows = Corners(q.Street, q.Cross, q.Jurisdiction, bothOrders: false, crossFirst: false);
            if (rows.Count == 0) rows = Corners(s1, s2, q.Jurisdiction, bothOrders: true, crossFirst: false);
            if (rows.Count == 0 && q.Jurisdiction.Length > 0) rows = Corners(s1, s2, string.Empty, bothOrders: true, crossFirst: false);

            var list = rows.GroupBy(r => r.Group)
                .Select(g => g.First().Candidate)
                .OrderBy(c => c.Cells[0]).ThenBy(c => c.Cells[1])
                .Take(_settings.ListLimit)
                .ToList();

            return Pick(q, "Location Validation - Intersection",
                        new[] { "Primary Street", "Cross Street", "L.Jur", "L.Police", "L.Fire", "L.EMS",
                                "R.Jur", "R.Police", "R.Fire", "R.EMS" }, list);
        }

        private sealed class Corner
        {
            public string Group;
            public LocationCandidate Candidate;
        }

        /// <summary>
        /// GEO1 segments meeting a GEO2 cross street at an intersection point
        /// (GEO1_INT_PT_HI with GEO2_TYPE 'H', or _LO with 'L'). crossFirst matches
        /// the first street against GEO2, as GeocodeRecordFound does; the list
        /// matches it against GEO1.
        /// </summary>
        private List<Corner> Corners(string s1, string s2, string jur, bool bothOrders, bool crossFirst)
        {
            var result = new List<Corner>();

            void Add(string segmentStreet, string crossStreet)
            {
                var segPhrase = SearchPhrase.StartsWith(segmentStreet);
                var crossPhrase = SearchPhrase.StartsWith(crossStreet);

                var ends = _store.CrossStreetsByName(crossPhrase.Prefix)
                    .Where(x => crossPhrase.Matches(x.GEO2_NAME_ZIP)).ToList();
                if (ends.Count == 0) return;

                var segments = _store.Segments(ends.Select(e => e.GEO2_P_KEY).Distinct())
                    .Where(g => segPhrase.Matches(g.GEO1_LOC_STR_ZIP) && IsBlank(g.GEO1_C_NAME))
                    .Where(g => jur.Length == 0 || JurOk(g.GEO1_LOC_JUR_L, jur) || JurOk(g.GEO1_LOC_JUR_R, jur))
                    .ToDictionary(g => g.GEO1_P_KEY);

                foreach (var e in ends)
                {
                    if (!segments.TryGetValue(e.GEO2_P_KEY, out var g)) continue;
                    var atPoint = (g.GEO1_INT_PT_HI == 'Y' && e.GEO2_TYPE == 'H') || (g.GEO1_INT_PT_LO == 'Y' && e.GEO2_TYPE == 'L');
                    if (!atPoint) continue;

                    result.Add(new Corner
                    {
                        Group = string.Join("|", T(g.GEO1_LOC_JUR_L), T(g.GEO1_LOC_JUR_R), T(g.GEO1_LOC_STR), T(e.GEO2_NAME),
                                            T(g.GEO1_POLICE_1L), T(g.GEO1_FIRE_1L), T(g.GEO1_POLICE_1R), T(g.GEO1_FIRE_1R)),
                        Candidate = new LocationCandidate
                        {
                            GeoKey = g.GEO1_P_KEY,
                            CrossName = T(e.GEO2_NAME),
                            CrossType = e.GEO2_TYPE,
                            Cells = new[]
                            {
                                T(g.GEO1_LOC_STR), T(e.GEO2_NAME),
                                T(g.GEO1_LOC_JUR_L), T(g.GEO1_POLICE_1L), T(g.GEO1_FIRE_1L), T(g.GEO1_EMS_1L),
                                T(g.GEO1_LOC_JUR_R), T(g.GEO1_POLICE_1R), T(g.GEO1_FIRE_1R), T(g.GEO1_EMS_1R)
                            }
                        }
                    });
                }
            }

            if (crossFirst)
            {
                Add(s2, s1);
                if (bothOrders) Add(s1, s2);
            }
            else
            {
                Add(s1, s2);
                if (bothOrders) Add(s2, s1);
            }

            return result;
        }

        /// <summary>
        /// Geo043 okToClose. A plain segment (TYPE_CODE 0), or a split one with a
        /// default side, takes that side; any other split segment asks L or R.
        /// </summary>
        private LocationCheck ResolveIntersection(LocationQuery q, LocationCandidate c, char side)
        {
            var geo = _store.Segment(c.GeoKey);
            if (geo == null) return LocationCheck.Invalid("Unable to access GeoCode record.");

            if (side != 'L' && side != 'R')
            {
                if (geo.GEO1_TYPE_CODE == 0 || (geo.GEO1_TYPE_CODE == 1 && geo.GEO1_DFLT_SIDE != ' ' && geo.GEO1_DFLT_SIDE != '\0'))
                {
                    side = geo.GEO1_DFLT_SIDE == 'R' ? 'R' : 'L';
                }
                else
                {
                    return new LocationCheck
                    {
                        Status = LocationStatus.ChooseSide,
                        Pending = c,
                        Pick = new LocationPickList { Kind = q.Kind, Query = q, Title = "Split Record: Please select Street Side" },
                        Message = "Split Record: Please select Street Side"
                    };
                }
            }

            var result = new LocationResult
            {
                Kind = LocationKind.Intersection,
                GeoKey = geo.GEO1_P_KEY,
                Street = T(geo.GEO1_LOC_STR),
                Cross = c.CrossName,
                Side = side,
                Jurisdiction = side == 'L' ? T(geo.GEO1_LOC_JUR_L) : T(geo.GEO1_LOC_JUR_R),
                Forced = 'N'
            };

            return Finish(result, q);
        }

        // ============================================================ common name

        private LocationCheck SearchCommonName(LocationQuery q)
        {
            var rows = CommonNames(q.Search, q.Jurisdiction);
            if (rows.Count == 1) return ResolveCommonName(q, rows[0].GeoKey, rows[0].CrossName);

            return Pick(q, "Location Validation - Common Name",
                        new[] { "Common Name", "Location", "L.Jur", "R.Jur", "L.Police", "R.Police" }, rows);
        }

        /// <summary>
        /// Geo045: segments by common name - address ranges, plus the named
        /// intersections (no range) with the GEO2 cross street they meet.
        /// Fix: Geo045's jurisdiction branches were inverted, so a jurisdiction
        /// filtered nothing and a blank one threw. It filters when given here.
        /// </summary>
        private List<LocationCandidate> CommonNames(string search, string jur)
        {
            var phrase = search == "?" ? new SearchPhrase("%") : SearchPhrase.StartsWith(search);
            var segs = _store.SegmentsByCommonName(phrase.Prefix)
                .Where(g => phrase.Matches(g.GEO1_C_NAME_ZIP))
                .Where(g => jur.Length == 0 || JurOk(g.GEO1_LOC_JUR_L, jur) || JurOk(g.GEO1_LOC_JUR_R, jur))
                .Take(_settings.ListLimit)
                .ToList();

            var ends = _store.CrossStreets(segs.Select(g => g.GEO1_P_KEY)).ToLookup(x => x.GEO2_P_KEY);

            return segs.Select(g =>
            {
                var isCorner = g.GEO1_LOC_ADD_HI_L == 0 && g.GEO1_LOC_ADD_HI_R == 0;
                var cross = isCorner ? T(ends[g.GEO1_P_KEY].FirstOrDefault()?.GEO2_NAME) : string.Empty;
                var addr = g.GEO1_LOC_ADD_HI_L > 0 ? g.GEO1_LOC_ADD_HI_L : g.GEO1_LOC_ADD_HI_R;

                return new LocationCandidate
                {
                    GeoKey = g.GEO1_P_KEY,
                    CrossName = cross,
                    Cells = new[]
                    {
                        T(g.GEO1_C_NAME),
                        LocationFormatter.Format(isCorner ? 0 : addr, T(g.GEO1_LOC_STR), string.Empty, string.Empty, cross),
                        T(g.GEO1_LOC_JUR_L), T(g.GEO1_LOC_JUR_R), T(g.GEO1_POLICE_1L), T(g.GEO1_POLICE_1R)
                    }
                };
            }).ToList();
        }

        /// <summary>Geo045 okToClose: the named segment, left side, its high number as the address.</summary>
        private LocationCheck ResolveCommonName(LocationQuery q, int geoKey, string cross)
        {
            var geo = _store.Segment(geoKey);
            if (geo == null) return LocationCheck.Invalid("Unable to access GeoCode record.");

            var isCorner = geo.GEO1_LOC_ADD_HI_L == 0 && geo.GEO1_LOC_ADD_HI_R == 0;
            var result = new LocationResult
            {
                Kind = LocationKind.CommonName,
                GeoKey = geo.GEO1_P_KEY,
                Street = T(geo.GEO1_LOC_STR),
                Cross = isCorner ? cross : string.Empty,
                StreetNo = isCorner ? 0 : (geo.GEO1_LOC_ADD_HI_L > 0 ? geo.GEO1_LOC_ADD_HI_L : geo.GEO1_LOC_ADD_HI_R),
                Side = geo.GEO1_LOC_ADD_HI_L > 0 || isCorner ? 'L' : 'R',
                Forced = 'N'
            };
            result.Jurisdiction = result.Side == 'L' ? T(geo.GEO1_LOC_JUR_L) : T(geo.GEO1_LOC_JUR_R);

            return Finish(result, q);
        }

        // ==================================================================== box

        /// <summary>
        /// TakeCall_FindBox (TC:2301-2418). A box is a PMM6 routing id - the alarm's
        /// premise, which must be active - or else a RUN1 run number, whose location
        /// is then validated like any other entry.
        /// Fix: the legacy stripped "B/" with TrimStart("B/".ToCharArray()), which
        /// also ate a box number's own leading B's.
        /// </summary>
        private LocationCheck FindBox(string text, string jur)
        {
            var box = text.Substring(2).Trim();

            var alarm = _store.Alarms(box, exact: true).FirstOrDefault();
            if (alarm == null)
            {
                var run = _store.RunCard(box);
                if (run == null || (IsBlank(run.RUN1_LOC_ST_NAM) && IsBlank(run.RUN1_LOC_X_ST)))
                {
                    return LocationCheck.Invalid("Box record could not be found.");
                }

                var locText = !IsBlank(run.RUN1_LOC_X_ST)
                    ? T(run.RUN1_LOC_ST_NAM) + " / " + T(run.RUN1_LOC_X_ST)
                    : Whole(run.RUN1_LOC_ST_NO) + " " + T(run.RUN1_LOC_ST_NAM);

                var check = Validate(new LocationRequest
                {
                    Text = locText,
                    Jurisdiction = IsBlank(run.RUN1_LOC_JUR) ? jur : T(run.RUN1_LOC_JUR),
                    Forced = 'N'
                });

                var notice = "Box " + box + " from run card " + T(run.RUN1_RUN_NO) + ".";
                if (check.Result != null)
                {
                    check.Result.CallCode = run.RUN1_CALL_CODE;
                    check.Result.Notice = notice;
                }

                // Still to be picked: the box's call code rides on the query to Finish.
                if (check.Pick?.Query != null)
                {
                    check.Pick.Query.BoxCallCode = run.RUN1_CALL_CODE;
                    check.Pick.Query.BoxNotice = notice;
                }

                return check;
            }

            if (alarm.PMM6_INACTIVE == 'Y') return LocationCheck.Invalid("Box routing id is inactive.");

            var pmm = _store.Premise(alarm.PMM6_FOP);
            if (pmm == null) return LocationCheck.Invalid("Premise record could not be accessed.");
            if (pmm.PMM1_INACTIVE == 'Y') return LocationCheck.Invalid("Premise is set as inactive.");

            var q = new LocationQuery { Kind = LocationKind.Box, Original = text, Jurisdiction = jur, Search = box };
            return ResolvePremise(q, pmm.PMM1_FOP, T(alarm.PMM6_ROUTING_ID));
        }

        // ============================================================= finishing

        private static LocationResult AsParsed(LocationQuery q, char forced) => new LocationResult
        {
            Kind = q.Kind,
            Jurisdiction = q.Jurisdiction,
            Street = q.Kind == LocationKind.AsTyped ? string.Empty : q.Street,
            StreetNo = q.StreetNo,
            Sub = q.Sub,
            Unit = q.Unit,
            Cross = q.Kind == LocationKind.AsTyped ? q.Original : q.Cross,
            Forced = forced
        };

        /// <summary>
        /// What TakeCall_ValidateLocation does after LaGeo040 answers (TC:2061-2250):
        /// premise kept or dropped, GEO1 areas by side, the premise's overrides and
        /// coordinates, the RUN1 routing override, the alarm company, the default
        /// jurisdiction, the premise search at the address, and the display text.
        /// </summary>
        private LocationCheck Finish(LocationResult r, LocationQuery q)
        {
            // Intersections go into DSE1 as street / cross with no number.
            if (r.Kind == LocationKind.Intersection) r.StreetNo = 0;

            // DSE1_LOC_X_ST is 40 wide (TC: PadRight(40).Substring(0, 40)).
            if (r.Cross.Length > 40) r.Cross = r.Cross.Substring(0, 40);

            // The premise is kept for P/ A/ and B/, or when CAD7_PREM_SW attaches premises.
            // Fix: a B/ box's premise was dropped unless PREM_SW was on - it is the premise
            // the box belongs to, and the run card and hazards hang off it.
            var keepsPremise = r.Kind == LocationKind.Premise || r.Kind == LocationKind.Alarm || r.Kind == LocationKind.Box;
            if (!keepsPremise && !_settings.AttachSinglePremise) r.PremiseNo = 0;

            if (r.GeoKey > 0)
            {
                var geo = _store.Segment(r.GeoKey);
                if (geo != null)
                {
                    // Fix: the legacy took the right side's areas whenever the side was
                    // unknown (a box). The side is worked out from the number instead.
                    if (r.Side != 'L' && r.Side != 'R') r.Side = IsEven(geo.GEO1_LOC_ADD_LO_L) == IsEven(r.StreetNo) ? 'L' : 'R';
                    ApplyAreas(r, geo);
                }
            }

            if (r.PremiseNo > 0)
            {
                var pmm = _store.Premise(r.PremiseNo);
                if (pmm != null)
                {
                    if (!IsBlank(pmm.PMM1_POLICE)) r.Police1 = T(pmm.PMM1_POLICE);
                    if (!IsBlank(pmm.PMM1_FIRE)) r.Fire1 = T(pmm.PMM1_FIRE);
                    if (!IsBlank(pmm.PMM1_EMS)) r.Ems1 = T(pmm.PMM1_EMS);
                    if (!IsBlank(pmm.PMM1_OTHER)) r.Other1 = T(pmm.PMM1_OTHER);
                    if (!IsBlank(pmm.PMM1_TOW_ZONE)) r.Tow = T(pmm.PMM1_TOW_ZONE);
                    if (!IsBlank(pmm.PMM1_LOC_JUR)) r.Jurisdiction = T(pmm.PMM1_LOC_JUR);
                }

                var xy = _store.PremiseCoordinates(r.PremiseNo);
                if (xy != null && xy.Value.Lat != 0)
                {
                    r.X = xy.Value.Lat;
                    r.Y = xy.Value.Long;
                }

                r.PremiseHazard = _store.HazardCount(r.PremiseNo) > 0;
            }

            // DSE1_ROUTING_ID: the coverage '4' run card may move the location.
            if (r.RoutingId.Length > 0)
            {
                var run = _store.RunCard(r.RoutingId, '4');
                if (run != null)
                {
                    if (!IsBlank(run.RUN1_LOC_JUR)) r.Jurisdiction = T(run.RUN1_LOC_JUR);
                    if (!IsBlank(run.RUN1_LOC_X_ST))
                    {
                        r.Street = T(run.RUN1_LOC_ST_NAM);
                        r.Cross = T(run.RUN1_LOC_X_ST);
                    }
                    else if (run.RUN1_LOC_ST_NO > 0)
                    {
                        r.Street = T(run.RUN1_LOC_ST_NAM);
                        r.StreetNo = run.RUN1_LOC_ST_NO;
                    }
                }

                // TakeCall_LoadAlarmInfo: the alarm's call code and the alarm company.
                LoadAlarm(r);
            }

            if (r.Jurisdiction.Length == 0) r.Jurisdiction = _settings.DefaultJurisdiction;

            if (q.BoxCallCode > 0 && r.CallCode == 0) r.CallCode = q.BoxCallCode;
            if (q.BoxNotice.Length > 0 && r.Notice.Length == 0) r.Notice = q.BoxNotice;

            // ValidateLocationPassed: an address with no premise looks for one there.
            if (r.StreetNo > 0 && r.PremiseNo == 0) SearchForPremise(r);

            r.Formatted = r.Kind == LocationKind.AsTyped
                ? q.Original
                : LocationFormatter.Format(r.StreetNo, r.Street, r.Sub, r.Unit, r.Cross);

            SetPremiseName(r);
            return LocationCheck.Resolved(r);
        }

        private static void ApplyAreas(LocationResult r, GEO1 g)
        {
            var left = r.Side == 'L';
            r.Police1 = T(left ? g.GEO1_POLICE_1L : g.GEO1_POLICE_1R);
            r.Police2 = T(left ? g.GEO1_POLICE_2L : g.GEO1_POLICE_2R);
            r.Fire1 = T(left ? g.GEO1_FIRE_1L : g.GEO1_FIRE_1R);
            r.Fire2 = T(left ? g.GEO1_FIRE_2L : g.GEO1_FIRE_2R);
            r.Ems1 = T(left ? g.GEO1_EMS_1L : g.GEO1_EMS_1R);
            r.Ems2 = T(left ? g.GEO1_EMS_2L : g.GEO1_EMS_2R);
            r.Other1 = T(left ? g.GEO1_OTHER_1L : g.GEO1_OTHER_1R);
            r.Other2 = T(left ? g.GEO1_OTHER_2L : g.GEO1_OTHER_2R);
            r.Tow = T(left ? g.GEO1_TOW_L : g.GEO1_TOW_R);
            var jur = T(left ? g.GEO1_LOC_JUR_L : g.GEO1_LOC_JUR_R);
            if (jur.Length > 0) r.Jurisdiction = jur;
            r.CampusId = g.GEO1_CAMPUS_ID;
        }

        private void LoadAlarm(LocationResult r)
        {
            var alarm = _store.Alarms(r.RoutingId, exact: true).FirstOrDefault();
            if (alarm == null || alarm.PMM6_INACTIVE == 'Y') return;

            var pmm = _store.Premise(alarm.PMM6_FOP);
            if (pmm == null || pmm.PMM1_INACTIVE == 'Y') return;

            if (alarm.PMM6_CALL_CODE > 0) r.CallCode = alarm.PMM6_CALL_CODE;

            var company = alarm.PMM6_ALC_CO_NO > 0 ? _store.AlarmCompany(alarm.PMM6_ALC_CO_NO) : null;
            if (company == null) return;

            var digits = (T(company.ALC1_PHONE_2)).PadLeft(7, '0');
            r.Contact = new AlarmContact
            {
                Name = T(company.ALC1_CO_NM),
                Address = T(company.ALC1_ADDR_1),
                City = T(company.ALC1_CITY),
                State = T(company.ALC1_STATE),
                Phone = IsBlank(company.ALC1_PHONE_2) ? string.Empty
                    : T(company.ALC1_PHONE_1) + "-" + digits.Substring(0, 3) + "-" + digits.Substring(3, 4)
            };
        }

        /// <summary>
        /// TakeCall SearchForPremise (TC:2664-2802): active premises at the address
        /// (with sub and unit when given, else the number alone), or at the corner.
        /// One is attached when CAD7_PREM_SW is on; two or more is "Multiple Premises".
        /// </summary>
        private void SearchForPremise(LocationResult r)
        {
            var found = new List<PMM1>();

            if (r.StreetNo > 0)
            {
                var at = _store.PremisesAt(r.Jurisdiction, r.Street, r.StreetNo);
                if (r.Sub.Length > 0 || r.Unit.Length > 0)
                {
                    found = at.Where(p => T(p.PMM1_LOC_SUB_NO) == r.Sub && T(p.PMM1_LOC_UNI_NO) == r.Unit).Take(2).ToList();
                }

                if (found.Count == 0) found = at.Take(2).ToList();
            }

            if (found.Count == 0 && r.Cross.Length > 0)
            {
                found = _store.PremisesAtCorner(r.Jurisdiction, r.Street, r.Cross).Take(2).ToList();
            }

            if (found.Count == 0) return;

            if (found.Count > 1)
            {
                r.MultiplePremises = true;
                return;
            }

            if (_settings.AttachSinglePremise) r.PremiseNo = found[0].PMM1_FOP;
            else r.PremiseAvailable = true;
        }

        /// <summary>TakeCall_SetLocationPassed: PMM1 name, else GEO1 common name, else subdivision.</summary>
        private void SetPremiseName(LocationResult r)
        {
            if (r.PremiseNo > 0)
            {
                r.PremiseName = _store.PremiseName(r.PremiseNo);
                r.PremiseHazard = _store.HazardCount(r.PremiseNo) > 0;
            }
            else if (r.MultiplePremises)
            {
                r.PremiseName = "Multiple Premises";
            }

            if (r.GeoKey > 0)
            {
                var geo = _store.Segment(r.GeoKey);
                if (geo != null)
                {
                    r.CommonName = T(geo.GEO1_C_NAME);
                    if (r.PremiseName.Length == 0) r.PremiseName = !IsBlank(geo.GEO1_C_NAME) ? T(geo.GEO1_C_NAME) : T(geo.GEO1_SUB_NM);
                }
            }
        }

        // ======================================================== premise list

        /// <summary>
        /// The premises at a validated location - the list LwPmm905 shows behind
        /// the "Multiple Premises" button.
        /// </summary>
        public LocationPickList PremisesAt(LocationResult location)
        {
            if (location == null) return null;

            var rows = new List<PMM1>();
            if (location.StreetNo > 0) rows.AddRange(_store.PremisesAt(location.Jurisdiction, location.Street, location.StreetNo));
            if (location.Cross.Length > 0) rows.AddRange(_store.PremisesAtCorner(location.Jurisdiction, location.Street, location.Cross));

            return new LocationPickList
            {
                Kind = LocationKind.PremiseAtLocation,
                Title = "Premises at " + location.Formatted,
                Columns = new[] { "Premise Name", "Location", "Sub", "Unit", "Jurisdiction" },
                Query = new LocationQuery { Kind = LocationKind.PremiseAtLocation, Original = location.Formatted },
                Rows = rows.GroupBy(p => p.PMM1_FOP).Select(g => g.First())
                    .OrderBy(p => p.PMM1_SPACELESS)
                    .Select(p => new LocationCandidate
                    {
                        PremiseNo = p.PMM1_FOP,
                        Cells = new[] { PremiseListName(p), T(p.PMM1_LOC_FORMAT), T(p.PMM1_LOC_SUB_NO), T(p.PMM1_LOC_UNI_NO), T(p.PMM1_LOC_JUR) }
                    }).ToList()
            };
        }

        /// <summary>
        /// TakeCall_ListPremises: the picked premise is attached, its sub and unit
        /// replace the entry's, its areas override, and the location is re-formatted.
        /// </summary>
        public LocationResult ChoosePremise(LocationResult location, int fop)
        {
            var pmm = _store.Premise(fop);
            if (location == null || pmm == null) return location;

            location.PremiseNo = pmm.PMM1_FOP;
            location.MultiplePremises = false;
            location.PremiseAvailable = false;
            location.Sub = T(pmm.PMM1_LOC_SUB_NO);
            location.Unit = T(pmm.PMM1_LOC_UNI_NO);
            if (!IsBlank(pmm.PMM1_POLICE)) location.Police1 = T(pmm.PMM1_POLICE);
            if (!IsBlank(pmm.PMM1_FIRE)) location.Fire1 = T(pmm.PMM1_FIRE);
            if (!IsBlank(pmm.PMM1_EMS)) location.Ems1 = T(pmm.PMM1_EMS);
            if (!IsBlank(pmm.PMM1_OTHER)) location.Other1 = T(pmm.PMM1_OTHER);
            if (!IsBlank(pmm.PMM1_TOW_ZONE)) location.Tow = T(pmm.PMM1_TOW_ZONE);
            if (!IsBlank(pmm.PMM1_LOC_JUR)) location.Jurisdiction = T(pmm.PMM1_LOC_JUR);

            var xy = _store.PremiseCoordinates(fop);
            if (xy != null && xy.Value.Lat != 0)
            {
                location.X = xy.Value.Lat;
                location.Y = xy.Value.Long;
            }

            location.Formatted = LocationFormatter.Format(pmm.PMM1_LOC_ADDR, T(pmm.PMM1_LOC_STR), location.Sub, location.Unit, T(pmm.PMM1_LOC_X_ST));
            location.PremiseName = _store.PremiseName(fop);
            location.PremiseHazard = _store.HazardCount(fop) > 0;
            return location;
        }

        // ================================================================ helpers

        private static LocationCheck Pick(LocationQuery q, string title, string[] columns, List<LocationCandidate> rows) =>
            new LocationCheck
            {
                Status = LocationStatus.Choose,
                Pick = new LocationPickList { Kind = q.Kind, Title = title, Columns = columns, Rows = rows, Query = q },
                Message = rows.Count == 0 ? "No matching location was found." : string.Empty
            };

        private static bool IsEven(decimal value) => ((int)value / 2) * 2 == value;

        private static bool Between(decimal value, decimal low, decimal high) => value >= low && value <= high;

        private static bool JurOk(string rowJur, string jur) =>
            string.IsNullOrWhiteSpace(jur) || string.Equals(T(rowJur), jur.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool IsBlank(string value) => string.IsNullOrWhiteSpace(value);

        private static string T(string value) => (value ?? string.Empty).Trim();

        private static string Whole(decimal value) =>
            value == decimal.Truncate(value) ? ((long)value).ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
    }
}
