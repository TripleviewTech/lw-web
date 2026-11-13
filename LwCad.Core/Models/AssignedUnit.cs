namespace LwCadSystemUX.Models
{
    /// <summary>
    /// A unit on a call, as the overview's Units Assigned chips show it.
    /// Carries the agency from the roster rather than guessing it from the unit
    /// number, which does not work for the numeric unit numbers real rosters use.
    /// </summary>
    public class AssignedUnit
    {
        public AssignedUnit(string unitNo, Agency agency)
        {
            UnitNo = unitNo ?? string.Empty;
            Agency = agency;
        }

        public string UnitNo { get; }

        public Agency Agency { get; }

        /// <summary>PD / FD / EMS / OTH - drives the chip colour.</summary>
        public string AgencyCode
        {
            get
            {
                switch (Agency)
                {
                    case Agency.Fire: return "FD";
                    case Agency.Ems: return "EMS";
                    case Agency.Other: return "OTH";
                    default: return "PD";
                }
            }
        }
    }
}
