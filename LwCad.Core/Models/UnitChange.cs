namespace LwCadSystemUX.Models
{
    /// <summary>
    /// What one of the three unit functions is asking for. Mirrors the fields the
    /// legacy LwCad135 screen collects: the unit, the new status, a location, and
    /// the minutes it may hold that status before the monitor flags it.
    /// </summary>
    public class UnitChange
    {
        public UnitChange(UnitFunction function, CadUnit unit)
        {
            Function = function;
            Unit = unit;
        }

        public UnitFunction Function { get; }

        public CadUnit Unit { get; }

        /// <summary>The new status. Null for Location Change and Release.</summary>
        public UnitStatusOption Status { get; set; }

        public string Location { get; set; } = string.Empty;

        /// <summary>0 means the unit does not time out in this status.</summary>
        public int TimeoutMinutes { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }
}
