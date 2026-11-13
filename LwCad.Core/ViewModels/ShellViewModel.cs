using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LwCadSystemUX.Functions;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The CAD dashboard. Owns the three call monitors, the unit monitor, the
    /// Quick Menu rail, the Standard Functions bar and the Take Call panel, and keeps the one-second clock
    /// that drives durations, overdue flags and the Take Call timer.
    /// </summary>
    public class ShellViewModel : ObservableObject
    {
        private readonly ICadDataService _data;

        private AppTheme _theme = AppTheme.Light;
        private bool _isRailCollapsed;
        private bool _isFunctionBarCollapsed;
        private string _quickMenuSearch = string.Empty;
        private string _statusMessage = string.Empty;
        private CadCall _selectedCall;
        private int _notificationCount;   // no notification source yet - the badge stays hidden until one exists
        private int _ticksSinceRefresh;
        private long _lastTimerMinute;

        /// <summary>Seconds between monitor re-reads, as gl_CadCycMon paces the legacy screens.</summary>
        public int RefreshSeconds { get; set; } = 5;

        public ShellViewModel() : this(new DemoCadDataService())
        {
        }

        public ShellViewModel(ICadDataService data)
        {
            _data = data;

            // The last two arguments are the monitor's share of the board and the
            // floor the splitter may drag it to, which the original board's
            // proportions set: Stacked the tallest, then Active, then Delayed.
            Active = new CallListViewModel(this, "Active", CallQueue.Active, "Icon.Swap", false, 1.2, 150);
            Stacked = new CallListViewModel(this, "Stacked", CallQueue.Stacked, "Icon.Layers", true, 1.4, 150);
            // Delayed is the one monitor whose chevron collapses it outright rather
            // than fitting it to its rows: some agencies carry a great many delayed
            // calls, and a dozen rows of a list nobody is working is more of the
            // board than it is worth. Its count badge stays visible collapsed, so a
            // call landing there is still seen.
            Delayed = new CallListViewModel(this, "Delayed", CallQueue.Delayed, "Icon.Clock", true,
                                            1.0, 130, shrinksToHeader: true);

            Delayed.Shrink();


            Units = new UnitMonitorViewModel(this);
            TakeCall = new TakeCallViewModel(this, data);
            UnitFunctions = new UnitFunctionViewModel(this, data);
            AssignUnits = new AssignUnitsViewModel(this, data);
            FunctionStub = new FunctionStubViewModel(this);
            CadAlerts = new CadAlertsViewModel(this, data);
            LocationPicker = new LocationPickerViewModel(this);
            SelfInit = new SelfInitViewModel(this, data);
            UnitActivate = new UnitActivateViewModel(this, data);

            // One panel at a time owns the keyboard and the mouse; see IsModalOpen.
            TakeCall.PropertyChanged += OnPanelStateChanged;
            UnitFunctions.PropertyChanged += OnPanelStateChanged;
            AssignUnits.PropertyChanged += OnPanelStateChanged;
            FunctionStub.PropertyChanged += OnPanelStateChanged;
            CadAlerts.PropertyChanged += OnPanelStateChanged;
            LocationPicker.PropertyChanged += OnPanelStateChanged;
            SelfInit.PropertyChanged += OnPanelStateChanged;
            UnitActivate.PropertyChanged += OnPanelStateChanged;

            ToggleThemeCommand = new RelayCommand(() =>
                Theme = Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

            ToggleRailCommand = new RelayCommand(() => IsRailCollapsed = !IsRailCollapsed);
            ToggleFunctionBarCommand = new RelayCommand(() => IsFunctionBarCollapsed = !IsFunctionBarCollapsed);

            SelectCallCommand = new RelayCommand(p => SelectCall(p as CadCall));
            UnitRowMenuCommand = new RelayCommand(p => Units.SelectedUnit = p as CadUnit);
            ToggleCallExpandCommand = new RelayCommand(p => ToggleCallExpand(p as CadCall));
            CallRowMenuCommand = new RelayCommand(p => ShowCallRowMenu(p as CadCall));

            TakeCallCommand = new RelayCommand(OpenTakeCall);
            UnitFunctionCommand = new RelayCommand(p => OpenUnitFunction(p));
            AssignCommand = new RelayCommand(p => OpenAssign(p as CadCall));
            NotImplementedCommand = new RelayCommand(p =>
                SetStatus(Convert.ToString(p) + " is not enabled in this build."));
            RunSlotCommand = new RelayCommand(p => RunSlot(p as KeySlot));
            CadAlertsCommand = new RelayCommand(() => OpenCadAlerts(null));
            RunFunctionCommand = new RelayCommand(p => RunFunction(
                p as CadFunctionInfo ?? (p is string id ? CadFunctionCatalog.Get(id) : null), null));

            ClearStatusCommand = new RelayCommand(() => StatusMessage = string.Empty);

            QuickMenuItems = BuildQuickMenu();
            FunctionKeys = BuildFunctionBank(KeyBank.Function);
            ShiftFunctionKeys = BuildFunctionBank(KeyBank.Shift);
            CtrlFunctionKeys = BuildFunctionBank(KeyBank.Ctrl);
            CallRowActions = BuildCallRowActions();
            UnitRowActions = BuildUnitRowActions();
            ApplyQuickMenuFilter();

            foreach (var call in SourceCalls) Attach(call);
            SourceCalls.CollectionChanged += OnCallsChanged;
            SourceUnits.CollectionChanged += OnUnitsChanged;

            RebuildAll();

            // The one-second clock is driven by the host (Tick), on the UI thread:
            // in Blazor that is the circuit's renderer, so the board never changes
            // under a render.

            SetStatus("CAD dashboard ready. Session " + SessionId + ", position " + E911Position + ".");
        }

        // ------------------------------------------------------------- header

        public string AgencyNamePrimary => _data.AgencyName.Split(' ').FirstOrDefault() ?? _data.AgencyName;

        public string AgencyNameSecondary
        {
            get
            {
                var parts = _data.AgencyName.Split(new[] { ' ' }, 2);
                return parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        public string OperatorId => _data.OperatorId;
        public int SessionId => _data.SessionId;
        public int E911Position => _data.E911Position;

        public int NotificationCount
        {
            get => _notificationCount;
            set
            {
                if (Set(ref _notificationCount, value)) Raise(nameof(HasNotifications));
            }
        }

        public bool HasNotifications => NotificationCount > 0;

        // -------------------------------------------------------------- state

        public ObservableCollection<CadCall> SourceCalls => _data.Calls;
        public ObservableCollection<CadUnit> SourceUnits => _data.Units;

        public CallListViewModel Active { get; }
        public CallListViewModel Stacked { get; }
        public CallListViewModel Delayed { get; }
        public UnitMonitorViewModel Units { get; }
        public TakeCallViewModel TakeCall { get; }

        /// <summary>Unit Status, Location Change and Release Unit share one panel.</summary>
        public UnitFunctionViewModel UnitFunctions { get; }

        /// <summary>
        /// Assign Call and Assign / Assist. One panel runs the four legacy assign
        /// programs - LwCad131 / 132 / 133 / 134 - behind its agency tabs.
        /// </summary>
        public AssignUnitsViewModel AssignUnits { get; }

        /// <summary>A key with no panel in this build opens its notes here.</summary>
        public FunctionStubViewModel FunctionStub { get; }

        /// <summary>CAD Alerts - LwCad301 / LwCad302.</summary>
        public CadAlertsViewModel CadAlerts { get; }

        /// <summary>The LaGeo041-045 location select screens, over Take Call or Self Initiate.</summary>
        public LocationPickerViewModel LocationPicker { get; }

        /// <summary>Self Initiate and Traffic Stop - LwCad107 / LwCad112, one panel.</summary>
        public SelfInitViewModel SelfInit { get; }

        /// <summary>Activate Unit - LwCad129, from the menu or from Self Initiate.</summary>
        public UnitActivateViewModel UnitActivate { get; }

        /// <summary>Calls with an alert waiting - the badge on the top bar's CAD Alerts.</summary>
        public int PendingAlertCount => _data.PendingAlertCallCount;

        public bool HasPendingAlerts => PendingAlertCount > 0;

        /// <summary>Re-reads the badge after the alerts screen has changed something.</summary>
        internal void RefreshAlertBadge()
        {
            Raise(nameof(PendingAlertCount));
            Raise(nameof(HasPendingAlerts));
        }

        /// <summary>The Quick Menu - the KYB3 A-Z keys that carry a function, in letter order.</summary>
        public ObservableCollection<MenuAction> QuickMenuItems { get; }

        /// <summary>The Standard Functions bar, one bank per row: F1-F9, Shift F1-F9, Ctrl F1-F9 (KYB2).</summary>
        public ObservableCollection<MenuAction> FunctionKeys { get; }
        public ObservableCollection<MenuAction> ShiftFunctionKeys { get; }
        public ObservableCollection<MenuAction> CtrlFunctionKeys { get; }

        /// <summary>The keyboard template the two menus were built from.</summary>
        public Keymap Keymap => _data.Keymap;

        /// <summary>What the status line says about the keyboard template at start-up.</summary>
        public string KeymapStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(Keymap.Problem)) return Keymap.Problem;
                return string.IsNullOrEmpty(Keymap.Source) ? string.Empty : "Keys from " + Keymap.Source + ".";
            }
        }

        /// <summary>Actions behind the ellipsis on a call row. They act on <see cref="SelectedCall"/>.</summary>
        public ObservableCollection<MenuAction> CallRowActions { get; }

        /// <summary>Actions behind the ellipsis on a unit row. They act on the selected unit.</summary>
        public ObservableCollection<MenuAction> UnitRowActions { get; }

        public AppTheme Theme
        {
            get => _theme;
            set
            {
                if (Set(ref _theme, value))
                {
                    Raise(nameof(IsDark));
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsDark => Theme == AppTheme.Dark;

        public event EventHandler ThemeChanged;

        public bool IsRailCollapsed
        {
            get => _isRailCollapsed;
            set
            {
                if (Set(ref _isRailCollapsed, value)) Raise(nameof(IsRailExpanded));
            }
        }

        public bool IsRailExpanded => !IsRailCollapsed;

        /// <summary>The Standard Functions bar folded to its header, as the legacy ExpFunctions expander.</summary>
        public bool IsFunctionBarCollapsed
        {
            get => _isFunctionBarCollapsed;
            set
            {
                if (Set(ref _isFunctionBarCollapsed, value)) Raise(nameof(IsFunctionBarExpanded));
            }
        }

        public bool IsFunctionBarExpanded => !IsFunctionBarCollapsed;

        public string QuickMenuSearch
        {
            get => _quickMenuSearch;
            set
            {
                if (Set(ref _quickMenuSearch, value)) ApplyQuickMenuFilter();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (Set(ref _statusMessage, value)) Raise(nameof(HasStatus));
            }
        }

        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        /// <summary>
        /// The one call the dispatcher is working. Held on the shell rather than
        /// on a grid so the highlight follows the event when it moves between the
        /// Stacked, Active and Delayed monitors.
        /// </summary>
        public CadCall SelectedCall
        {
            get => _selectedCall;
            private set
            {
                var previous = _selectedCall;
                if (!Set(ref _selectedCall, value)) return;

                if (previous != null) previous.IsSelected = false;
                if (value != null) value.IsSelected = true;

                Raise(nameof(HasSelectedCall));
                RefreshCallRowActions();
            }
        }

        public bool HasSelectedCall => SelectedCall != null;

        // ------------------------------------------------------------- modality

        /// <summary>
        /// A panel is holding the console. The board behind it is dimmed, disabled
        /// and out of the tab order while this is true - dimming alone let the
        /// operator tab off a panel and run an unrelated function behind it.
        ///
        /// Take Call collapsed to its rail is deliberately not modal: parking a call
        /// on the rail to work the board is the whole point of that rail.
        /// </summary>
        public bool IsModalOpen =>
            (TakeCall.IsOpen && TakeCall.IsExpanded) || UnitFunctions.IsOpen || AssignUnits.IsOpen ||
            FunctionStub.IsOpen || CadAlerts.IsOpen || LocationPicker.IsOpen || SelfInit.IsOpen || UnitActivate.IsOpen;

        /// <summary>
        /// A function panel has opened over a Take Call, expanded or parked. Take
        /// Call is above the board's scrim, so it needs its own to stop the operator
        /// reaching back into it - including reopening it from the collapsed rail.
        /// </summary>
        public bool IsPanelOverTakeCall =>
            TakeCall.IsOpen && (UnitFunctions.IsOpen || AssignUnits.IsOpen || FunctionStub.IsOpen || CadAlerts.IsOpen ||
                                LocationPicker.IsOpen || SelfInit.IsOpen || UnitActivate.IsOpen);

        /// <summary>The location picker or Activate Unit has opened over Self Initiate / Traffic Stop.</summary>
        public bool IsPanelOverSelfInit => SelfInit.IsOpen && (LocationPicker.IsOpen || UnitActivate.IsOpen);

        private void OnPanelStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TakeCallViewModel.IsOpen) &&
                e.PropertyName != nameof(TakeCallViewModel.IsCollapsed) &&
                e.PropertyName != nameof(TakeCallViewModel.IsExpanded))
            {
                return;
            }

            Raise(nameof(IsModalOpen));
            Raise(nameof(IsPanelOverTakeCall));
            Raise(nameof(IsPanelOverSelfInit));
        }

        /// <summary>
        /// Refuses a second panel while one is already holding the console, and says
        /// so rather than stacking them. Reached from the Ctrl+K chords, which are
        /// the one way in that the scrim cannot block.
        /// </summary>
        private bool BlockedByModal(string wanted)
        {
            if (!IsModalOpen) return false;

            SetStatus(wanted + " cannot be opened while " + OpenPanelName() + " is open. " +
                      "Finish or cancel it first - Esc closes it.");
            return true;
        }

        private string OpenPanelName()
        {
            if (UnitActivate.IsOpen) return UnitActivate.Title;
            if (LocationPicker.IsOpen) return "the location select screen";
            if (CadAlerts.IsOpen) return CadAlerts.Title;
            if (FunctionStub.IsOpen) return FunctionStub.Title;
            if (AssignUnits.IsOpen) return AssignUnits.Title;
            if (UnitFunctions.IsOpen) return UnitFunctions.Title;
            if (SelfInit.IsOpen) return SelfInit.Title;
            return "Take Call";
        }

        // ----------------------------------------------------------- commands

        public RelayCommand ToggleThemeCommand { get; }
        public RelayCommand ToggleRailCommand { get; }
        public RelayCommand ToggleFunctionBarCommand { get; }
        public RelayCommand SelectCallCommand { get; }
        public RelayCommand ToggleCallExpandCommand { get; }
        public RelayCommand CallRowMenuCommand { get; }
        public RelayCommand UnitRowMenuCommand { get; }
        public RelayCommand TakeCallCommand { get; }
        public RelayCommand UnitFunctionCommand { get; }
        public RelayCommand AssignCommand { get; }
        public RelayCommand NotImplementedCommand { get; }

        /// <summary>Runs a keyboard key - a Quick Menu entry or a Standard Functions cell.</summary>
        public RelayCommand RunSlotCommand { get; }

        /// <summary>The top bar's CAD Alerts: every call with an alert waiting (img_CadAlert).</summary>
        public RelayCommand CadAlertsCommand { get; }

        /// <summary>Runs a catalogued function directly - the row menus. Takes the entry or its id.</summary>
        public RelayCommand RunFunctionCommand { get; }
        public RelayCommand ClearStatusCommand { get; }

        /// <summary>Puts text on this user's clipboard; the Blazor page wires it to JS interop.</summary>
        public Action<string> CopyToClipboard { get; set; }

        public void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
        }

        public void SelectCall(CadCall call)
        {
            if (call == null) return;

            SelectedCall = call;

            // Unconditional: re-selecting the same call leaves the property unchanged,
            // so its setter does not run - but the call's units may have moved since.
            RefreshCallRowActions();

            SetStatus(call.EventNo + "  " + call.CallType + "  " + call.Location +
                      "  priority " + call.PriorityNumber + ".");
        }

        /// <summary>
        /// Which assignment the selected call takes. A call already running on the
        /// Active monitor gets units added to it - an assist - rather than being
        /// assigned from scratch. The Quick Menu entry and the row menu entry are
        /// renamed to match, because they run the same program either way and the
        /// only thing telling the dispatcher which is which is the wording.
        /// </summary>
        public AssignMode AssignModeFor(CadCall call) =>
            call != null && call.Queue == CallQueue.Active ? AssignMode.Assist : AssignMode.Call;

        /// <summary>
        /// Fits the call row menu to the row it was opened from: what the assign
        /// entry is called, and whether the case number entry belongs there at all.
        /// </summary>
        private void RefreshCallRowActions()
        {
            var assist = AssignModeFor(SelectedCall) == AssignMode.Assist;

            // A key's caption is the site's KYB literal; it is only replaced while the
            // selected call is Active, and put back afterwards.
            foreach (var action in _assignActions)
            {
                action.Title = assist ? "Assign / Assist" : action.BaseTitle;
            }

            // A case number is assigned to a unit, not to an event, so the entry only
            // belongs on a call that has units on it. A stacked call has none - that
            // is what being stacked means - and neither does a delayed one.
            if (_rowCaseNoAction != null)
            {
                _rowCaseNoAction.IsVisible = SelectedCall != null && SelectedCall.HasAssignedUnits;
            }
        }

        internal void ToggleCallExpand(CadCall call)
        {
            if (call == null) return;

            call.IsExpanded = !call.IsExpanded;
            if (call.IsExpanded) SelectCall(call);
        }

        private void ShowCallRowMenu(CadCall call)
        {
            SelectCall(call);
        }

        private void OpenTakeCall()
        {
            if (BlockedByModal("Take Call")) return;

            TakeCall.Open();
        }

        /// <summary>
        /// Opens a unit function on the unit the dispatcher is pointing at - the row
        /// the menu was opened from, or whatever the Unit Monitor has selected when
        /// the function comes from the Quick Menu.
        /// </summary>
        private void OpenUnitFunction(object parameter)
        {
            if (BlockedByModal("A unit function")) return;

            var function = UnitFunction.Status;

            if (parameter is UnitFunction typed) function = typed;
            else if (parameter is string name && Enum.TryParse(name.Replace(" ", string.Empty), out UnitFunction parsed))
            {
                function = parsed;
            }

            UnitFunctions.Open(function, Units.SelectedUnit);
        }

        /// <summary>
        /// Opens the assign panel on a call - the row the menu was opened from, or
        /// whatever the board has selected when the function comes from the Quick
        /// Menu. Which of the two assignments it runs follows the call's monitor.
        /// </summary>
        private void OpenAssign(CadCall call)
        {
            if (BlockedByModal(AssignModeFor(call ?? SelectedCall) == AssignMode.Assist
                                   ? "Assign / Assist"
                                   : "Assign Call"))
            {
                return;
            }

            var target = call ?? SelectedCall;

            if (target == null)
            {
                SetStatus("Assign Call: select a call on one of the monitors first.");
                return;
            }

            SelectCall(target);
            AssignUnits.Open(target, AssignModeFor(target));
        }

        /// <summary>Applies a unit function and re-reads the monitors so counts follow.</summary>
        internal WriteReport ApplyUnitChange(UnitChange change)
        {
            var report = _data.ApplyUnitChange(change);
            Units.Rebuild();
            RebuildAll();
            _vanishedSelection = null;
            return report;
        }

        /// <summary>Applies an assignment and re-reads the monitors so the call re-files.</summary>
        internal WriteReport ApplyAssignment(UnitAssignment assignment)
        {
            var report = _data.AssignUnits(assignment);
            Units.Rebuild();
            RebuildAll();
            _vanishedSelection = null;
            return report;
        }

        /// <summary>
        /// Holds every monitor - Esc on the LW console. Each list's own lock button
        /// releases it again, as the per-list lock does on the legacy console.
        /// </summary>
        public void LockAllLists()
        {
            Stacked.IsLocked = true;
            Active.IsLocked = true;
            Delayed.IsLocked = true;
            Units.IsLocked = true;
            SetStatus("All lists locked - auto refresh held. Use each list's lock to release it.");
        }

        /// <summary>
        /// Asks the operator to confirm - LwCadSystem's "Save record?" before a call
        /// is committed. The page sets this; the default answers yes so a view
        /// model never waits on a dialog nobody can see.
        ///
        /// Web port: WPF answered through a blocking MessageBox. A Blazor Server
        /// circuit cannot block on the user, so the question is awaited instead and
        /// the flows that ask it (Take Call / Self Initiate save and cancel, the
        /// cross-jurisdiction assign, activating an unknown unit) are async. The
        /// questions, their order and what each answer does are unchanged.
        /// </summary>
        public Func<string, Task<bool>> ConfirmAsync { get; set; } = _ => Task.FromResult(true);

        /// <summary>Commits a Take Call draft and leaves the new event selected.</summary>
        private void OpenSelfInit(SelfInitMode mode)
        {
            if (BlockedByModal(mode == SelfInitMode.TrafficStop ? "Traffic Stop" : "Self Initiate")) return;

            SelfInit.Open(mode);
        }

        /// <summary>
        /// Activate Unit, on a unit (from Self Initiate) or with the unit to type
        /// (from the menu). <paramref name="activated"/> runs only after a save.
        /// </summary>
        internal void OpenActivateUnit(string unitNo, Action<string> activated)
        {
            UnitActivate.Open(unitNo, activated);
        }

        /// <summary>Commits a Self Initiate / Traffic Stop and re-files the board; throws when nothing was written.</summary>
        internal CadCall SelfInitiate(SelfInitDraft draft)
        {
            var call = _data.SelfInitiate(draft);
            Units.Rebuild();
            RebuildAll();
            _vanishedSelection = null;
            if (!call.IsCompleted) SelectedCall = call;
            return call;
        }

        internal WriteReport ActivateUnit(UnitActivation activation)
        {
            var report = _data.ActivateUnit(activation);
            if (!report.Failed)
            {
                Units.Rebuild();
                RebuildAll();
            }

            return report;
        }

        internal CadCall DispatchCall(NewCallDraft draft)
        {
            var call = _data.Dispatch(draft);
            RebuildAll();
            _vanishedSelection = null;
            SelectedCall = call;
            return call;
        }

        // ------------------------------------------------- quick menu and keys

        /// <summary>
        /// The Quick Menu rail - the KYB3 A-Z keys of the session's template, in
        /// letter order, as LwCadSystem's ExpXFunctions lists them. Blank keys are
        /// left off the rail (the legacy showed them as disabled buttons); the chord
        /// is Ctrl+K and the key's letter, where the legacy used Alt and the letter.
        /// Search stays at the top of the rail and filters these.
        /// </summary>
        private ObservableCollection<MenuAction> BuildQuickMenu()
        {
            var items = new ObservableCollection<MenuAction>();

            foreach (var slot in Keymap.Quick.Where(s => !s.IsEmpty))
            {
                items.Add(BuildKeyAction(slot, slot.Position));
            }

            return items;
        }

        /// <summary>
        /// One row of the Standard Functions bar - nine KYB2 keys of one bank. All
        /// nine cells are kept, blank ones disabled, so each key stays where the
        /// template put it (CadMain MapCadKeyboard).
        /// </summary>
        private ObservableCollection<MenuAction> BuildFunctionBank(KeyBank bank)
        {
            var items = new ObservableCollection<MenuAction>();

            foreach (var slot in Keymap.Standard.Where(s => s.Bank == bank))
            {
                items.Add(slot.IsEmpty
                    ? new MenuAction(string.Empty, null, false, RunSlotCommand, slot)
                    {
                        KeyHint = slot.KeyHint,
                        Detail = slot.KeyText + " - no function on this key",
                        IsBlank = true
                    }
                    : BuildKeyAction(slot, null));
            }

            return items;
        }

        private MenuAction BuildKeyAction(KeySlot slot, string chordKey)
        {
            var function = CadFunctionCatalog.Resolve(slot);
            var title = string.IsNullOrWhiteSpace(slot.Literal) ? function.Title : slot.Literal;

            var action = new MenuAction(title, chordKey, true, RunSlotCommand, slot)
            {
                KeyHint = chordKey == null ? slot.KeyHint : null,
                IsStub = !function.IsImplemented,
                Detail = slot.KeyText + "  -  " + (function.IsUncatalogued ? slot.ProgramId + " (uncatalogued)" : function.Legacy) +
                         (function.IsImplemented ? string.Empty : "  -  not built yet, opens its notes")
            };

            if (function.Handler == FunctionHandler.Assign) _assignActions.Add(action);
            return action;
        }

        /// <summary>
        /// The call row menu. These are the same functions the keys offer, reached
        /// from the row the dispatcher is already pointing at; the ones not built
        /// yet open their notes, as a key does.
        /// </summary>
        private ObservableCollection<MenuAction> BuildCallRowActions()
        {
            var rowAssign = new MenuAction("Assign Call", "Q", true, AssignCommand);
            _assignActions.Add(rowAssign);

            _rowCaseNoAction = RowFunction("Asgn. Case No", "CAD144");
            _rowCaseNoAction.IsVisible = false;

            return new ObservableCollection<MenuAction>
            {
                rowAssign,
                RowFunction("Release Call", "CAD142"),
                RowFunction("Vw Act Call", "CAD699"),
                RowFunction("Call Alerts", "CAD301"),
                _rowCaseNoAction
            };
        }

        private MenuAction RowFunction(string title, string cataloguedId)
        {
            var function = CadFunctionCatalog.Get(cataloguedId);
            return new MenuAction(title, null, true, RunFunctionCommand, function)
            {
                IsStub = !function.IsImplemented,
                Detail = function.Legacy
            };
        }

        /// <summary>
        /// Every entry that runs the assign program - they are renamed Assign / Assist
        /// together while an Active call is selected. See RefreshCallRowActions.
        /// </summary>
        private readonly List<MenuAction> _assignActions = new List<MenuAction>();

        /// <summary>The row entry that only belongs on a call with units on it.</summary>
        private MenuAction _rowCaseNoAction;

        /// <summary>The unit row menu - the unit functions planned for this console.</summary>
        private ObservableCollection<MenuAction> BuildUnitRowActions()
        {
            return new ObservableCollection<MenuAction>
            {
                new MenuAction("Unit Status", null, true, UnitFunctionCommand, UnitFunction.Status),
                new MenuAction("Release Unit", null, true, UnitFunctionCommand, UnitFunction.Release),
                new MenuAction("Location Change", null, true, UnitFunctionCommand, UnitFunction.Location)
            };
        }

        private void ApplyQuickMenuFilter()
        {
            var term = (QuickMenuSearch ?? string.Empty).Trim();
            foreach (var item in QuickMenuItems)
            {
                item.IsVisible = term.Length == 0 ||
                                 item.Title.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>Runs the Ctrl+K chord completion - Ctrl+K then the Quick Menu key's letter.</summary>
        public bool InvokeShortcut(string key)
        {
            var item = QuickMenuItems.FirstOrDefault(
                i => string.Equals(i.ShortcutKey, key, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                SetStatus("Ctrl+K " + key + " - no Quick Menu function on that letter.");
                return true;
            }

            item.Command.Execute(item.CommandParameter);
            return true;
        }

        /// <summary>
        /// F1-F9 with no modifier, Shift or Ctrl - one Standard Functions key, as
        /// CadMain's Window_KeyDown maps them onto btn_FuntionF1..CF9.
        /// </summary>
        public void InvokeFunctionKey(KeyBank bank, int number)
        {
            var slot = Keymap.Find(bank, number.ToString());

            if (slot == null || slot.IsEmpty)
            {
                SetStatus((slot?.KeyText ?? "That key") + " has no function in this keyboard template.");
                return;
            }

            RunSlot(slot);
        }

        /// <summary>Runs a key: resolves its program id through the catalogue and opens what it names.</summary>
        private void RunSlot(KeySlot slot)
        {
            if (slot == null || slot.IsEmpty) return;

            RunFunction(CadFunctionCatalog.Resolve(slot), slot);
        }

        /// <summary>
        /// Opens a function: one of the panels this build has, or - for everything
        /// else - its stub, carrying the legacy program's notes.
        /// </summary>
        private void RunFunction(CadFunctionInfo function, KeySlot slot)
        {
            if (function == null) return;

            switch (function.Handler)
            {
                case FunctionHandler.TakeCall:
                    OpenTakeCall();
                    return;

                // LwCad140 is the assist program, but which assignment runs follows the
                // selected call's monitor either way - see AssignModeFor.
                case FunctionHandler.Assign:
                case FunctionHandler.Assist:
                    OpenAssign(null);
                    return;

                case FunctionHandler.UnitStatus:
                    OpenUnitFunction(UnitFunction.Status);
                    return;

                case FunctionHandler.ReleaseUnit:
                    OpenUnitFunction(UnitFunction.Release);
                    return;

                case FunctionHandler.LocationChange:
                    OpenUnitFunction(UnitFunction.Location);
                    return;

                case FunctionHandler.SelfInitiate:
                    OpenSelfInit(SelfInitMode.SelfInitiate);
                    return;

                case FunctionHandler.TrafficStop:
                    OpenSelfInit(SelfInitMode.TrafficStop);
                    return;

                case FunctionHandler.ActivateUnit:
                    if (BlockedByModal("Activate Unit")) return;
                    OpenActivateUnit(null, null);
                    return;

                // From a key or a row menu the legacy hands LwCad301 the selected call.
                case FunctionHandler.CadAlerts:
                    OpenCadAlerts(SelectedCall);
                    return;
            }

            // CAD Mapping is a web app now: the host opens it on the selected call (and unit),
            // as btn_Map started LwCadMapping.exe with the call's link string. Not blocked by an
            // open panel - the map opens in its own tab and leaves the panel as it is.
            if (OpenMapping != null && IsMappingFunction(function, slot))
            {
                var call = SelectedCall;
                var unit = Units.SelectedUnit;
                var opened = OpenMapping(call, unit);
                SetStatus(!opened
                    ? "The browser blocked the CAD Mapping window. Allow pop-ups for this site, then press Map again."
                    : call != null
                        ? "CAD Mapping opened on event " + call.EventNo + "."
                        : unit != null ? "CAD Mapping opened on unit " + unit.UnitNo + "." : "CAD Mapping opened.");
                return;
            }

            var title = slot != null && !string.IsNullOrWhiteSpace(slot.Literal) ? slot.Literal : function.Title;
            if (BlockedByModal(title)) return;

            // Not rebuilt here: run the legacy program when it is installed beside the
            // console, as WinA.LaunchProgram would; otherwise open its notes.
            var program = InstalledProgram(function, slot);
            if (program != null)
            {
                LaunchLegacyProgram(program, null);
                return;
            }

            FunctionStub.Open(function, slot);
        }

        /// <summary>
        /// Opens the web CAD Mapping app on (call, unit) - either may be null - and returns false
        /// when the browser refused the window. Set per user by the web host; when it is null the
        /// Map function falls back to the legacy LwCadMapping program or its notes.
        /// </summary>
        public Func<CadCall, CadUnit, bool> OpenMapping { get; set; }

        /// <summary>The catalogue's map function (CADMAPPING / CADMAPPINGGDB), however the key names it.</summary>
        private static bool IsMappingFunction(CadFunctionInfo function, KeySlot slot)
        {
            static bool IsMap(string id)
            {
                var name = KeymapLoader.NormalizeProgramId(id ?? string.Empty);
                return name.EndsWith("CADMAPPING", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith("CADMAPPINGGDB", StringComparison.OrdinalIgnoreCase);
            }

            return function.Programs.Any(IsMap) || (slot != null && IsMap(slot.ProgramId));
        }

        /// <summary>The key's program, or the catalogued one with this agency's prefix, when installed.</summary>
        private static string InstalledProgram(CadFunctionInfo function, KeySlot slot)
        {
            if (slot != null && !string.IsNullOrWhiteSpace(slot.ProgramId) && LegacyLauncher.IsInstalled(slot.ProgramId))
            {
                return slot.ProgramId;
            }

            var prefix = AgencyPrefix();
            foreach (var id in function.Programs)
            {
                if (LegacyLauncher.IsInstalled(prefix + id)) return prefix + id;
                if (LegacyLauncher.IsInstalled(id)) return id;
            }

            return null;
        }

        private static string AgencyPrefix()
        {
            try
            {
                var id = new LaConfig.LaGetConfig().AgencyId;
                return string.IsNullOrWhiteSpace(id) ? "LW" : id.Trim().ToUpperInvariant();
            }
            catch
            {
                return "LW";
            }
        }

        /// <summary>
        /// Starts a legacy program with the selected call and unit - WinA.LaunchProgram.
        /// LwCadMapping and LwCadUnitMonitor warn when already running, as the legacy
        /// buttons do. False when the program is not installed.
        /// </summary>
        public bool LaunchLegacyProgram(string programId, LegacyContext context)
        {
            var name = KeymapLoader.NormalizeProgramId(programId);
            if (!LegacyLauncher.IsInstalled(name)) return false;

            // The "already running" check looks at this machine's processes, which on a
            // web server are not the operator's - so it only runs without a host hook.
            if (CadHost.LaunchLegacy == null &&
                (name.EndsWith("CADMAPPING") || name.EndsWith("CADUNITMONITOR")) &&
                System.Diagnostics.Process.GetProcesses().Any(p => SafeName(p).Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus(name + " is already running.");
                return true;
            }

            var call = SelectedCall;
            var unit = Units.SelectedUnit;
            context ??= new LegacyContext
            {
                EventType = call?.EventType ?? ' ',
                EventNo1 = call?.EventNo1 ?? 0,
                EventNo2 = call?.EventNo2 ?? 0,
                SKey = call?.EventKey ?? 0,
                DispNo1 = unit?.DispNo1 ?? 0,
                DispNo2 = unit?.DispNo2 ?? 0,
                UnitNo = unit?.UnitNo ?? string.Empty
            };

            LegacyLauncher.Launch(name, context, out var message);
            SetStatus(message);
            return true;
        }

        private static string SafeName(System.Diagnostics.Process p)
        {
            try
            {
                return p.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// TakeCall_SaveDSE1's follow-ons, in its order: the call alert (LwCad302),
        /// then the assignment (LwCad130). One panel holds the console at a time, so
        /// the assign panel opens when the alerts screen closes.
        /// </summary>
        internal void AfterTakeCall(CadCall call, bool alert, bool assign)
        {
            if (call == null) return;

            if (alert)
            {
                SelectCall(call);
                CadAlerts.Open(call, assign ? () => OpenAssign(call) : (Action)null);
                return;
            }

            if (assign) OpenAssign(call);
        }

        /// <summary>Opens CAD Alerts, on one call or - with none - on the list.</summary>
        private void OpenCadAlerts(CadCall forCall)
        {
            if (BlockedByModal("CAD Alerts")) return;

            CadAlerts.Open(forCall);
        }

        // ------------------------------------------------------------- clock

        /// <summary>The one-second board clock. The host calls it on its UI context.</summary>
        public void Tick() => OnClockTick(this, EventArgs.Empty);

        private void OnClockTick(object sender, EventArgs e)
        {
            // Durations and overdue flags change by the minute, so they are refreshed once a
            // minute rather than every second (fewer re-renders for every user on the board).
            var minute = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
            if (minute != _lastTimerMinute)
            {
                _lastTimerMinute = minute;
                foreach (var call in SourceCalls) call.TickTimers();
                foreach (var unit in SourceUnits) unit.TickTimers();
                Units.RaiseCounts();
            }

            TakeCall.Tick();

            if (++_ticksSinceRefresh < RefreshSeconds) return;
            _ticksSinceRefresh = 0;

            try
            {
                _data.Refresh();
            }
            catch (Exception ex)
            {
                SetStatus("Monitor refresh failed: " + ex.Message);
                return;
            }

            RebuildAll();
            RefreshAlertBadge();
            ReportVanishedSelection();
        }

        // ------------------------------------------- the highlight is the call
        //
        // Web port (2026-11-12): several dispatchers work one board, so another
        // console can move, re-order or close the call this operator has
        // highlighted. The highlight is held on the call object, which the data
        // services update in place by key, so it follows a call between monitors
        // by itself. Two cases need help, and are handled here:
        //  - a refresh that replaces the call object for the same event (a stack
        //    row re-created rather than updated): the highlight is re-attached to
        //    the new object for the same event number;
        //  - the call leaving the board (closed or released by another console):
        //    the status line says so, rather than the highlight silently going.

        /// <summary>The highlighted call that left the board during the current change, if any.</summary>
        private CadCall _vanishedSelection;

        private void ReportVanishedSelection()
        {
            var gone = _vanishedSelection;
            _vanishedSelection = null;
            if (gone == null || SelectedCall != null) return;

            SetStatus("Event " + gone.EventNo + " (" + gone.CallType + ") is no longer on the board - " +
                      "it was closed or removed, possibly by another console.");
        }

        private static bool SameEvent(CadCall a, CadCall b) =>
            a != null && b != null && a.EventType == b.EventType &&
            !string.IsNullOrEmpty(a.EventNo) && string.Equals(a.EventNo, b.EventNo, StringComparison.Ordinal);

        private void OnCallsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CadCall call in e.OldItems)
                {
                    Detach(call);
                    if (ReferenceEquals(call, SelectedCall))
                    {
                        _vanishedSelection = call;
                        SelectedCall = null;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (CadCall call in e.NewItems)
                {
                    Attach(call);

                    // The same event under a new object: the highlight goes with it.
                    if (SelectedCall == null && SameEvent(call, _vanishedSelection))
                    {
                        _vanishedSelection = null;
                        SelectedCall = call;
                    }
                }
            }

            RebuildAll();
        }

        private void OnUnitsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Units.Rebuild();
        }

        private void Attach(CadCall call)
        {
            call.PropertyChanged += OnCallPropertyChanged;
        }

        private void Detach(CadCall call)
        {
            call.PropertyChanged -= OnCallPropertyChanged;
        }

        private void OnCallPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // A call changing monitors is the one property that re-files the rows.
            if (e.PropertyName == nameof(CadCall.Queue))
            {
                // Closed in place (not removed): it is on no monitor any more, so the
                // highlight cannot follow it - say so instead.
                if (ReferenceEquals(sender, SelectedCall) && SelectedCall.Queue == CallQueue.Closed)
                {
                    _vanishedSelection = SelectedCall;
                    SelectedCall = null;
                    ReportVanishedSelection();
                }

                RebuildAll();

                // A selected call moving to Active turns its Assign Call into an
                // Assign / Assist without the selection itself changing, so the rename
                // has to be driven from here as well.
                if (ReferenceEquals(sender, SelectedCall)) RefreshCallRowActions();
                return;
            }

            // Cursoring with the arrow keys selects the row's container, which writes
            // straight back to the model. Picking that up here is what clears the
            // previous selection, including one sitting in a different monitor.
            if (e.PropertyName == nameof(CadCall.IsSelected) &&
                sender is CadCall call && call.IsSelected && !ReferenceEquals(call, SelectedCall))
            {
                SelectCall(call);
            }
        }

        private void RebuildAll()
        {
            Active.Rebuild();
            Stacked.Rebuild();
            Delayed.Rebuild();
            Units.Rebuild();
        }
    }
}
