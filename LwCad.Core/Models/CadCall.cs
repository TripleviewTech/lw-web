using System;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One CAD event. Field names follow the legacy DSE1 / STK1 record so the
    /// demo service can be swapped for the production data layer without the
    /// dashboard changing.
    /// </summary>
    public class CadCall : ObservableObject
    {
        private CallQueue _queue = CallQueue.Stacked;
        private string _caseNo = string.Empty;
        private string _info = string.Empty;
        private string _notes = string.Empty;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _hasAlert;
        private bool _hasCrew;
        private Agency _primaryAgency = Agency.Police;

        public CadCall()
        {
            TrackChanges();

            AssignedUnits.CollectionChanged += (s, e) => Raise(nameof(HasAssignedUnits));
        }


        // Cells are built by a MultiBinding over (column, row), which listens to the
        // row object rather than to any one property. Bumping this on every change
        // gives those bindings something to watch, so a ticking Duration or a
        // changed Info still repaints.
        private int _rowVersion;
        public int RowVersion => _rowVersion;

        private void TrackChanges()
        {
            PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(RowVersion)) return;
                _rowVersion++;
                Raise(nameof(RowVersion));
            };
        }

        /// <summary>Stable identity of the event - the legacy SKey / EventNo pair.</summary>
        public long EventKey { get; set; }

        /// <summary>Formatted event number, e.g. 3222-4424242.</summary>
        public string EventNo { get; set; } = string.Empty;

        public string CallType { get; set; } = string.Empty;
        public string CallCode { get; set; } = string.Empty;

        /// <summary>
        /// What the overview puts under the call code - the code's description,
        /// which is what the legacy working-call area shows there. Falls back to
        /// the code itself when the description cannot be resolved.
        /// </summary>
        private string _callCodeText = string.Empty;
        public string CallCodeText
        {
            get => string.IsNullOrWhiteSpace(_callCodeText) ? CallCode : _callCodeText;
            set => Set(ref _callCodeText, value);
        }
        public string Jurisdiction { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Premise { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        /// <summary>Operational priority band chosen on the Take Call screen.</summary>
        public CallPriority Priority { get; set; } = CallPriority.Medium;

        /// <summary>Dispatch priority digit 1-9 shown in the "P" column (legacy DSE1 priority).</summary>
        public int PriorityNumber { get; set; } = 5;
        /// <summary>
        /// DSE1_EVENT_TYPE - P, F, E or O. Which agency owns the event, and what
        /// the Agency column on the call monitors shows.
        /// </summary>
        public Agency PrimaryAgency
        {
            get => _primaryAgency;
            set
            {
                if (!Set(ref _primaryAgency, value)) return;

                Raise(nameof(AgencyCode));
                Raise(nameof(CrewText));
            }
        }

        /// <summary>
        /// PD / FD / EMS / OTH - the coloured badge in the Agency column, resolved
        /// from DSE1_EVENT_TYPE exactly as the Unit Monitor resolves RST1_AGC_TYPE.
        /// </summary>
        public string AgencyCode
        {
            get
            {
                switch (PrimaryAgency)
                {
                    case Agency.Fire: return "FD";
                    case Agency.Ems: return "EMS";
                    case Agency.Other: return "OTH";
                    default: return "PD";
                }
            }
        }

        /// <summary>
        /// Per-agency dispatch priorities, CCC1_PRTY_P / _F / _E / _O. 0 means the
        /// call code does not task that agency. The assign panel shows all four.
        /// </summary>
        public int PolicePriority { get; set; }
        public int FirePriority { get; set; }
        public int EmsPriority { get; set; }
        public int OtherPriority { get; set; }

        /// <summary>
        /// What the assign screens key their recommendation off.
        /// <c>PoliceArea</c> is DSE1_POLICE_1, the police report area, which the
        /// deployment plan is looked up by; <c>ResponseArea</c> is DSE1_RESP_AREA,
        /// the run card number fire and EMS read their card by; and
        /// <c>LocationJurisdiction</c> is DSE1_LOC_JUR, which is not always the
        /// STK1_JUR the monitors show.
        /// </summary>
        public string PoliceArea { get; set; } = string.Empty;
        public string ResponseArea { get; set; } = string.Empty;
        public string LocationJurisdiction { get; set; } = string.Empty;

        /// <summary>
        /// DSE1_FIRE_1 and DSE1_EMS_1. Lowell keys its run cards on these rather than
        /// on DSE1_RESP_AREA: LwCad132 reads RUN1 by the fire box, LwCad133 reads RUN5
        /// by the EMS area.
        /// </summary>
        public string FireArea { get; set; } = string.Empty;
        public string EmsArea { get; set; } = string.Empty;

        /// <summary>DSE1_PMM_NO - the premise, whose PMM1_FIRE / PMM1_EMS override the areas.</summary>
        public int PremiseNo { get; set; }

        /// <summary>DSE1 key, for writes that address the event rather than its stack row.</summary>
        public char EventType { get; set; } = 'P';
        public short EventNo1 { get; set; }
        public int EventNo2 { get; set; }

        /// <summary>DSE1_CALL_CODE as a number, which is how RUN1 and RUN5 are keyed.</summary>
        public int CallCodeNumber { get; set; }

        /// <summary>
        /// CCC1_RESTACK. When the last unit is released off this call the event is
        /// significant enough to go back on the stack rather than close. Releasing a
        /// unit while others are still assigned never touches the call either way.
        /// </summary>
        public bool RestackOnRelease { get; set; }

        /// <summary>Reporting party, carried over from the Take Call screen.</summary>
        public string ReportedBy { get; set; } = string.Empty;
        public string ReportedPhone { get; set; } = string.Empty;

        /// <summary>When the call was received - drives the Time and Duration columns.</summary>
        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        public ObservableCollection<AssignedUnit> AssignedUnits { get; } = new ObservableCollection<AssignedUnit>();

        public string CaseNo
        {
            get => _caseNo;
            set => Set(ref _caseNo, value);
        }

        /// <summary>The "Add. Info" column.</summary>
        public string Info
        {
            get => _info;
            set => Set(ref _info, value);
        }

        public string Notes
        {
            get => _notes;
            set => Set(ref _notes, value);
        }

        public CallQueue Queue
        {
            get => _queue;
            set => Set(ref _queue, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => Set(ref _isExpanded, value);
        }

        /// <summary>
        /// Selection lives on the call, not on a grid, so the highlight follows
        /// the event when it moves between the Stacked / Active / Delayed monitors.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <summary>Timeout / attention flag shown against the row.</summary>
        public bool HasAlert
        {
            get => _hasAlert;
            set => Set(ref _hasAlert, value);
        }

        public string Time => ReceivedAt.ToString("HH:mm");

        public string Duration
        {
            get
            {
                var elapsed = DateTime.Now - ReceivedAt;
                if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                // Hours and minutes only: the board refreshes the column once a minute
                // (ShellViewModel.OnClockTick), so seconds would only ever show a stale value.
                return string.Format("{0:00}:{1:00}", (int)elapsed.TotalHours, elapsed.Minutes);
            }
        }

        public string PriorityText => PriorityNumber.ToString();

        /// <summary>
        /// EMS crew assigned. The legacy Active list shows this only for EMS events,
        /// from a DST1 transaction of type 16, and leaves it blank otherwise.
        /// </summary>
        public bool HasCrew
        {
            get => _hasCrew;
            set
            {
                if (Set(ref _hasCrew, value)) Raise(nameof(CrewText));
            }
        }

        public string CrewText => PrimaryAgency == Agency.Ems ? (HasCrew ? "Y" : "N") : string.Empty;

        public bool HasAssignedUnits => AssignedUnits.Count > 0;

        /// <summary>Saved with a disposition - completed on entry, never on the stack.</summary>
        public bool IsCompleted { get; set; }


        /// <summary>
        /// Names the row for screen readers and the automation tree; the default
        /// would announce the type name.
        /// </summary>
        public override string ToString()
        {
            var parts = new[] { CallType, Jurisdiction, Location, Time }
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join("  ", parts);
        }

        internal void TickTimers()
        {
            Raise(nameof(Duration));
        }
    }
}
