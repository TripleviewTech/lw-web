using System;
using System.Collections.Generic;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Location;

namespace LwCadSystemUX.Services.TakeCall
{
    /// <summary>
    /// Everything the legacy Take Call screen looks up on its own besides the call
    /// itself (TakeCall.cs / TakeCall.xaml.cs): the CAD7 switches, the location
    /// engine, the libraries its fields are checked against, and the indicator
    /// checks - duplicate, history, patrol and SOP - plus E911 and the map record.
    /// </summary>
    public interface ITakeCallSupport
    {
        TakeCallSettings Settings { get; }

        LocationEngine Locations { get; }

        /// <summary>MLB1 05/01 - dispositions. A disposition completes the call on entry.</summary>
        IReadOnlyList<LookupItem> Dispositions { get; }

        /// <summary>MLB2 50/01 - states, for the reporting person and the plate.</summary>
        IReadOnlyList<LookupItem> States { get; }

        /// <summary>MLB2 50/22 - plate types.</summary>
        IReadOnlyList<LookupItem> PlateTypes { get; }

        /// <summary>JUR1_P_NAME - the city "SAME" fills in (TakeCall_GetRpCity).</summary>
        string CityFor(string jurisdiction);

        /// <summary>
        /// The MMC1 checks (LaValidations Val_MMC1 through dbo.la_fn_GetMMC1Desc):
        /// the first field that is filled in but unknown, with the legacy message,
        /// or empty when all are fine.
        /// </summary>
        string VehicleError(string make, string model, string style, string color);

        /// <summary>CheckForDuplicate (TC:2465-2547) and the LwCad190 list behind the Duplicate button.</summary>
        IReadOnlyList<CallSummary> Duplicates(LocationResult place);

        /// <summary>la_fn_DSE1HistoryExist and the LwCad030 list behind the History button.</summary>
        IReadOnlyList<CallSummary> History(LocationResult place);

        /// <summary>la_fn_ValidatePTL1HitLevel - patrol file hits on the location, the person or the vehicle.</summary>
        PatrolHit Patrol(LocationResult place, string lastName, string firstName, string middleName,
                         string plate, string plateState);

        /// <summary>CCC2 - the call code's SOPs for the agencies with a priority.</summary>
        IReadOnlyList<SopEntry> Sops(int callCode, bool police, bool fire, bool ems, bool other);

        /// <summary>EMG2 - an E911 call waiting at the 911 position, or null.</summary>
        E911Call PendingE911(short position);

        /// <summary>CloseCleanUp: every EMG2 row for the position and caller's phone is removed.</summary>
        void ClearE911(E911Call call);

        /// <summary>TakeCall_CreateSTK2: the in-flight map record, written again as the call changes.</summary>
        long SaveMapRecord(MapRecord record, long existingKey);

        void DeleteMapRecord(long key);
    }

    /// <summary>The CAD7 switches Take Call reads at start-up (TakeCall.xaml.cs:330-356).</summary>
    public sealed class TakeCallSettings
    {
        /// <summary>CAD7_CAD_P / F / E / O - which priority boxes the operator may change.</summary>
        public bool AllowPolice { get; init; } = true;
        public bool AllowFire { get; init; } = true;
        public bool AllowEms { get; init; } = true;
        public bool AllowOther { get; init; } = true;

        /// <summary>CAD7_DUP_SW / CAD7_DUP_RANGE.</summary>
        public bool DuplicateCheck { get; init; }
        public short DuplicateRange { get; init; }

        /// <summary>CAD7_E911_XFER: 'A' load automatically, 'P' on request, 'N' never.</summary>
        public char E911Transfer { get; init; } = 'N';

        public bool Visual911 { get; init; }
        public bool VisualHistory { get; init; }
        public bool VisualPatrol { get; init; }
        public bool Play911 { get; init; }
        public bool PlayHistory { get; init; }
        public bool PlayPatrol { get; init; }

        /// <summary>CAD7_PREM_SW - a single premise at an address is attached automatically.</summary>
        public bool AttachSinglePremise { get; init; }

        public string DefaultJurisdiction { get; init; } = string.Empty;
        public string DefaultCity { get; init; } = string.Empty;

        /// <summary>TakeCall.cs:38-39 - hard-coded in Lowell's copy.</summary>
        public string DefaultState { get; init; } = "MA";
        public string DefaultAreaCode { get; init; } = "978";
    }

    /// <summary>A call shown in the Duplicate or History list.</summary>
    public sealed class CallSummary
    {
        public string EventNo { get; init; } = string.Empty;
        public DateTime? Received { get; init; }
        public string CallType { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        public string ReceivedText => Received?.ToString("MM/dd/yyyy HH:mm") ?? string.Empty;
    }

    /// <summary>la_fn_ValidatePTL1HitLevel's three digits: 1 exact, 2 close, 0 none - location, name, vehicle.</summary>
    public sealed class PatrolHit
    {
        public int Location { get; init; }
        public int Name { get; init; }
        public int Vehicle { get; init; }

        public bool Any => Location > 0 || Name > 0 || Vehicle > 0;
        public bool Exact => Location == 1 || Name == 1 || Vehicle == 1;

        public static PatrolHit FromLevel(int level)
        {
            var digits = Math.Max(0, level).ToString().PadLeft(3, '0');
            return new PatrolHit
            {
                Location = digits[0] - '0',
                Name = digits[1] - '0',
                Vehicle = digits[2] - '0'
            };
        }
    }

    public sealed class SopEntry
    {
        public char Type { get; init; }
        public string Text { get; init; } = string.Empty;

        public string Agency => Type switch { 'P' => "Police", 'F' => "Fire", 'E' => "EMS", _ => "Other" };
    }

    /// <summary>An EMG2 row - TakeCall_LoadE911Call copies it onto the form.</summary>
    public sealed class E911Call
    {
        public short CadPosition { get; init; }
        public short Position { get; init; }
        public short Phone1 { get; init; }
        public short Phone2 { get; init; }
        public short Phone3 { get; init; }
        public string LastName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string MiddleName { get; init; } = string.Empty;
        public int StreetNo { get; init; }
        public string Street { get; init; } = string.Empty;
        public string Unit { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Jurisdiction { get; init; } = string.Empty;
        public DateTime? Received { get; init; }
        public decimal XLow { get; init; }
        public decimal YLow { get; init; }
        public decimal XHigh { get; init; }
        public decimal YHigh { get; init; }

        public string Phone => Phone1 > 0 || Phone2 > 0 || Phone3 > 0
            ? Phone1.ToString("000") + "-" + Phone2.ToString("000") + "-" + Phone3.ToString("0000")
            : string.Empty;

        public string LocationText => LocationFormatter.Format(StreetNo, Street, string.Empty, Unit, string.Empty);
    }

    /// <summary>The STK2 in-flight map record (TakeCall_CreateSTK2).</summary>
    public sealed class MapRecord
    {
        public short Position { get; init; }
        public DateTime Received { get; init; }
        public int ReceivedBy { get; init; }
        public short Priority { get; init; }
        public int CallCode { get; init; }
        public string Area { get; init; } = string.Empty;
        public LocationResult Place { get; init; }
        public string LastName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string MiddleName { get; init; } = string.Empty;
        public short PhoneArea { get; init; }
        public short Phone1 { get; init; }
        public int Phone2 { get; init; }
        public string PcName { get; init; } = string.Empty;
    }
}
