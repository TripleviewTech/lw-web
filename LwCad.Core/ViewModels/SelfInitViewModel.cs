using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;
using LwCadSystemUX.Services.Location;
using LwCadSystemUX.Services.SelfInit;
using LwCadSystemUX.Services.TakeCall;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// Self Initiate and Traffic Stop - LwCadSystem SelfInit.cs and TraffStop.cs
    /// (LwCad107 / LwCad112), one panel in two modes. An officer reports a call
    /// they are already on: the unit is typed first, the event is its agency's,
    /// and on save the unit is on the event, en route or arrived, and the event
    /// is on the Active monitor.
    ///
    /// The location block is Take Call's (LaGeo040 and the select screens). What
    /// the legacy forms add:
    /// - the unit, checked against RST1 - a unit not logged on is offered Activate
    ///   Unit (LwCad129); a unit on another call is released from it first, with
    ///   a disposition (LwCad123);
    /// - one priority, seeded from the call code for the unit's agency;
    /// - CAD7_SELF_E / CAD7_TRAF_E: en route, arrived, or ask "Has Unit/Apparatus arrived?";
    /// - Traffic Stop: police units only, the CCC1_TS_CODE call code, the plate
    ///   block first and the racial profile (MLB2 05/60-66, DSU1_R_PROF_*).
    ///
    /// Legacy defects not carried over are listed in the README.
    /// </summary>
    public class SelfInitViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private SelfInitMode _mode;
        private bool _isOpen;
        private DateTime _openedAt;

        private string _unitNo = string.Empty;
        private RosterUnit _unit;
        private string _checkedUnit = string.Empty;
        private LookupItem _releaseDisposition;

        private CallCodeItem _callCode;
        private string _priority = "0";

        private string _jurisdiction = string.Empty;
        private string _policeCode = string.Empty;
        private string _fireCode = string.Empty;
        private string _emsCode = string.Empty;
        private string _supportCode = string.Empty;
        private string _towCode = string.Empty;
        private string _force = "N";
        private string _location = string.Empty;
        private string _premise = string.Empty;
        private LocationResult _place;
        private bool _applyingPlace;
        private bool _saveAfterPick;

        private LookupItem _origin;
        private LookupItem _disposition;
        private string _narrative = string.Empty;

        private string _profileOfficer = string.Empty;

        private string _validationMessage = string.Empty;
        private string _notice = string.Empty;

        private int _historyCount;
        private int _duplicateCount;
        private int _sopCount;
        private PatrolHit _patrol = new PatrolHit();
        private string _infoTitle = string.Empty;
        private bool _isInfoOpen;

        private bool _saving;

        public SelfInitViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            SaveCommand = new RelayCommand(() => _ = SaveAsync());
            CancelCommand = new RelayCommand(() => _ = CancelAsync());
            ValidateUnitCommand = new RelayCommand(() => ValidateUnit());
            ListPremisesCommand = new RelayCommand(ListPremises);
            ClearPremiseCommand = new RelayCommand(ClearPremise);
            ShowHistoryCommand = new RelayCommand(ShowHistory);
            ShowDuplicatesCommand = new RelayCommand(ShowDuplicates);
            ShowPatrolCommand = new RelayCommand(ShowPatrol);
            ShowSopCommand = new RelayCommand(ShowSop);
            ShowLocationCommand = new RelayCommand(ShowLocation);
            CloseInfoCommand = new RelayCommand(() => IsInfoOpen = false);
        }

        private ITakeCallSupport TakeCall => _data.TakeCallSupport;
        private ISelfInitSupport Support => _data.SelfInitSupport;
        private TakeCallSettings Settings => TakeCall?.Settings ?? new TakeCallSettings();

        // ------------------------------------------------------------ lookups

        public IReadOnlyList<CallCodeItem> CallCodes => _data.CallCodes;
        public IReadOnlyList<LookupItem> Origins => _data.Origins;
        public IReadOnlyList<LookupItem> Dispositions => TakeCall?.Dispositions ?? Array.Empty<LookupItem>();
        public IReadOnlyList<string> JurisdictionCodes => _data.Jurisdictions.Select(j => j.Code).ToList();
        public IReadOnlyList<string> States => _data.States;
        public IReadOnlyList<string> PlateTypes => (TakeCall?.PlateTypes ?? Array.Empty<LookupItem>()).Select(p => p.Code).ToList();
        public IReadOnlyList<string> ForceFlags { get; } = new[] { "N", "Y", "X" };

        public IReadOnlyList<string> Races => ProfileValues(60);
        public IReadOnlyList<string> Sexes => ProfileValues(61);
        public IReadOnlyList<string> Sectors => ProfileValues(62);
        public IReadOnlyList<string> Discretions => ProfileValues(63);
        public IReadOnlyList<string> Residents => ProfileValues(64);
        public IReadOnlyList<string> CitationWarnings => ProfileValues(65);
        public IReadOnlyList<string> Searches => ProfileValues(66);

        private readonly Dictionary<int, IReadOnlyList<string>> _profiles = new Dictionary<int, IReadOnlyList<string>>();

        private IReadOnlyList<string> ProfileValues(int library)
        {
            if (!_profiles.TryGetValue(library, out var list))
            {
                list = Support?.ProfileList(library) ?? new[] { string.Empty };
                _profiles[library] = list;
            }

            return list;
        }

        // ------------------------------------------------------------- state

        public SelfInitMode Mode
        {
            get => _mode;
            private set
            {
                if (!Set(ref _mode, value)) return;
                Raise(nameof(IsTrafficStop));
                Raise(nameof(IsSelfInitiate));
                Raise(nameof(Title));
            }
        }

        public bool IsTrafficStop => Mode == SelfInitMode.TrafficStop;
        public bool IsSelfInitiate => Mode == SelfInitMode.SelfInitiate;

        /// <summary>The legacy window titles, less "Computer Assisted Dispatch - ".</summary>
        public string Title => IsTrafficStop ? "Traffic Stop" : "Self Initiate";

        public bool IsOpen { get => _isOpen; private set => Set(ref _isOpen, value); }

        public string ReceivedText => _openedAt == default ? string.Empty : "Received " + _openedAt.ToString("HH:mm");

        public string ValidationMessage
        {
            get => _validationMessage;
            private set { if (Set(ref _validationMessage, value)) Raise(nameof(HasValidationMessage)); }
        }

        public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

        public string Notice
        {
            get => _notice;
            private set { if (Set(ref _notice, value)) Raise(nameof(HasNotice)); }
        }

        public bool HasNotice => !string.IsNullOrEmpty(Notice);

        // --------------------------------------------------------------- unit

        public string UnitNo
        {
            get => _unitNo;
            set
            {
                if (!Set(ref _unitNo, (value ?? string.Empty).ToUpperInvariant())) return;
                if (!string.Equals(_checkedUnit, Clean(value), StringComparison.OrdinalIgnoreCase)) ClearUnit();
            }
        }

        public bool IsUnitValidated => _unit != null;

        /// <summary>"LWPD  Police  -  4101 SMITH, JOHN" - the agency and the officers from RST1.</summary>
        public string UnitSummary
        {
            get
            {
                if (_unit == null) return string.Empty;
                var officers = new[] { Officer(_unit.Officer1, _unit.Officer1Name), Officer(_unit.Officer2, _unit.Officer2Name) }
                    .Where(o => o.Length > 0);
                return (_unit.Agency + "  " + AgencyName(_unit.AgencyType) + "  " + string.Join("  /  ", officers)).Trim();
            }
        }

        /// <summary>The unit is on another call - it is released from it first (LwCad123).</summary>
        public bool UnitIsBusy => _unit != null && _unit.IsOnCall;

        public string BusyText => UnitIsBusy ? "On " + _unit.EventText + " - released from it first, with this disposition" : string.Empty;

        /// <summary>LwCad123 tbxNV_DispCode - defaults to 999.</summary>
        public LookupItem ReleaseDisposition { get => _releaseDisposition; set => Set(ref _releaseDisposition, value); }

        private char AgencyType => _unit?.AgencyType ?? ' ';

        // --------------------------------------------------------- call code

        public CallCodeItem CallCode
        {
            get => _callCode;
            set
            {
                if (!Set(ref _callCode, value)) return;
                SeedPriority();
                RefreshSop();
            }
        }

        /// <summary>tbxNV_Priority - one digit, never 0.</summary>
        public string Priority
        {
            get => _priority;
            set => Set(ref _priority, Digit(value));
        }

        /// <summary>Val_tbxNV_CallCode: CCC1_PRTY_&lt;RST1_AGC_TYPE&gt;, 0 until the unit is known.</summary>
        private void SeedPriority()
        {
            if (CallCode == null) return;

            int seeded;
            switch (AgencyType)
            {
                case 'P': seeded = CallCode.PolicePriority; break;
                case 'F': seeded = CallCode.FirePriority; break;
                case 'E': seeded = CallCode.EmsPriority; break;
                case 'O': seeded = CallCode.OtherPriority; break;
                default: seeded = 0; break;
            }

            // A code that does not task the unit's agency still takes the code's own.
            if (seeded == 0 && _unit != null) seeded = CallCode.DefaultPriorityNumber;
            Priority = seeded.ToString();
        }

        // ---------------------------------------------------------- location

        public string Jurisdiction
        {
            get => _jurisdiction;
            set { if (Set(ref _jurisdiction, value) && !_applyingPlace) InvalidatePlace(); }
        }

        public string PoliceCode { get => _policeCode; private set => Set(ref _policeCode, value); }
        public string FireCode { get => _fireCode; private set => Set(ref _fireCode, value); }
        public string EmsCode { get => _emsCode; private set => Set(ref _emsCode, value); }
        public string SupportCode { get => _supportCode; private set => Set(ref _supportCode, value); }
        public string TowCode { get => _towCode; private set => Set(ref _towCode, value); }

        public string Force
        {
            get => _force;
            set { if (Set(ref _force, value) && !_applyingPlace) InvalidatePlace(); }
        }

        public string Location
        {
            get => _location;
            set
            {
                if (!Set(ref _location, value) || _applyingPlace) return;
                if (_place != null && Squash(value) != Squash(_place.Formatted)) InvalidatePlace();
            }
        }

        public string Premise { get => _premise; private set => Set(ref _premise, value); }

        public bool IsLocationValidated => _place != null;
        public bool PremiseHazard => _place != null && _place.PremiseHazard;
        public bool HasPremise => _place != null && _place.PremiseNo > 0;
        public bool CanListPremises => _place != null && (_place.MultiplePremises || _place.PremiseAvailable);
        public bool CanClearPremise => HasPremise;

        // ------------------------------------------------------- the rest

        public LookupItem Origin { get => _origin; set => Set(ref _origin, value); }

        public LookupItem Disposition
        {
            get => _disposition;
            set { if (Set(ref _disposition, value)) Raise(nameof(HasDisposition)); }
        }

        public bool HasDisposition => Disposition != null && Disposition.Code.Length > 0;

        public string Narrative { get => _narrative; set => Set(ref _narrative, value); }

        public VehicleInfo Vehicle { get; } = new VehicleInfo();

        public RacialProfile Profile { get; } = new RacialProfile();

        /// <summary>tbxNV_RpOfcr - the profiling officer's PER1 id.</summary>
        public string ProfileOfficer
        {
            get => _profileOfficer;
            set
            {
                if (Set(ref _profileOfficer, new string((value ?? string.Empty).Where(char.IsDigit).Take(6).ToArray())))
                {
                    Raise(nameof(ProfileOfficerName));
                }
            }
        }

        public string ProfileOfficerName => int.TryParse(ProfileOfficer, out var id) && id > 0 ? Support?.PersonName(id) ?? string.Empty : string.Empty;

        // --------------------------------------------------------- indicators

        public int HistoryCount { get => _historyCount; private set { if (Set(ref _historyCount, value)) Raise(nameof(HasHistory)); } }
        public bool HasHistory => HistoryCount > 0 && Settings.VisualHistory;

        /// <summary>Self Initiate's Duplicate button (CAD7_DUP_SW); Traffic Stop dropped it.</summary>
        public int DuplicateCount { get => _duplicateCount; private set { if (Set(ref _duplicateCount, value)) Raise(nameof(HasDuplicates)); } }
        public bool HasDuplicates => DuplicateCount > 0;

        public int SopCount { get => _sopCount; private set { if (Set(ref _sopCount, value)) Raise(nameof(HasSop)); } }
        public bool HasSop => SopCount > 0;

        public bool HasPatrolHit => _patrol.Any && Settings.VisualPatrol;
        public bool PatrolExact => _patrol.Exact;

        public string InfoTitle { get => _infoTitle; private set => Set(ref _infoTitle, value); }
        public bool IsInfoOpen { get => _isInfoOpen; set => Set(ref _isInfoOpen, value); }
        public ObservableCollection<InfoLine> InfoLines { get; } = new ObservableCollection<InfoLine>();

        // ----------------------------------------------------------- commands

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ValidateUnitCommand { get; }
        public RelayCommand ListPremisesCommand { get; }
        public RelayCommand ClearPremiseCommand { get; }
        public RelayCommand ShowHistoryCommand { get; }
        public RelayCommand ShowDuplicatesCommand { get; }
        public RelayCommand ShowPatrolCommand { get; }
        public RelayCommand ShowSopCommand { get; }
        public RelayCommand ShowLocationCommand { get; }
        public RelayCommand CloseInfoCommand { get; }

        // =============================================================== flow

        /// <summary>
        /// Opens the form. Traffic Stop is refused outright for a session whose DAT1
        /// row does not carry police (TraffStop GeneralInitialization).
        /// </summary>
        public bool Open(SelfInitMode mode)
        {
            if (mode == SelfInitMode.TrafficStop && CadSession.Current.AgencyPolice != 'Y')
            {
                _shell.SetStatus("Traffic Stop: User Agency not allowed.");
                return false;
            }

            Mode = mode;
            Reset();
            _openedAt = DateTime.Now;
            Raise(nameof(ReceivedText));

            // TraffStop_SetInitialize: the CCC1_TS_CODE call code.
            if (IsTrafficStop)
            {
                var code = Support?.TrafficStopCallCode() ?? string.Empty;
                CallCode = CallCodes.FirstOrDefault(c => c.Code == code);
            }

            IsOpen = true;
            _shell.SetStatus(Title + " - enter the unit.");
            return true;
        }

        /// <summary>F5 / Esc / Cancel, with the legacy question.</summary>
        public async Task<bool> CancelAsync(bool ask = true)
        {
            if (ask)
            {
                // Web port: the question is awaited, so a second Esc must not ask again.
                if (_asking || _saving) return false;
                _asking = true;
                try
                {
                    if (!await _shell.ConfirmAsync(IsTrafficStop ? "Cancel without completing process?" : "Cancel without creating record?"))
                    {
                        return false;
                    }
                }
                finally
                {
                    _asking = false;
                }

                if (!IsOpen) return false;
            }

            IsOpen = false;
            Reset();
            _shell.SetStatus(Title + " cancelled. No event was created.");
            return true;
        }

        /// <summary>A cancel question is showing.</summary>
        private bool _asking;

        private async Task SaveAsync()
        {
            if (_saving || _asking) return;

            _saving = true;
            try
            {
                await SaveCore();
            }
            catch (Exception ex)
            {
                Fail("The call was not saved. " + ex.Message);
            }
            finally
            {
                _saving = false;
            }
        }

        private async Task SaveCore()
        {
            // SelfInit_ValAll: the unit first, then the location (which may need the picker).
            if (!ValidateUnit()) return;
            if (IsTrafficStop && !ValidateProfileOfficer()) return;
            if (!ValidateLocation(saveAfter: true)) return;
            if (!Validate()) return;

            if (!await _shell.ConfirmAsync("Save record?")) return;
            if (!IsOpen) return;

            var arrive = IsTrafficStop ? Support?.Settings.TrafficArrive ?? 'E' : Support?.Settings.SelfArrive ?? 'E';
            var completed = HasDisposition;
            var arrived = !completed && (arrive == 'A' || (arrive == 'P' && await _shell.ConfirmAsync("Has Unit/Apparatus arrived?")));

            var releaseCode = ReleaseDisposition != null && short.TryParse(ReleaseDisposition.Code, out var rc) ? rc : (short)999;
            if (UnitIsBusy &&
                !await _shell.ConfirmAsync("Unit " + _unit.UnitNo + " is on " + _unit.EventText + ".\n\n" +
                                "Release it with disposition " + releaseCode + " and continue?"))
            {
                Fail("Unit/Apparatus was NOT released.");
                return;
            }

            var draft = BuildDraft(arrived, releaseCode);

            CadCall call;
            try
            {
                call = _shell.SelfInitiate(draft);
            }
            catch (Exception ex)
            {
                // One transaction: nothing was written, so the form stays open to retry.
                Fail("The call was not saved. " + ex.Message);
                return;
            }

            var code = CallCode;
            var title = Title;
            var unit = _unit.UnitNo;

            IsOpen = false;
            Reset();

            var released = _data is SqlCadDataService live && live.LastSelfInitRelease.Length > 0 ? " " + live.LastSelfInitRelease : string.Empty;
            _shell.SetStatus(completed
                ? title + " " + call.EventNo + " saved and completed (disposition " + draft.Disposition + ")." + released
                : title + " " + call.EventNo + " - unit " + unit + (arrived ? " arrived" : " en route") + " at " + call.Location + "." + released);

            // SelfInit_Save: CCC1_ASSGN_ASST offers the assist screen (LwCad130).
            if (!completed && code != null && code.OffersAssist && await _shell.ConfirmAsync("Assign Assist Unit(s)/Apparatus?"))
            {
                _shell.AfterTakeCall(call, alert: false, assign: true);
            }
        }

        private SelfInitDraft BuildDraft(bool arrived, short releaseCode)
        {
            var vehicle = Vehicle.IsEmpty || (string.IsNullOrWhiteSpace(Vehicle.Plate) && string.IsNullOrWhiteSpace(Vehicle.Make) &&
                                              string.IsNullOrWhiteSpace(Vehicle.Model) && string.IsNullOrWhiteSpace(Vehicle.Style) &&
                                              string.IsNullOrWhiteSpace(Vehicle.Color))
                ? null
                : new VehicleInfo
                {
                    Plate = Vehicle.Plate,
                    PlateState = Vehicle.PlateState,
                    PlateType = IsTrafficStop ? string.Empty : Vehicle.PlateType,
                    Year = IsTrafficStop ? string.Empty : Vehicle.Year,
                    Make = Vehicle.Make,
                    Model = Vehicle.Model,
                    Style = Vehicle.Style,
                    Color = Vehicle.Color
                };

            return new SelfInitDraft
            {
                Mode = Mode,
                UnitNo = _unit.UnitNo,
                CallCode = CallCode,
                Priority = ToShort(Priority),
                Place = _place,
                Location = Location,
                Jurisdiction = Jurisdiction,
                PoliceCode = PoliceCode,
                FireCode = FireCode,
                EmsCode = EmsCode,
                SupportCode = SupportCode,
                TowCode = TowCode,
                Force = Force,
                Origin = Origin,
                Disposition = HasDisposition && short.TryParse(Disposition.Code, out var d) ? d : (short)0,
                Narrative = Narrative,
                Vehicle = vehicle,
                Arrived = arrived,
                ReleaseDisposition = UnitIsBusy ? releaseCode : (short)0,
                Profile = IsTrafficStop
                    ? new RacialProfile
                    {
                        Race = Profile.Race,
                        Sex = Profile.Sex,
                        Sector = Profile.Sector,
                        Discretion = Profile.Discretion,
                        Resident = Profile.Resident,
                        CitationWarning = Profile.CitationWarning,
                        Search = Profile.Search,
                        Officer = ToInt(ProfileOfficer)
                    }
                    : null,
                ReceivedAt = _openedAt == default ? DateTime.Now : _openedAt
            };
        }

        // ========================================================== validation

        /// <summary>
        /// Val_tbxAV_UnitNo. On the roster: its agency, officers and any call it is
        /// on. Not on the roster: the UNT1 agency is checked, then "The UNIT entered
        /// is not active." offers Activate Unit, and the check runs again after it.
        /// </summary>
        public bool ValidateUnit()
        {
            if (_checkingUnit) return false;

            _checkingUnit = true;
            try
            {
                return ValidateUnitCore();
            }
            finally
            {
                _checkingUnit = false;
            }
        }

        private bool _checkingUnit;

        private bool _askingActivate;

        private async System.Threading.Tasks.Task AskToActivate(string unitNo)
        {
            if (_askingActivate) return;
            _askingActivate = true;
            try
            {
                if (!await _shell.ConfirmAsync("The UNIT entered is not active.\n\nWould you like to activate it now?"))
                {
                    Fail("Unit/Apparatus " + unitNo + " is not on the roster.");
                    return;
                }

                if (!IsOpen) return;

                _shell.OpenActivateUnit(unitNo, activated =>
                {
                    UnitNo = activated;
                    ValidateUnit();
                });
            }
            finally
            {
                _askingActivate = false;
            }
        }

        private bool ValidateUnitCore()
        {
            var unitNo = Clean(UnitNo);
            if (unitNo.Length == 0) return Fail(IsTrafficStop ? "Unit ID must be entered." : "Unit/Apparatus ID must be entered.");
            if (_unit != null && string.Equals(_checkedUnit, unitNo, StringComparison.OrdinalIgnoreCase)) return true;
            if (Support == null) return Fail("Unit lookups are not available.");

            var unit = Support.FindUnit(unitNo);
            if (unit == null)
            {
                // SelfInit_ValUnitType / TraffStop_ValUnitType: the unit file's agency.
                var defaults = Support.UnitDefaults(unitNo);
                if (defaults != null && defaults.AgencyType != ' ')
                {
                    var refused = AgencyRefusal(defaults.AgencyType);
                    if (refused.Length > 0) return Fail(refused);
                }

                // Web port: the question is awaited rather than blocking, so the check
                // answers "not valid yet" now - as WPF did on either answer - and the
                // answer is acted on when it comes: yes opens Activate Unit, no fails.
                ValidationMessage = string.Empty;
                _ = AskToActivate(unitNo);
                return false;
            }

            var reason = AgencyRefusal(unit.AgencyType);
            if (reason.Length > 0) return Fail(reason);

            _unit = unit;
            _checkedUnit = unitNo;
            ReleaseDisposition = unit.IsOnCall
                ? Dispositions.FirstOrDefault(d => d.Code == "999") ?? Dispositions.FirstOrDefault()
                : null;

            SeedPriority();
            RaiseUnit();
            ValidationMessage = string.Empty;
            _shell.SetStatus(Title + ": unit " + unit.UnitNo + (unit.IsOnCall ? " is on " + unit.EventText + " and will be released first." : " is available."));
            return true;
        }

        /// <summary>
        /// Traffic Stop is police only. Otherwise the session's DAT1_AGC_P/F/E/O -
        /// which the legacy only checked for a unit it had just activated.
        /// </summary>
        private string AgencyRefusal(char type)
        {
            if (IsTrafficStop) return type == 'P' ? string.Empty : "Traffic Stop is a function reserved for police.";

            var session = CadSession.Current;
            switch (type)
            {
                case 'P': return session.AgencyPolice == 'Y' ? string.Empty : "Police units not available to operator.";
                case 'F': return session.AgencyFire == 'Y' ? string.Empty : "Fire units not available to operator.";
                case 'E': return session.AgencyEms == 'Y' ? string.Empty : "EMS units not available to operator.";
                case 'O': return session.AgencyOther == 'Y' ? string.Empty : "Other units not available to operator.";
                default: return "Cannot assign keys, agency type unknown.";
            }
        }

        /// <summary>TraffStop_Val_PerID: 0 is allowed; any other id must be on PER1.</summary>
        private bool ValidateProfileOfficer()
        {
            var id = ToInt(ProfileOfficer);
            if (id == 0) return true;
            return ProfileOfficerName.Length > 0 || Fail("Profiling Officer is invalid.");
        }

        /// <summary>SelfInit_ValAll after the unit and the location, in its order, with its messages.</summary>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Jurisdiction)) Jurisdiction = Settings.DefaultJurisdiction;
            if (string.IsNullOrWhiteSpace(Jurisdiction) ||
                (JurisdictionCodes.Count > 0 && !JurisdictionCodes.Contains(Jurisdiction.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                return Fail("Jurisdiction is invalid.");
            }

            if (Origin == null) return Fail("Origin is invalid.");
            if (CallCode == null) return Fail("Call Code must be entered.");

            if (!string.IsNullOrWhiteSpace(Vehicle.PlateState) && !InList(States, Vehicle.PlateState)) return Fail("Vehicle State is invalid.");

            var vehicle = TakeCall?.VehicleError(Vehicle.Make, Vehicle.Model, Vehicle.Style, Vehicle.Color) ?? string.Empty;
            if (vehicle.Length > 0) return Fail(vehicle);

            if (IsSelfInitiate && !string.IsNullOrWhiteSpace(Vehicle.PlateType) && PlateTypes.Count > 0 && !InList(PlateTypes, Vehicle.PlateType))
            {
                return Fail("Plate Type is invalid.");
            }

            // SelfInit_ValidateMore.
            if (string.IsNullOrWhiteSpace(Location)) return Fail("Location must be entered.");
            if (ToShort(Priority) == 0) return Fail("Priority must be specified.");

            ValidationMessage = string.Empty;
            return true;
        }

        // ============================================================ location

        /// <summary>Val_tbxAV_Location - Take Call's validation, the same skip rule and picker.</summary>
        public bool ValidateLocation(bool saveAfter)
        {
            if (TakeCall == null) return !string.IsNullOrWhiteSpace(Location) || Fail("Location must be entered.");
            if (_place != null) return true;
            if (string.IsNullOrWhiteSpace(Location)) return saveAfter ? Fail("Location must be entered.") : false;

            if (string.IsNullOrWhiteSpace(Jurisdiction)) Jurisdiction = Settings.DefaultJurisdiction;

            var check = TakeCall.Locations.Validate(new LocationRequest
            {
                Text = Location,
                Jurisdiction = Jurisdiction,
                Forced = string.IsNullOrWhiteSpace(Force) ? 'N' : Force.Trim()[0]
            });

            switch (check.Status)
            {
                case LocationStatus.Resolved:
                    ApplyPlace(check.Result);
                    return true;

                case LocationStatus.Choose:
                case LocationStatus.ChooseSide:
                    _saveAfterPick = saveAfter;
                    ValidationMessage = string.Empty;
                    _shell.LocationPicker.Open(TakeCall.Locations, check,
                        result =>
                        {
                            ApplyPlace(result);
                            if (_saveAfterPick)
                            {
                                _saveAfterPick = false;
                                _saving = false;
                                _ = SaveAsync();
                            }
                        },
                        () =>
                        {
                            _saveAfterPick = false;
                            Fail("Location could not be validated");
                        });
                    return false;

                default:
                    return Fail(string.IsNullOrWhiteSpace(check.Message) ? "Location could not be validated" : check.Message);
            }
        }

        private void ApplyPlace(LocationResult place)
        {
            if (place == null) return;

            _applyingPlace = true;
            try
            {
                _place = place;
                Location = place.Formatted;
                if (!string.IsNullOrWhiteSpace(place.Jurisdiction)) Jurisdiction = place.Jurisdiction;
                Force = place.Forced == 'Y' || place.Forced == 'X' ? place.Forced.ToString() : "N";
                PoliceCode = place.Police1;
                FireCode = place.Fire1;
                EmsCode = place.Ems1;
                SupportCode = place.Other1;
                TowCode = place.Tow;
                Premise = place.PremiseName;
            }
            finally
            {
                _applyingPlace = false;
            }

            ValidationMessage = string.Empty;
            Notice = place.Notice;
            RaisePlace();
            RefreshIndicators();
        }

        private void InvalidatePlace()
        {
            if (_place == null) return;

            _place = null;
            PoliceCode = FireCode = EmsCode = SupportCode = TowCode = string.Empty;
            Premise = string.Empty;
            HistoryCount = 0;
            DuplicateCount = 0;
            _patrol = new PatrolHit();
            Notice = string.Empty;
            RaisePlace();
        }

        private void RaisePlace()
        {
            Raise(nameof(IsLocationValidated));
            Raise(nameof(HasPremise));
            Raise(nameof(PremiseHazard));
            Raise(nameof(CanListPremises));
            Raise(nameof(CanClearPremise));
            Raise(nameof(HasPatrolHit));
            Raise(nameof(PatrolExact));
        }

        private void ListPremises()
        {
            if (_place == null || TakeCall == null) return;

            var list = TakeCall.Locations.PremisesAt(_place);
            if (list == null || list.Rows.Count == 0)
            {
                _shell.SetStatus("No premises are on file at " + _place.Formatted + ".");
                return;
            }

            _shell.LocationPicker.OpenPremises(list, fop => TakeCall.Locations.ChoosePremise(_place, fop), ApplyPlace);
        }

        /// <summary>btn_ShowHidePremises: the premise off the call (DSU1_PMM_NO 0).</summary>
        private void ClearPremise()
        {
            if (_place == null) return;

            _place.PremiseNo = 0;
            _place.PremiseHazard = false;
            _place.PremiseName = _place.CommonName;
            Premise = _place.PremiseName;
            RaisePlace();
        }

        // ========================================================== indicators

        private void RefreshIndicators()
        {
            if (TakeCall == null) return;

            HistoryCount = TakeCall.History(_place).Count;
            DuplicateCount = IsSelfInitiate && Settings.DuplicateCheck ? TakeCall.Duplicates(_place).Count : 0;
            _patrol = TakeCall.Patrol(_place, string.Empty, string.Empty, string.Empty, Vehicle.Plate, Vehicle.PlateState) ?? new PatrolHit();
            RefreshSop();
            RaisePlace();
            Raise(nameof(HasHistory));
        }

        /// <summary>CCC2 for the unit's agency - Traffic Stop always reads the police SOP.</summary>
        private void RefreshSop()
        {
            SopCount = SopsFor().Count;
        }

        private IReadOnlyList<SopEntry> SopsFor()
        {
            if (TakeCall == null || CallCode == null || !int.TryParse(CallCode.Code, out var code)) return Array.Empty<SopEntry>();

            var type = IsTrafficStop ? 'P' : AgencyType;
            if (type == ' ') type = 'P';
            return TakeCall.Sops(code, type == 'P', type == 'F', type == 'E', type == 'O');
        }

        private void ShowHistory() => ShowCalls("Call history - " + (_place?.Formatted ?? Location), TakeCall?.History(_place) ?? Array.Empty<CallSummary>());

        private void ShowDuplicates() => ShowCalls("Possible duplicate calls - " + (_place?.Formatted ?? Location), TakeCall?.Duplicates(_place) ?? Array.Empty<CallSummary>());

        private void ShowCalls(string title, IReadOnlyList<CallSummary> rows)
        {
            InfoLines.Clear();
            foreach (var r in rows) InfoLines.Add(new InfoLine(r.EventNo + "   " + r.ReceivedText + "   " + r.Status, r.CallType + "  -  " + r.Location));
            if (InfoLines.Count == 0) InfoLines.Add(new InfoLine("Nothing found.", string.Empty));
            InfoTitle = title;
            IsInfoOpen = true;
        }

        private void ShowSop()
        {
            InfoLines.Clear();
            foreach (var s in SopsFor()) InfoLines.Add(new InfoLine(s.Agency + " SOP", s.Text));
            if (InfoLines.Count == 0) InfoLines.Add(new InfoLine("No SOP for this call code.", string.Empty));
            InfoTitle = "SOP - " + (CallCode?.Display ?? string.Empty);
            IsInfoOpen = true;
        }

        private void ShowPatrol()
        {
            if (_shell.LaunchLegacyProgram("LaPtlHitList", null)) return;

            InfoLines.Clear();
            InfoLines.Add(new InfoLine("Location", Level(_patrol.Location)));
            InfoLines.Add(new InfoLine("Vehicle", Level(_patrol.Vehicle)));
            InfoLines.Add(new InfoLine("Patrol file", "LaPtlHitList is not installed beside this console, so the records cannot be listed here."));
            InfoTitle = "Patrol file hits";
            IsInfoOpen = true;
        }

        private void ShowLocation()
        {
            if (_place == null) return;
            if (_place.GeoKey > 0 && _shell.LaunchLegacyProgram("LaGeo001", null)) return;

            InfoLines.Clear();
            InfoLines.Add(new InfoLine(_place.Formatted, "Jurisdiction " + _place.Jurisdiction + ", side " + _place.Side +
                                                         (_place.Forced == 'Y' ? ", forced" : string.Empty)));
            InfoLines.Add(new InfoLine("GeoCode", _place.GeoKey > 0 ? _place.GeoKey.ToString() : "not geocoded"));
            InfoLines.Add(new InfoLine("Areas", "Police " + _place.Police1 + "   Fire " + _place.Fire1 + "   EMS " + _place.Ems1 +
                                                "   Other " + _place.Other1 + "   Tow " + _place.Tow));
            InfoTitle = "Location";
            IsInfoOpen = true;
        }

        private static string Level(int level) => level == 1 ? "Exact match" : level == 2 ? "Close match" : "No hit";

        // ============================================================== helpers

        private bool Fail(string message)
        {
            ValidationMessage = message;
            _shell.SetStatus(Title + ": " + message);
            return false;
        }

        private void ClearUnit()
        {
            if (_unit == null) return;
            _unit = null;
            _checkedUnit = string.Empty;
            ReleaseDisposition = null;
            RaiseUnit();
        }

        private void RaiseUnit()
        {
            Raise(nameof(IsUnitValidated));
            Raise(nameof(UnitSummary));
            Raise(nameof(UnitIsBusy));
            Raise(nameof(BusyText));
        }

        private void Reset()
        {
            _unit = null;
            _checkedUnit = string.Empty;
            _place = null;
            _saveAfterPick = false;

            _unitNo = string.Empty;
            Raise(nameof(UnitNo));
            ReleaseDisposition = null;
            CallCode = null;
            Priority = "0";

            _applyingPlace = true;
            try
            {
                Jurisdiction = string.IsNullOrWhiteSpace(Settings.DefaultJurisdiction) ? _data.DefaultJurisdiction : Settings.DefaultJurisdiction;
                Force = "N";
                Location = string.Empty;
            }
            finally
            {
                _applyingPlace = false;
            }

            PoliceCode = FireCode = EmsCode = SupportCode = TowCode = string.Empty;
            Premise = string.Empty;

            // SelfInit_SetDefaults: origin 0008.
            Origin = Origins.FirstOrDefault(o => o.Code.TrimStart('0') == "8")
                     ?? Origins.FirstOrDefault(o => o.Description.IndexOf("Officer", StringComparison.OrdinalIgnoreCase) >= 0)
                     ?? Origins.FirstOrDefault();
            Disposition = null;
            Narrative = string.Empty;

            Vehicle.Plate = Vehicle.PlateType = string.Empty;
            Vehicle.Make = Vehicle.Model = Vehicle.Style = Vehicle.Color = string.Empty;
            Vehicle.PlateState = _data.DefaultState;
            // Self Initiate defaults the plate year to this year; Traffic Stop has no year.
            Vehicle.Year = IsSelfInitiate ? DateTime.Now.Year.ToString() : string.Empty;

            Profile.Race = Profile.Sex = Profile.Sector = Profile.Discretion = string.Empty;
            Profile.Resident = Profile.CitationWarning = Profile.Search = string.Empty;
            ProfileOfficer = string.Empty;
            Raise(nameof(Profile));
            Raise(nameof(Vehicle));

            ValidationMessage = string.Empty;
            Notice = string.Empty;
            HistoryCount = 0;
            DuplicateCount = 0;
            SopCount = 0;
            _patrol = new PatrolHit();
            IsInfoOpen = false;
            InfoLines.Clear();
            RaiseUnit();
            RaisePlace();
        }

        private static string Officer(int id, string name) => id <= 0 ? string.Empty : (id + " " + name).Trim();

        private static string AgencyName(char type) =>
            type == 'P' ? "Police" : type == 'F' ? "Fire" : type == 'E' ? "EMS" : type == 'O' ? "Other" : string.Empty;

        private static bool InList(IEnumerable<string> list, string value) =>
            list.Any(v => string.Equals(v, (value ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        private static string Digit(string value)
        {
            var digit = (value ?? string.Empty).Where(char.IsDigit).Take(1).ToArray();
            return digit.Length == 0 ? "0" : new string(digit);
        }

        private static string Clean(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        private static short ToShort(string value) => short.TryParse(value, out var n) ? n : (short)0;
        private static int ToInt(string value) => int.TryParse(value, out var n) ? n : 0;
        private static string Squash(string text) => (text ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
    }
}
