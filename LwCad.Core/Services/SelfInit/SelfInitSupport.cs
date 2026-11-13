using System;
using System.Collections.Generic;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.Services.SelfInit
{
    /// <summary>
    /// The lookups Self Initiate, Traffic Stop and Activate Unit read - the roster
    /// row, UNT1 defaults, the AGC1 agencies, the MLB1 / MLB2 libraries and PER1
    /// names. Location, call codes, dispositions and the vehicle checks are Take
    /// Call's (ITakeCallSupport); the writes are ICadDataService.SelfInitiate and
    /// ActivateUnit.
    /// </summary>
    public interface ISelfInitSupport
    {
        SelfInitSettings Settings { get; }

        /// <summary>The unit's RST1 row, or null when it is not on the roster (not logged on).</summary>
        RosterUnit FindUnit(string unitNo);

        /// <summary>UNT1 for the unit, with its AGC1 agency type - null when UNT1 has no row.</summary>
        UnitDefaults UnitDefaults(string unitNo);

        /// <summary>AGC1 licensed agencies (AGC1_LIC_AGC = 'Y').</summary>
        IReadOnlyList<AgencyItem> Agencies { get; }

        /// <summary>MLB1 05/04 - unit types.</summary>
        IReadOnlyList<LookupItem> UnitTypes { get; }

        /// <summary>MLB1 05/10 - fire stations.</summary>
        IReadOnlyList<LookupItem> Stations { get; }

        /// <summary>JUR1 jurisdictions a unit can be logged on to: GEO_JUR or E911_JUR, and UNITS.</summary>
        IReadOnlyList<LookupItem> UnitJurisdictions { get; }

        /// <summary>la_fn_GetPerName - "" when the employee is not on PER1.</summary>
        string PersonName(int employeeId);

        /// <summary>MLB2 05/60-66 descriptions, blank first - the racial profile lists.</summary>
        IReadOnlyList<string> ProfileList(int library);

        /// <summary>The CCC1_TS_CODE call code Traffic Stop starts on; "" when none is flagged.</summary>
        string TrafficStopCallCode();
    }

    public sealed class SelfInitSettings
    {
        /// <summary>CAD7_SELF_E: E en route, A arrived, P ask. Blank is E.</summary>
        public char SelfArrive { get; set; } = 'E';

        /// <summary>CAD7_TRAF_E, the same switch for Traffic Stop.</summary>
        public char TrafficArrive { get; set; } = 'E';
    }

    /// <summary>What Val_tbxAV_UnitNo reads from RST1.</summary>
    public sealed class RosterUnit
    {
        public string UnitNo { get; set; } = string.Empty;
        public char AgencyType { get; set; } = ' ';
        public string Agency { get; set; } = string.Empty;
        public string Jurisdiction { get; set; } = string.Empty;
        public int Officer1 { get; set; }
        public string Officer1Name { get; set; } = string.Empty;
        public int Officer2 { get; set; }
        public string Officer2Name { get; set; } = string.Empty;
        public short Status { get; set; }
        public char EventType { get; set; } = ' ';
        public short EventNo1 { get; set; }
        public int EventNo2 { get; set; }
        public int DispNo2 { get; set; }
        public int LockId { get; set; }

        /// <summary>RST1_DISP_NO_2 &gt; 0 - SelfInit_PrepareRST1 releases it first (LwCad123).</summary>
        public bool IsOnCall => DispNo2 > 0;

        private string _eventText;

        /// <summary>"P 2026-0000012" - the call the unit is on.</summary>
        public string EventText
        {
            get => _eventText ?? EventType + " " + EventNo1.ToString("0000") + "-" + EventNo2.ToString("0000000");
            set => _eventText = value;
        }
    }

    /// <summary>UNT1 - the unit file LwCad129 pre-fills from.</summary>
    public sealed class UnitDefaults
    {
        public string UnitNo { get; set; } = string.Empty;
        public string Agency { get; set; } = string.Empty;

        /// <summary>AGC1_AGC_TYPE for UNT1_AGCY; ' ' when the agency is not in AGC1.</summary>
        public char AgencyType { get; set; } = ' ';

        public string Jurisdiction { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public short UnitType { get; set; }
        public int Employee1 { get; set; }
        public int Employee2 { get; set; }
        public string[] Areas { get; set; } = Array.Empty<string>();
    }

    public sealed class AgencyItem
    {
        public AgencyItem(string code, char type, bool isDefault)
        {
            Code = code ?? string.Empty;
            Type = type;
            IsDefault = isDefault;
        }

        public string Code { get; }

        /// <summary>AGC1_AGC_TYPE: P, F, E or O.</summary>
        public char Type { get; }

        /// <summary>AGC1_RST_DFLT - the one the activation screen starts on.</summary>
        public bool IsDefault { get; }

        public string TypeName => Type == 'F' ? "Fire" : Type == 'E' ? "EMS" : Type == 'O' ? "Other" : "Police";

        public string Display => Code + " - " + TypeName;

        public override string ToString() => Display;
    }
}
