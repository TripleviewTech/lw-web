namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One selectable unit status, from UST1. The abbreviation is what the monitor
    /// shows in the Status column; the availability switch is what decides whether
    /// the unit counts as available.
    /// </summary>
    public class UnitStatusOption
    {
        public UnitStatusOption(short code, string abbreviation, string description, bool isAvailable,
                                short nextStatusCode = 0)
        {
            Code = code;
            Abbreviation = (abbreviation ?? string.Empty).Trim();
            Description = (description ?? string.Empty).Trim();
            IsAvailable = isAvailable;
            NextStatusCode = nextStatusCode;
        }

        public short Code { get; }

        /// <summary>
        /// UST1_N_STATUS - the status that normally follows this one. It is what the
        /// Unit Status screen opens on, so the operator confirms the step the unit is
        /// actually making instead of picking it out of the list every time.
        /// 0 means nothing follows, which is how an in-service unit is set up: being
        /// available is not a step on the way to anything.
        /// </summary>
        public short NextStatusCode { get; }

        public bool HasNextStatus => NextStatusCode != 0;

        public string Abbreviation { get; }

        public string Description { get; }

        /// <summary>UST1_AVL_SW is 'A' for available, 'C' for committed to a call.</summary>
        public bool IsAvailable { get; }

        public string Display =>
            Description.Length > 0 ? Abbreviation + " - " + Description : Abbreviation;

        public override string ToString() => Display;
    }

    /// <summary>Which of the three unit functions a panel is running.</summary>
    public enum UnitFunction
    {
        Status,
        Location,
        Release
    }
}
