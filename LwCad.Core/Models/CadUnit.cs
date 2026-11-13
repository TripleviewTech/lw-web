using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One row of the Unit Monitor. Field names follow the legacy
    /// UnitStatusMonitor columns (Unit / Station / UType / TType / Area / Location / Time).
    /// </summary>
    public class CadUnit : ObservableObject
    {
        private string _status = "AVAIL";
        private string _callType = string.Empty;
        private string _location = string.Empty;
        private string _area = string.Empty;
        private UnitAvailability _availability = UnitAvailability.Available;
        private DateTime _statusSince = DateTime.Now;
        private int _timeoutMinutes = 30;
        private bool _isSelected;
        private long _assignedCallKey;
        private bool? _isOverdue;
        private bool _hasStatusTime = true;
        private bool _hasPendingChange;

        public CadUnit()
        {
            TrackChanges();
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

        public string UnitNo { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public string UnitType { get; set; } = string.Empty;

        /// <summary>RST1_JUR - the jurisdiction this unit answers in.</summary>
        public string Jurisdiction { get; set; } = string.Empty;

        /// <summary>RST1_VEH_ID - a column the original carried but kept hidden.</summary>
        public string VehicleId { get; set; } = string.Empty;

        /// <summary>
        /// RST1_RECOMMEND. The roster marks this unit as a recommended response,
        /// which is what puts it at the head of the assign screens' slot list.
        /// </summary>
        public bool IsRecommended { get; set; }
        public Agency Agency { get; set; } = Agency.Police;

        /// <summary>PD / FD / EMS / OTH - the coloured badge in front of the unit number.</summary>
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

        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public string CallType
        {
            get => _callType;
            set => Set(ref _callType, value);
        }

        /// <summary>
        /// RST1_R_AREA_1..5 joined, as the monitor's Area column shows them - "4/5/9".
        /// </summary>
        public string Area
        {
            get => _area;
            set => Set(ref _area, value);
        }

        /// <summary>
        /// The same thing split back out, which is what the assign screens match a
        /// call's area against. Derived rather than stored so the two cannot drift.
        /// </summary>
        public IEnumerable<string> ResponseAreas =>
            (Area ?? string.Empty).Split('/')
                .Select(a => a.Trim())
                .Where(a => a.Length > 0);

        /// <summary>True when this unit's response areas cover the call's area.</summary>
        public bool CoversArea(string area)
        {
            if (string.IsNullOrWhiteSpace(area)) return false;

            var wanted = area.Trim();
            return ResponseAreas.Any(a => string.Equals(a, wanted, StringComparison.OrdinalIgnoreCase));
        }

        public string Location
        {
            get => _location;
            set => Set(ref _location, value);
        }

        public UnitAvailability Availability
        {
            get => _availability;
            set
            {
                if (Set(ref _availability, value))
                {
                    Raise(nameof(IsAvailable));
                }
            }
        }

        public bool IsAvailable => Availability == UnitAvailability.Available;

        /// <summary>When the current status was set - drives the Time column and the overdue timer.</summary>
        public DateTime StatusSince
        {
            get => _statusSince;
            set
            {
                if (Set(ref _statusSince, value))
                {
                    Raise(nameof(StatusTime));
                    Raise(nameof(IsOverdue));
                }
            }
        }

        /// <summary>
        /// Minutes a unit may hold this status before the monitor flags it.
        /// A non-call location (meal break, motor pool) uses 0 = never times out,
        /// which is the behaviour Lowell expects from Unit Status entries.
        /// </summary>
        public int TimeoutMinutes
        {
            get => _timeoutMinutes;
            set
            {
                if (Set(ref _timeoutMinutes, value))
                {
                    Raise(nameof(IsOverdue));
                }
            }
        }

        /// <summary>Event this unit is assigned to (0 = none).</summary>
        /// <summary>RST1_DISP_NO_1 / _2 - the unit's DSU1 assignment, which unit programs are handed.</summary>
        public short DispNo1 { get; set; }
        public int DispNo2 { get; set; }

        public long AssignedCallKey
        {
            get => _assignedCallKey;
            set => Set(ref _assignedCallKey, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        /// <summary>False when RST1 carries no status hour/minute, which the monitor shows blank.</summary>
        public bool HasStatusTime
        {
            get => _hasStatusTime;
            set
            {
                if (Set(ref _hasStatusTime, value)) Raise(nameof(StatusTime));
            }
        }

        /// <summary>
        /// The Unit Monitor's Time column. Lowell prints it HHMM with no colon
        /// (WinA.cs RST1_LAST_HR / _MN padded to two digits each); the call
        /// monitors keep HH:mm, as they do on the LW console.
        /// </summary>
        public string StatusTime => HasStatusTime ? StatusSince.ToString("HHmm") : string.Empty;

        /// <summary>
        /// Overdue on the status timer. The SQL service sets this from RST1_T_O_FLAG,
        /// which is the flag the legacy monitor counts; with nothing set it falls
        /// back to measuring the status timer here.
        /// </summary>
        public bool IsOverdue
        {
            get => _isOverdue ?? (TimeoutMinutes > 0 &&
                                  (DateTime.Now - StatusSince).TotalMinutes >= TimeoutMinutes);
            set => Set(ref _isOverdue, value, nameof(IsOverdue));
        }

        /// <summary>
        /// True when a unit function changed this row on this console only. Nothing
        /// is written back to RST1 yet, so the row is marked rather than passed off
        /// as saved state.
        /// </summary>
        public bool HasPendingChange
        {
            get => _hasPendingChange;
            set => Set(ref _hasPendingChange, value);
        }

        /// <summary>Names the row for screen readers and the automation tree.</summary>
        public override string ToString()
        {
            var parts = new[] { AgencyCode, UnitNo, UnitType, Status, CallType, Location }
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join("  ", parts);
        }

        internal void TickTimers()
        {
            Raise(nameof(IsOverdue));
        }
    }
}
