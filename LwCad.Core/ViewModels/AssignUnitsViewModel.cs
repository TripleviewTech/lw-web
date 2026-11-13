using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The Assign Units panel, built to <c>Docs/Figma/CAD_Assign*.png</c>.
    ///
    /// One panel runs all four of the legacy assign programs, the way the boards
    /// show them - LwCad131 Assign Police, LwCad132 Assign Fire, LwCad133 Assign
    /// EMS and LwCad134 Assign Other - switched by the PD / FD / EMS / SPT tabs,
    /// which is what LwCad131's own "Assign Fire Unit" / "Assign EMS Unit" /
    /// "Assign Support Unit" buttons did.
    ///
    /// It also runs both assignment modes. Taking units to a call off the Stacked
    /// monitor is an <b>Assign Call</b>; adding units to a call that is already
    /// running on the Active monitor is an <b>Assign / Assist</b> (legacy LwCad140).
    /// Same program, same fields - so the panel says which one it is in its title,
    /// its Assign button and the status line rather than leaving the dispatcher to
    /// infer it from which list they came from.
    /// </summary>
    public class AssignUnitsViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private bool _isOpen;
        private bool _isCallDetailExpanded;
        private bool _isProcessedExpanded = true;
        private CadCall _call;
        private AssignMode _mode = AssignMode.Call;
        private Agency _agency = Agency.Police;
        private DispatchAction _action = DispatchAction.Dispatch;
        private string _manualUnitNo = string.Empty;
        private string _timeout = string.Empty;
        private bool _xmitToMdc;
        private string _validationMessage = string.Empty;
        private string _openedAt = string.Empty;

        public AssignUnitsViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            AssignCommand = new RelayCommand(() => _ = AssignAsync());
            CancelCommand = new RelayCommand(Cancel);

            SetAgencyCommand = new RelayCommand(p =>
            {
                if (p is Agency typed) Agency = typed;
                else if (p is string s && Enum.TryParse(s, out Agency parsed)) Agency = parsed;
            });

            SetActionCommand = new RelayCommand(p =>
            {
                var wanted = Action;

                if (p is DispatchAction typed) wanted = typed;
                else if (p is string s && Enum.TryParse(s, out DispatchAction parsed)) wanted = parsed;

                // A protected choice cannot be taken, by click or by command.
                if (wanted == DispatchAction.Dispatch && !IsDispatchAllowed) return;

                Action = wanted;
            });

            QueueCommand = new RelayCommand(p => Queue(p as AssignCandidate));
            RemoveQueuedCommand = new RelayCommand(p => Unqueue(p as AssignCandidate));
            AddManualCommand = new RelayCommand(AddManual);

            ToggleCallDetailCommand = new RelayCommand(() => IsCallDetailExpanded = !IsCallDetailExpanded);
            ToggleProcessedCommand = new RelayCommand(() => IsProcessedExpanded = !IsProcessedExpanded);

            MoreCommand = new RelayCommand(() =>
                _shell.SetStatus(Title + " options menu is not enabled in this build."));
        }

        // -------------------------------------------------------------- state

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        /// <summary>The event being assigned - the row the panel was opened from.</summary>
        public CadCall Call
        {
            get => _call;
            private set
            {
                if (!Set(ref _call, value)) return;

                Raise(nameof(HasCall));
                Raise(nameof(CallCodeText));
            }
        }

        public bool HasCall => Call != null;

        /// <summary>The Figma board's call code line - "000322 - Burglary".</summary>
        public string CallCodeText
        {
            get
            {
                if (Call == null) return string.Empty;

                var code = (Call.CallCode ?? string.Empty).Trim();
                var text = (Call.CallType ?? string.Empty).Trim();

                if (code.Length == 0) return text;
                return text.Length == 0 ? code : code + "  -  " + text;
            }
        }

        public AssignMode Mode
        {
            get => _mode;
            private set
            {
                if (!Set(ref _mode, value)) return;

                Raise(nameof(IsAssist));
                Raise(nameof(Title));
                Raise(nameof(ModeText));
                Raise(nameof(AssignLabel));
                Raise(nameof(ModeNote));
            }
        }

        public bool IsAssist => Mode == AssignMode.Assist;

        /// <summary>
        /// Assign Call on an event with no units yet, Assign / Assist on one already
        /// running. The panel header, the Assign button and the status line all read
        /// from here, so which of the two is running is never in doubt.
        /// </summary>
        public string ModeText => IsAssist ? "Assign / Assist" : "Assign Call";

        public string Title => IsAssist ? "Assign / Assist Units" : "Assign Units";

        public string ModeNote => IsAssist
            ? "Assist units are added to an event that is already running. The call stays on the Active monitor."
            : "Assigning the first unit takes the event off the stack and onto the Active monitor.";

        /// <summary>Which of the four assign programs the current tab is running.</summary>
        public Agency Agency
        {
            get => _agency;
            set
            {
                if (!Set(ref _agency, value)) return;

                Raise(nameof(IsPolice));
                Raise(nameof(IsFire));
                Raise(nameof(IsEms));
                Raise(nameof(IsOther));
                Raise(nameof(ProgramText));
                Raise(nameof(UsesGroupedLayout));
                Raise(nameof(HasTimeout));
                Raise(nameof(IsDispatchAllowed));

                // LwCad131 and LwCad134 open on En Route - they cannot dispatch at all;
                // the fire and EMS boards open on Dispatch, the first state their
                // apparatus take.
                Action = IsDispatchAllowed ? DispatchAction.Dispatch : DispatchAction.EnRoute;

                ValidationMessage = string.Empty;
                LoadCandidates();
            }
        }

        public bool IsPolice => Agency == Agency.Police;
        public bool IsFire => Agency == Agency.Fire;
        public bool IsEms => Agency == Agency.Ems;
        public bool IsOther => Agency == Agency.Other;

        /// <summary>
        /// The legacy program behind the current tab, shown in the panel header so
        /// the operator can tie the screen back to the one they know.
        /// </summary>
        public string ProgramText
        {
            get
            {
                switch (Agency)
                {
                    case Agency.Fire: return "LwCad132 - Assign Fire";
                    case Agency.Ems: return "LwCad133 - Assign EMS";
                    case Agency.Other: return "LwCad134 - Assign Other";
                    default: return "LwCad131 - Assign Police";
                }
            }
        }

        /// <summary>
        /// Fire and EMS list their roster a row per apparatus type, the way LwCad132
        /// and LwCad133 do. Police and Other use the numbered slot grid of LwCad131
        /// and LwCad134.
        /// </summary>
        public bool UsesGroupedLayout => Agency == Agency.Fire || Agency == Agency.Ems;

        /// <summary>Only LwCad131 and LwCad134 carry the status timeout field.</summary>
        public bool HasTimeout => Agency == Agency.Police || Agency == Agency.Other;

        /// <summary>
        /// Whether this screen can send a unit straight to Dispatched.
        ///
        /// LwCad131 and LwCad134 offer only En Route and Arrive; LwCad132 and
        /// LwCad133 offer Dispatch as well. The choice is protected rather than
        /// removed on the boards that lack it, so the three states sit in the same
        /// place whichever tab is up and an operator is not hunting for a control
        /// that moved.
        /// </summary>
        public bool IsDispatchAllowed => Agency == Agency.Fire || Agency == Agency.Ems;

        /// <summary>When the panel was opened, as the board's header shows it.</summary>
        public string OpenedAt
        {
            get => _openedAt;
            private set => Set(ref _openedAt, value);
        }

        // ------------------------------------------------------- recommendations

        /// <summary>What this agency's roster offers, grouped as its screen groups it.</summary>
        public ObservableCollection<AssignGroup> Groups { get; } = new ObservableCollection<AssignGroup>();

        public bool HasGroups => Groups.Count > 0;

        private string _candidateBasis = string.Empty;

        /// <summary>
        /// One line under the heading saying how these units were chosen - the area
        /// and jurisdiction they cover, or that nothing covered the call and the
        /// search was widened.
        /// </summary>
        public string CandidateBasis
        {
            get => _candidateBasis;
            private set => Set(ref _candidateBasis, value);
        }

        private bool _isCandidateListWidened;

        /// <summary>
        /// True when nothing covered the call. The line is drawn as a warning then,
        /// because a widened list read as a recommendation is how the wrong unit gets
        /// sent.
        /// </summary>
        public bool IsCandidateListWidened
        {
            get => _isCandidateListWidened;
            private set => Set(ref _isCandidateListWidened, value);
        }

        /// <summary>Units picked on this screen and waiting for Assign.</summary>
        public ObservableCollection<AssignCandidate> Queued { get; } =
            new ObservableCollection<AssignCandidate>();

        public bool HasQueued => Queued.Count > 0;

        public int QueuedCount => Queued.Count;

        /// <summary>
        /// Units this panel has already put on the call, the board's Processed Units
        /// tray. It is the operator's record of what this sitting has done, so it
        /// survives switching agency tabs and is only cleared when the panel reopens.
        /// </summary>
        public ObservableCollection<AssignedUnit> Processed { get; } =
            new ObservableCollection<AssignedUnit>();

        public int ProcessedCount => Processed.Count;

        /// <summary>
        /// The footer's close button: Cancel until a unit has been assigned, then Done -
        /// the panel stays open after an assignment, and closing it no longer abandons anything.
        /// </summary>
        public string CloseLabel => ProcessedCount > 0 ? "Done" : "Cancel";

        public bool IsProcessedExpanded
        {
            get => _isProcessedExpanded;
            set => Set(ref _isProcessedExpanded, value);
        }

        public bool IsCallDetailExpanded
        {
            get => _isCallDetailExpanded;
            set => Set(ref _isCallDetailExpanded, value);
        }

        // ------------------------------------------------------------- inputs

        /// <summary>tbxAV_UnitID - a unit typed in rather than picked off the roster.</summary>
        public string ManualUnitNo
        {
            get => _manualUnitNo;
            set => Set(ref _manualUnitNo, value);
        }

        /// <summary>Every unit on the monitor, so the manual box can complete against it.</summary>
        public IEnumerable<string> AllUnitNumbers => _data.Units.Select(u => u.UnitNo);

        /// <summary>tbxNV_TimeOut - minutes, blank leaves the call code's own timeout.</summary>
        public string Timeout
        {
            get => _timeout;
            set => Set(ref _timeout, value);
        }

        public bool XmitToMdc
        {
            get => _xmitToMdc;
            set => Set(ref _xmitToMdc, value);
        }

        public DispatchAction Action
        {
            get => _action;
            set
            {
                if (!Set(ref _action, value)) return;

                Raise(nameof(IsDispatch));
                Raise(nameof(IsEnRoute));
                Raise(nameof(IsArrived));
            }
        }

        public bool IsDispatch => Action == DispatchAction.Dispatch;
        public bool IsEnRoute => Action == DispatchAction.EnRoute;
        public bool IsArrived => Action == DispatchAction.Arrived;

        /// <summary>
        /// The primary button. It counts what is waiting, the way the board's
        /// "Assign - 3" does, and names the mode so an assist is never mistaken for
        /// a first assignment.
        /// </summary>
        public string AssignLabel =>
            QueuedCount > 0 ? ModeText + "  -  " + QueuedCount : ModeText;

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (Set(ref _validationMessage, value)) Raise(nameof(HasValidationMessage));
            }
        }

        public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

        // ---------------------------------------------------------- commands

        public RelayCommand AssignCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand SetAgencyCommand { get; }
        public RelayCommand SetActionCommand { get; }
        public RelayCommand QueueCommand { get; }
        public RelayCommand RemoveQueuedCommand { get; }
        public RelayCommand AddManualCommand { get; }
        public RelayCommand ToggleCallDetailCommand { get; }
        public RelayCommand ToggleProcessedCommand { get; }
        public RelayCommand MoreCommand { get; }

        // ------------------------------------------------------------- flow

        /// <summary>
        /// Opens the panel on a call. The mode follows the monitor the call is on -
        /// an event already running on Active is an assist - and the agency tab opens
        /// on the agency that owns the event, which is the tab the dispatcher wants
        /// in nearly every case.
        /// </summary>
        public void Open(CadCall call, AssignMode mode)
        {
            if (call == null)
            {
                _shell.SetStatus("Assign: select a call first.");
                return;
            }

            Call = call;
            Mode = mode;
            OpenedAt = DateTime.Now.ToString("MM/dd/yyyy  -  HH:mm:ss");

            Queued.Clear();
            Processed.Clear();
            RaiseQueued();
            Raise(nameof(ProcessedCount));
            Raise(nameof(CloseLabel));

            ManualUnitNo = string.Empty;
            Timeout = string.Empty;
            XmitToMdc = false;
            IsCallDetailExpanded = false;
            IsProcessedExpanded = true;
            ValidationMessage = string.Empty;

            // Setting Agency reloads the candidates and the default action; both are
            // repeated here because reopening on a call of the same agency leaves the
            // property unchanged, so its setter would not run.
            Agency = call.PrimaryAgency;
            Action = IsDispatchAllowed ? DispatchAction.Dispatch : DispatchAction.EnRoute;
            LoadCandidates();
            Raise(nameof(AllUnitNumbers));

            IsOpen = true;
            _shell.SetStatus(ModeText + " - " + ProgramText + " for event " + call.EventNo + ".");
        }

        public void Cancel()
        {
            IsOpen = false;
            _shell.SetStatus(ProcessedCount == 0
                ? ModeText + " cancelled. No unit was assigned."
                : ModeText + " done. " + ProcessedCount + " unit(s) assigned.");
        }

        /// <summary>Re-reads what this agency offers for the call.</summary>
        private void LoadCandidates()
        {
            Groups.Clear();

            var set = Call == null
                ? AssignCandidateSet.Empty
                : _data.GetAssignCandidates(Call, Agency);

            foreach (var group in set.Groups) Groups.Add(group);

            CandidateBasis = set.Basis;
            IsCandidateListWidened = set.IsWidened;

            // A unit queued on this tab stays queued when the tab comes back.
            foreach (var candidate in Groups.SelectMany(g => g.Candidates))
            {
                candidate.IsQueued = Queued.Any(q => string.Equals(q.UnitNo, candidate.UnitNo,
                                                                   StringComparison.OrdinalIgnoreCase));
            }

            Raise(nameof(HasGroups));
        }

        private void Queue(AssignCandidate candidate)
        {
            if (candidate == null || candidate.IsAlreadyAssigned || candidate.IsQueued) return;

            candidate.IsQueued = true;
            Queued.Add(candidate);
            RaiseQueued();
            ValidationMessage = string.Empty;
        }

        private void Unqueue(AssignCandidate candidate)
        {
            if (candidate == null) return;

            candidate.IsQueued = false;
            Queued.Remove(candidate);

            // The same unit may be sitting in the roster list under its own object.
            foreach (var other in Groups.SelectMany(g => g.Candidates)
                                        .Where(c => string.Equals(c.UnitNo, candidate.UnitNo,
                                                                  StringComparison.OrdinalIgnoreCase)))
            {
                other.IsQueued = false;
            }

            RaiseQueued();
        }

        /// <summary>
        /// Takes the unit typed into the manual box, the way the legacy screens take
        /// tbxAV_UnitID: it has to be a unit the monitor is showing, and it cannot
        /// already be on this call.
        /// </summary>
        private void AddManual()
        {
            var entered = (ManualUnitNo ?? string.Empty).Trim();
            if (entered.Length == 0) return;

            var unit = _data.Units.FirstOrDefault(
                u => string.Equals(u.UnitNo, entered, StringComparison.OrdinalIgnoreCase));

            if (unit == null)
            {
                Fail("Unit " + entered + " is not on the monitor.");
                return;
            }

            if (Call != null && Call.AssignedUnits.Any(
                    a => string.Equals(a.UnitNo, unit.UnitNo, StringComparison.OrdinalIgnoreCase)))
            {
                Fail("Unit " + unit.UnitNo + " is already on this call.");
                return;
            }

            if (Queued.Any(q => string.Equals(q.UnitNo, unit.UnitNo, StringComparison.OrdinalIgnoreCase)))
            {
                Fail("Unit " + unit.UnitNo + " is already waiting to be assigned.");
                return;
            }

            // A manual unit may be off the current tab's roster entirely - assigning
            // a fire unit from the PD board is exactly what the legacy screen's
            // "Assign Fire Unit" button was for - so it is queued on its own.
            var onRoster = Groups.SelectMany(g => g.Candidates).FirstOrDefault(
                c => string.Equals(c.UnitNo, unit.UnitNo, StringComparison.OrdinalIgnoreCase));

            if (onRoster != null)
            {
                Queue(onRoster);
            }
            else
            {
                var candidate = new AssignCandidate(unit, string.Empty, false) { IsQueued = true };
                Queued.Add(candidate);
                RaiseQueued();
            }

            ManualUnitNo = string.Empty;
            ValidationMessage = string.Empty;
        }

        private bool _assigning;

        /// <summary>Web port: the cross-jurisdiction question is awaited, so one assign runs at a time.</summary>
        private async Task AssignAsync()
        {
            if (_assigning) return;
            _assigning = true;
            try
            {
                await AssignCore();
            }
            catch (Exception ex)
            {
                _shell.SetStatus(Title + " failed: " + ex.Message);
            }
            finally
            {
                _assigning = false;
            }
        }

        private async Task AssignCore()
        {
            if (!Validate()) return;

            var assignment = new UnitAssignment(Call, Agency, Mode)
            {
                Action = Action,
                TimeoutMinutes = HasTimeout ? ParsedTimeout() : 0,
                XmitToMdc = XmitToMdc
            };

            foreach (var candidate in Queued) assignment.Units.Add(candidate.Unit);

            // Lowell confirms a unit sent outside its own jurisdiction (LwCad131
            // TabA_AssignUnit: "X will be assigned to a call in JUR"); Easthampton
            // commented the prompt out.
            var callJur = (Call.LocationJurisdiction ?? string.Empty).Trim();
            if (callJur.Length == 0) callJur = (Call.Jurisdiction ?? string.Empty).Trim();

            foreach (var unit in assignment.Units.ToList())
            {
                var unitJur = (unit.Jurisdiction ?? string.Empty).Trim();
                if (unitJur.Length == 0 || callJur.Length == 0 ||
                    string.Equals(unitJur, callJur, StringComparison.OrdinalIgnoreCase)) continue;

                if (!await _shell.ConfirmAsync(unit.UnitNo + " will be assigned to a call in " + callJur + ". Continue?"))
                {
                    assignment.Units.Remove(unit);
                }
            }

            if (assignment.Units.Count == 0)
            {
                _shell.SetStatus(ModeText + " - nothing assigned.");
                return;
            }

            var report = _shell.ApplyAssignment(assignment);

            // A unit the tables refused - locked by another console, no event for its
            // agency - stays in the queue so the dispatcher can see it did not go.
            var sent = new HashSet<string>(assignment.Units.Select(u => u.UnitNo), StringComparer.OrdinalIgnoreCase);
            var refused = Queued.Where(c => !sent.Contains(c.UnitNo) || report.RefusedUnits.Contains(c.UnitNo)).ToList();
            var assigned = Queued.Except(refused).ToList();

            foreach (var candidate in assigned) Processed.Add(new AssignedUnit(candidate.UnitNo, candidate.Agency));

            Queued.Clear();
            foreach (var candidate in refused) Queued.Add(candidate);
            RaiseQueued();
            Raise(nameof(ProcessedCount));
            Raise(nameof(CloseLabel));

            // The panel stays open: the boards keep a Processed Units tray precisely
            // because one event often takes units from more than one agency, and the
            // legacy screens likewise return to the roster rather than closing.
            LoadCandidates();

            var summary = assigned.Count == 0
                ? ModeText + " - no unit was assigned."
                : ModeText + " - " + string.Join(", ", assigned.Select(c => c.UnitNo)) + " " +
                  ActionText() + " on event " + Call.EventNo + ".";

            ValidationMessage = refused.Count > 0 ? report.Message : string.Empty;
            _shell.SetStatus((summary + " " + report.Message).Trim());
        }

        private string ActionText()
        {
            switch (Action)
            {
                case DispatchAction.EnRoute: return "sent en route";
                case DispatchAction.Arrived: return "marked arrived";
                default: return "dispatched";
            }
        }

        private int ParsedTimeout() =>
            int.TryParse((Timeout ?? string.Empty).Trim(), out var minutes) && minutes > 0 ? minutes : 0;

        /// <summary>
        /// The order the legacy screens check in: an event, at least one unit, and a
        /// timeout that is a number when the screen offers the field at all.
        /// </summary>
        public bool Validate()
        {
            if (Call == null) return Fail("No call selected.");
            if (Queued.Count == 0) return Fail("Select at least one unit to assign.");

            if (HasTimeout && !string.IsNullOrWhiteSpace(Timeout) &&
                !int.TryParse(Timeout.Trim(), out _))
            {
                return Fail("Timeout must be a number of minutes.");
            }

            ValidationMessage = string.Empty;
            return true;
        }

        private bool Fail(string message)
        {
            ValidationMessage = message;
            _shell.SetStatus(ModeText + ": " + message);
            return false;
        }

        private void RaiseQueued()
        {
            Raise(nameof(HasQueued));
            Raise(nameof(QueuedCount));
            Raise(nameof(AssignLabel));
        }
    }
}
