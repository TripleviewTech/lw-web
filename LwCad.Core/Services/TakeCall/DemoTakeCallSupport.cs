using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Location;

namespace LwCadSystemUX.Services.TakeCall
{
    /// <summary>
    /// Take Call's lookups for the demo board: the demo geobase, a few libraries,
    /// and canned answers so each indicator can be seen - history at 302 CEDAR LN,
    /// a patrol hit at 40 BAKER ST, SOPs on the burglary and fire alarm codes, and
    /// one E911 call waiting at position 1 (F11 loads it).
    /// </summary>
    public sealed class DemoTakeCallSupport : ITakeCallSupport
    {
        private readonly Func<IEnumerable<CadCall>> _calls;
        private bool _e911Taken;
        private long _mapKeys = 1;

        public DemoTakeCallSupport(Func<IEnumerable<CadCall>> calls, string defaultCity, string defaultState)
        {
            _calls = calls;

            Settings = new TakeCallSettings
            {
                DuplicateCheck = true,
                DuplicateRange = 50,
                E911Transfer = 'P',
                Visual911 = true,
                VisualHistory = true,
                VisualPatrol = true,
                AttachSinglePremise = false,
                DefaultJurisdiction = DemoGeoStore.Jur,
                DefaultCity = defaultCity,
                DefaultState = defaultState
            };

            Locations = new LocationEngine(new DemoGeoStore(), new LocationSettings
            {
                AttachSinglePremise = Settings.AttachSinglePremise,
                DefaultJurisdiction = Settings.DefaultJurisdiction
            });
        }

        public TakeCallSettings Settings { get; }
        public LocationEngine Locations { get; }

        public IReadOnlyList<LookupItem> Dispositions { get; } = new List<LookupItem>
        {
            new LookupItem("1", "Report Taken"),
            new LookupItem("2", "Advised"),
            new LookupItem("3", "Gone On Arrival"),
            new LookupItem("4", "Unfounded"),
            new LookupItem("5", "Referred To Other Agency"),
            new LookupItem("999", "Completed")
        };

        public IReadOnlyList<LookupItem> States { get; } = new List<LookupItem>
        {
            new LookupItem("MA", "Massachusetts"), new LookupItem("NH", "New Hampshire"),
            new LookupItem("NY", "New York"), new LookupItem("CT", "Connecticut"),
            new LookupItem("RI", "Rhode Island"), new LookupItem("VT", "Vermont"),
            new LookupItem("ME", "Maine"), new LookupItem("TX", "Texas")
        };

        public IReadOnlyList<LookupItem> PlateTypes { get; } = new List<LookupItem>
        {
            new LookupItem("PC", "Passenger"), new LookupItem("CO", "Commercial"),
            new LookupItem("MC", "Motorcycle"), new LookupItem("TR", "Trailer"),
            new LookupItem("AP", "Apportioned")
        };

        public string CityFor(string jurisdiction) =>
            string.Equals((jurisdiction ?? string.Empty).Trim(), DemoGeoStore.Jur, StringComparison.OrdinalIgnoreCase)
                ? Settings.DefaultCity
                : string.Empty;

        private static readonly HashSet<string> Makes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "FORD", "TOYT", "HOND", "CHEV", "NISS", "DODG", "SUBA" };
        private static readonly HashSet<string> Colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "BLK", "WHI", "SIL", "GRY", "BLU", "RED", "GRN", "TAN" };
        private static readonly HashSet<string> Styles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "4D", "2D", "SUV", "PK", "VAN", "MC" };

        public string VehicleError(string make, string model, string style, string color)
        {
            if (!string.IsNullOrWhiteSpace(make) && !Makes.Contains(make.Trim())) return "Vehicle Make is invalid.";
            if (!string.IsNullOrWhiteSpace(style) && !Styles.Contains(style.Trim())) return "Vehicle Style is invalid.";
            if (!string.IsNullOrWhiteSpace(color) && !Colors.Contains(color.Trim())) return "Vehicle Color is invalid.";
            return string.Empty;
        }

        public IReadOnlyList<CallSummary> Duplicates(LocationResult place)
        {
            if (place == null || place.Street.Length == 0 || place.Forced == 'X') return Array.Empty<CallSummary>();

            var street = Words(place.Street);
            return _calls()
                .Where(c => c.Queue != CallQueue.Delayed && !string.IsNullOrWhiteSpace(c.Location))
                .Where(c => Words(c.Location).Contains(street))
                .Select(c => new CallSummary
                {
                    EventNo = c.EventNo,
                    Received = c.ReceivedAt,
                    CallType = c.CallType,
                    Location = c.Location,
                    Status = c.Queue.ToString()
                })
                .ToList();
        }

        public IReadOnlyList<CallSummary> History(LocationResult place)
        {
            if (place == null) return Array.Empty<CallSummary>();

            if (place.Street == "CEDAR LN" && place.StreetNo == 302)
            {
                return new[]
                {
                    new CallSummary { EventNo = "P 2026-00003811", Received = DateTime.Today.AddDays(-12).AddHours(22), CallType = "Disturbance", Location = "302 CEDAR LN", Status = "Closed" },
                    new CallSummary { EventNo = "P 2026-00001207", Received = DateTime.Today.AddDays(-65).AddHours(3), CallType = "Burglary - In Progress", Location = "302 CEDAR LN", Status = "Closed" },
                    new CallSummary { EventNo = "F 2025-00009120", Received = DateTime.Today.AddDays(-240).AddHours(14), CallType = "Fire Alarm - Commercial", Location = "302 CEDAR LN", Status = "Closed" }
                };
            }

            return Array.Empty<CallSummary>();
        }

        public PatrolHit Patrol(LocationResult place, string lastName, string firstName, string middleName,
                                string plate, string plateState)
        {
            var location = place != null && place.Street == "BAKER ST" && place.StreetNo == 40 ? 1 : 0;
            var name = string.Equals((lastName ?? string.Empty).Trim(), "SMITH", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            var vehicle = string.Equals((plate ?? string.Empty).Trim(), "8XY123", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return new PatrolHit { Location = location, Name = name, Vehicle = vehicle };
        }

        public IReadOnlyList<SopEntry> Sops(int callCode, bool police, bool fire, bool ems, bool other)
        {
            var list = new List<SopEntry>();
            if (callCode == 220100 && police)
            {
                list.Add(new SopEntry { Type = 'P', Text = "Send two units. Establish a perimeter before entry. Notify the shift supervisor. Hold the caller on the line." });
            }

            if (callCode == 410100 && fire)
            {
                list.Add(new SopEntry { Type = 'F', Text = "Engine and ladder. Check the alarm panel and the Knox box. Contact the alarm company for zone information." });
            }

            return list;
        }

        public E911Call PendingE911(short position)
        {
            if (_e911Taken || position != 1) return null;

            return new E911Call
            {
                CadPosition = 1, Position = 1, Phone1 = 978, Phone2 = 555, Phone3 = 142,
                LastName = "ADAMS", FirstName = "JOHN", MiddleName = "Q",
                StreetNo = 88, Street = "ELM ST", City = Settings.DefaultCity, State = Settings.DefaultState,
                Jurisdiction = DemoGeoStore.Jur, Received = DateTime.Now.AddSeconds(-20)
            };
        }

        public void ClearE911(E911Call call)
        {
            if (call != null) _e911Taken = true;
        }

        public long SaveMapRecord(MapRecord record, long existingKey) => existingKey > 0 ? existingKey : _mapKeys++;

        public void DeleteMapRecord(long key)
        {
        }

        private static string Words(string text) =>
            " " + string.Join(" ", (text ?? string.Empty).ToUpperInvariant()
                .Split(new[] { ' ', '.', ',', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)) + " ";
    }
}
