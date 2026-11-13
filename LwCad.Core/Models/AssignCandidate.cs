using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One unit offered by an assign screen. The legacy screens fill these from the
    /// roster rows flagged RST1_RECOMMEND - eight numbered slots on LwCad131 and
    /// LwCad134, a row per apparatus type on LwCad132 and LwCad133 - and show the
    /// unit's current state beside the number so the dispatcher can see at a glance
    /// which of the recommendations are already on the call.
    /// </summary>
    public class AssignCandidate : ObservableObject
    {
        private bool _isQueued;

        public AssignCandidate(CadUnit unit, string groupName, bool isAlreadyAssigned)
        {
            Unit = unit;
            GroupName = groupName ?? string.Empty;
            IsAlreadyAssigned = isAlreadyAssigned;
        }

        public CadUnit Unit { get; }

        public string UnitNo => Unit.UnitNo;

        public Agency Agency => Unit.Agency;

        /// <summary>Apparatus type this candidate was filed under - Pumper, Ambulance, ...</summary>
        public string GroupName { get; }

        /// <summary>RST1_RECOMMEND - the roster marks this unit as a recommended response.</summary>
        public bool IsRecommended { get; set; }

        /// <summary>Already on this call, so the screen offers it as done rather than as a button.</summary>
        public bool IsAlreadyAssigned { get; }

        /// <summary>Picked on this screen and waiting for Assign.</summary>
        public bool IsQueued
        {
            get => _isQueued;
            set
            {
                if (!Set(ref _isQueued, value)) return;

                Raise(nameof(StateText));
                Raise(nameof(CanAssign));
            }
        }

        /// <summary>False once the unit is on the call, or queued to go on it.</summary>
        public bool CanAssign => !IsAlreadyAssigned && !IsQueued;

        public string StateText
        {
            get
            {
                if (IsAlreadyAssigned) return "Assigned";
                return IsQueued ? "Queued" : "Assign";
            }
        }

        /// <summary>The unit's response areas, shown beside it so the recommendation is checkable.</summary>
        public string AreaText => (Unit.Area ?? string.Empty).Trim();

        /// <summary>What the unit is doing now, shown as the candidate's tooltip.</summary>
        public string Summary
        {
            get
            {
                var what = string.IsNullOrWhiteSpace(Unit.CallType) ? "clear" : Unit.CallType;
                var parts = new System.Collections.Generic.List<string> { Unit.Status, what };

                if (AreaText.Length > 0) parts.Add("area " + AreaText);
                if (!string.IsNullOrWhiteSpace(Unit.Jurisdiction)) parts.Add(Unit.Jurisdiction);
                if (!string.IsNullOrWhiteSpace(Unit.Station)) parts.Add(Unit.Station);

                return UnitNo + "  -  " + string.Join(", ", parts);
            }
        }

        public override string ToString() => UnitNo + "  " + StateText;
    }
}
