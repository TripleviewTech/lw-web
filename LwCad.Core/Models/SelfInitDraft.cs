using System;
using LwCadSystemUX.Services.Location;

namespace LwCadSystemUX.Models
{
    /// <summary>Which of the two officer-initiated forms is open - they share one panel.</summary>
    public enum SelfInitMode
    {
        /// <summary>LwCadSystem SelfInit.cs (LwCad107).</summary>
        SelfInitiate,

        /// <summary>LwCadSystem TraffStop.cs (LwCad112): police only, plate first, racial profile.</summary>
        TrafficStop
    }

    /// <summary>
    /// The Traffic Stop racial profile block - MLB2 system 5, libraries 60-66, and
    /// the profiling officer (PER1). The legacy stores the library *description*
    /// in DSU1_R_PROF_*, not the code, and so does this: reports written against
    /// the legacy rows read the text.
    /// </summary>
    public sealed class RacialProfile
    {
        public string Race { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Discretion { get; set; } = string.Empty;
        public string Resident { get; set; } = string.Empty;
        public string CitationWarning { get; set; } = string.Empty;
        public string Search { get; set; } = string.Empty;
        public int Officer { get; set; }
    }

    /// <summary>
    /// A completed Self Initiate / Traffic Stop form: one event of the unit's own
    /// agency and the unit already on it (SelfInit_Save).
    /// </summary>
    public sealed class SelfInitDraft
    {
        public SelfInitMode Mode { get; set; }

        public string UnitNo { get; set; } = string.Empty;
        public CallCodeItem CallCode { get; set; }

        /// <summary>tbxNV_Priority - one priority, for the unit's agency.</summary>
        public short Priority { get; set; }

        public LocationResult Place { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Jurisdiction { get; set; } = string.Empty;
        public string PoliceCode { get; set; } = string.Empty;
        public string FireCode { get; set; } = string.Empty;
        public string EmsCode { get; set; } = string.Empty;
        public string SupportCode { get; set; } = string.Empty;
        public string TowCode { get; set; } = string.Empty;
        public string Force { get; set; } = "N";

        public LookupItem Origin { get; set; }

        /// <summary>MLB1 05/01; above 0 closes the call as it is entered.</summary>
        public short Disposition { get; set; }

        public string Narrative { get; set; } = string.Empty;
        public VehicleInfo Vehicle { get; set; }

        /// <summary>CAD7_SELF_E / CAD7_TRAF_E resolved - 'A', or 'P' answered Yes.</summary>
        public bool Arrived { get; set; }

        /// <summary>
        /// The disposition the unit's current call is closed with when it has to be
        /// released first (LwCad123, default 999). 0 leaves the dispatch record's
        /// disposition as it is.
        /// </summary>
        public short ReleaseDisposition { get; set; }

        /// <summary>Traffic Stop only.</summary>
        public RacialProfile Profile { get; set; }

        /// <summary>When the form was opened - the legacy's RCV / DSP / ENR time.</summary>
        public DateTime ReceivedAt { get; set; }
    }

    /// <summary>What LwCad129 puts on the roster: one RST1 row and an RST2 row per report area.</summary>
    public sealed class UnitActivation
    {
        public string UnitNo { get; set; } = string.Empty;
        public string Agency { get; set; } = string.Empty;
        public char AgencyType { get; set; } = 'P';
        public string Jurisdiction { get; set; } = string.Empty;
        public int Employee1 { get; set; }
        public int Employee2 { get; set; }

        /// <summary>MLB1 05/04 code; 0 is N/A.</summary>
        public short UnitType { get; set; }

        /// <summary>Fire / EMS only - the MLB1 05/10 description, as LwCad129 stores it.</summary>
        public string Station { get; set; } = string.Empty;

        /// <summary>Police report areas, up to five.</summary>
        public string[] Areas { get; set; } = Array.Empty<string>();
    }
}
