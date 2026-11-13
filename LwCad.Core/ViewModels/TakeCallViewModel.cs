using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;
using LwCadSystemUX.Services.Location;
using LwCadSystemUX.Services.TakeCall;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The Take Call panel - LwCadSystem TakeCall.cs / TakeCall.xaml.cs.
    ///
    /// Location is validated as the legacy validates it (LaGeo040 and the LaGeo041-045
    /// select screens, ported in Services/Location): on leaving the field or on
    /// save, a single match fills the location, its areas by side and its premise;
    /// anything else opens the location picker. P/ A/ B/ C/ and Force Y / X work as
    /// they did.
    ///
    /// The four priority boxes are seeded from the call code and are the
    /// operator's to change; one event is created per agency above 0. A
    /// disposition completes the call as it is entered. The indicator strip is the
    /// legacy toolbar: Location, Premise (red with hazards), History, Patrol, SOP,
    /// Duplicate and E911.
    ///
    /// Validation order and wording follow TakeCall_ValAll / TakeCall_ValidateMore.
    /// </summary>
    public class TakeCallViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private DateTime _openedAt;
        private bool _isOpen;
        private bool _isCollapsed;
        private bool _isCallInfoExpanded;
        private bool _showVehicle;

        private string _eventNo = string.Empty;
        private CallCodeItem _callCode;

        private string _priorityPolice = "0";
        private string _priorityFire = "0";
        private string _priorityEms = "0";
        private string _priorityOther = "0";

        private string _jurisdiction = string.Empty;
        private string _policeCode = string.Empty;
        private string _fireCode = string.Empty;
        private string _emsCode = string.Empty;
        private string _supportCode = string.Empty;
        private string _towCode = string.Empty;
        private string _force = "N";

        private string _location = string.Empty;
        private string _premise = string.Empty;
        private LookupItem _origin;
        private LookupItem _disposition;
        private string _otherComment = string.Empty;

        private string _firstName = string.Empty;
        private string _middleName = string.Empty;
        private string _lastName = string.Empty;
        private string _phoneNumber = string.Empty;
        private string _address = string.Empty;
        private string _city = string.Empty;
        private string _state = string.Empty;

        private string _narrative = string.Empty;
        private string _validationMessage = string.Empty;
        private string _notice = string.Empty;
        private string _elapsed = "00:00";

        // The validated location and what was validated, for the legacy skip rule.
        private LocationResult _place;
        private bool _applyingPlace;

        // Indicators.
        private int _duplicateCount;
        private int _historyCount;
        private PatrolHit _patrol = new PatrolHit();
        private int _sopCount;
        private E911Call _e911Waiting;
        private E911Call _e911Loaded;
        private bool _e911AutoLoaded;
        private long _mapKey;

        // The info list under the indicator strip.
        private string _infoTitle = string.Empty;
        private bool _isInfoOpen;

        private bool _saveAfterPick;

        public TakeCallViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            DispatchCommand = new RelayCommand(() => _ = DispatchAsync());
            CancelCommand = new RelayCommand(() => _ = CancelAsync());
            ToggleCollapseCommand = new RelayCommand(() => IsCollapsed = !IsCollapsed);
            ExpandCommand = new RelayCommand(() => IsCollapsed = false);
            ToggleVehicleCommand = new RelayCommand(() => ShowVehicle = !ShowVehicle);
            ToggleCallInfoCommand = new RelayCommand(() => IsCallInfoExpanded = !IsCallInfoExpanded);
            CopyEventNoCommand = new RelayCommand(CopyEventNo);
            MoreCommand = new RelayCommand(() =>
                _shell.SetStatus("Take Call options: F3 / Enter save, F11 loads the E911 call, F12 opens a field's list, Esc cancels."));

            ValidateLocationCommand = new RelayCommand(() => ValidateLocation(saveAfter: false));
            ListPremisesCommand = new RelayCommand(ListPremises);
            ClearPremiseCommand = new RelayCommand(ClearPremise);
            LoadE911Command = new RelayCommand(LoadE911);
            ShowDuplicatesCommand = new RelayCommand(ShowDuplicates);
            ShowHistoryCommand = new RelayCommand(ShowHistory);
            ShowPatrolCommand = new RelayCommand(ShowPatrol);
            ShowSopCommand = new RelayCommand(ShowSop);
            ShowPremiseCommand = new RelayCommand(ShowPremise);
            ShowLocationCommand = new RelayCommand(ShowLocation);
            CloseInfoCommand = new RelayCommand(() => IsInfoOpen = false);
        }

        private ITakeCallSupport Support => _data.TakeCallSupport;
        private TakeCallSettings Settings => Support?.Settings ?? new TakeCallSettings();

        // ------------------------------------------------------------ lookups

        public IReadOnlyList<CallCodeItem> CallCodes => _data.CallCodes;
        public IReadOnlyList<LookupItem> Origins => _data.Origins;
        public IReadOnlyList<LookupItem> Dispositions => Support?.Dispositions ?? Array.Empty<LookupItem>();

        /// <summary>Jurisdiction codes alone - the picker shows "LW", not the full name.</summary>
        public IReadOnlyList<string> JurisdictionCodes => _data.Jurisdictions.Select(j => j.Code).ToList();
        public IReadOnlyList<string> States => _data.States;
        public IReadOnlyList<string> PlateTypes => (Support?.PlateTypes ?? Array.Empty<LookupItem>()).Select(p => p.Code).ToList();

        /// <summary>TakeCall.cs:75-77 - blank, N, Y, X.</summary>
        public IReadOnlyList<string> ForceFlags { get; } = new[] { "N", "Y", "X" };

        public bool AllowPolicePriority => Settings.AllowPolice;
        public bool AllowFirePriority => Settings.AllowFire;
        public bool AllowEmsPriority => Settings.AllowEms;
        public bool AllowOtherPriority => Settings.AllowOther;

        // ------------------------------------------------------------- state

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (Set(ref _isCollapsed, value)) Raise(nameof(IsExpanded));
            }
        }

        public bool IsExpanded => !IsCollapsed;

        public bool IsCallInfoExpanded
        {
            get => _isCallInfoExpanded;
            set => Set(ref _isCallInfoExpanded, value);
        }

        public bool ShowVehicle
        {
            get => _showVehicle;
            set => Set(ref _showVehicle, value);
        }

        public VehicleInfo Vehicle { get; } = new VehicleInfo();

        public string Elapsed
        {
            get => _elapsed;
            private set => Set(ref _elapsed, value);
        }

        public string EventNo
        {
            get => _eventNo;
            private set
            {
                if (Set(ref _eventNo, value)) Raise(nameof(EventNoSuffix));
            }
        }

        public string EventNoSuffix => "/" + EventNo;

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (Set(ref _validationMessage, value)) Raise(nameof(HasValidationMessage));
            }
        }

        public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

        /// <summary>An information line - "Possible duplicate call(s) found.", a box's run card.</summary>
        public string Notice
        {
            get => _notice;
            private set
            {
                if (Set(ref _notice, value)) Raise(nameof(HasNotice));
            }
        }

        public bool HasNotice => !string.IsNullOrEmpty(Notice);

        /// <summary>The Location box is showing a location that has been validated.</summary>
        public bool IsLocationValidated => _place != null;

        public LocationResult Place => _place;

        // -------------------------------------------------------- input fields

        public CallCodeItem CallCode
        {
            get => _callCode;
            set
            {
                if (!Set(ref _callCode, value)) return;

                // Val_tbxNV_CallCode: a new code resets the four priorities to CCC1's.
                if (value != null)
                {
                    PriorityPolice = value.PolicePriority.ToString();
                    PriorityFire = value.FirePriority.ToString();
                    PriorityEms = value.EmsPriority.ToString();
                    PriorityOther = value.OtherPriority.ToString();
                }

                Raise(nameof(CallCodeDisplay));
                RefreshSop();
                SaveMapRecord();
            }
        }

        public string CallCodeDisplay => CallCode == null ? "Call Code" : CallCode.Display;

        public string PriorityPolice { get => _priorityPolice; set { if (Set(ref _priorityPolice, Digit(value))) PrioritiesChanged(); } }
        public string PriorityFire { get => _priorityFire; set { if (Set(ref _priorityFire, Digit(value))) PrioritiesChanged(); } }
        public string PriorityEms { get => _priorityEms; set { if (Set(ref _priorityEms, Digit(value))) PrioritiesChanged(); } }
        public string PriorityOther { get => _priorityOther; set { if (Set(ref _priorityOther, Digit(value))) PrioritiesChanged(); } }

        private void PrioritiesChanged()
        {
            Raise(nameof(Priority));
            Raise(nameof(IsHigh));
            Raise(nameof(IsMedium));
            Raise(nameof(IsLow));
            Raise(nameof(PriorityText));
            RefreshSop();
        }

        /// <summary>The band of the lowest priority entered - the chip on the header and the rail.</summary>
        public CallPriority Priority
        {
            get
            {
                var lowest = new[] { PriorityPolice, PriorityFire, PriorityEms, PriorityOther }
                    .Select(p => short.TryParse(p, out var n) ? n : (short)0)
                    .Where(n => n > 0)
                    .DefaultIfEmpty((short)0)
                    .Min();

                if (lowest == 0) return CallPriority.Low;
                if (lowest <= 3) return CallPriority.High;
                return lowest <= 6 ? CallPriority.Medium : CallPriority.Low;
            }
        }

        public bool IsHigh => Priority == CallPriority.High;
        public bool IsMedium => Priority == CallPriority.Medium;
        public bool IsLow => Priority == CallPriority.Low;
        public string PriorityText => Priority.ToString();

        public string Jurisdiction
        {
            get => _jurisdiction;
            set
            {
                if (Set(ref _jurisdiction, value) && !_applyingPlace) InvalidatePlace();
            }
        }

        public string PoliceCode { get => _policeCode; set => Set(ref _policeCode, value); }
        public string FireCode { get => _fireCode; set => Set(ref _fireCode, value); }
        public string EmsCode { get => _emsCode; set => Set(ref _emsCode, value); }
        public string SupportCode { get => _supportCode; set => Set(ref _supportCode, value); }
        public string TowCode { get => _towCode; set => Set(ref _towCode, value); }

        public string Force
        {
            get => _force;
            set
            {
                if (Set(ref _force, value) && !_applyingPlace) InvalidatePlace();
            }
        }

        public string Location
        {
            get => _location;
            set
            {
                if (!Set(ref _location, value) || _applyingPlace) return;

                // Typing over a validated location needs validating again - unless it
                // only differs in spacing, which the legacy skip rule ignores.
                if (_place != null && Squash(value) != Squash(_place.Formatted)) InvalidatePlace();
            }
        }

        public string Premise { get => _premise; set => Set(ref _premise, value); }

        public bool PremiseHazard => _place != null && _place.PremiseHazard;

        /// <summary>"Multiple Premises", or a single premise PREM_SW did not attach - the list button.</summary>
        public bool CanListPremises => _place != null && (_place.MultiplePremises || _place.PremiseAvailable);

        public bool CanClearPremise => _place != null && _place.PremiseNo > 0;

        public LookupItem Origin
        {
            get => _origin;
            set => Set(ref _origin, value);
        }

        public LookupItem Disposition
        {
            get => _disposition;
            set
            {
                if (Set(ref _disposition, value)) Raise(nameof(HasDisposition));
            }
        }

        public bool HasDisposition => Disposition != null && Disposition.Code.Length > 0;

        public string OtherComment { get => _otherComment; set => Set(ref _otherComment, value); }

        public string FirstName { get => _firstName; set { if (Set(ref _firstName, value)) PatrolDirty(); } }
        public string MiddleName { get => _middleName; set { if (Set(ref _middleName, value)) PatrolDirty(); } }
        public string LastName { get => _lastName; set { if (Set(ref _lastName, value)) PatrolDirty(); } }
        public string PhoneNumber { get => _phoneNumber; set => Set(ref _phoneNumber, value); }
        public string Address { get => _address; set => Set(ref _address, value); }
        public string City { get => _city; set => Set(ref _city, value); }
        public string State { get => _state; set => Set(ref _state, value); }
        public string Narrative { get => _narrative; set => Set(ref _narrative, value); }

        // ---------------------------------------------------------- indicators

        public bool IsGeocoded => _place != null && _place.GeoKey > 0;
        public bool HasPremise => _place != null && _place.PremiseNo > 0;

        public int DuplicateCount { get => _duplicateCount; private set { if (Set(ref _duplicateCount, value)) Raise(nameof(HasDuplicates)); } }
        public bool HasDuplicates => DuplicateCount > 0;

        public int HistoryCount { get => _historyCount; private set { if (Set(ref _historyCount, value)) Raise(nameof(HasHistory)); } }

        /// <summary>History shows when CAD7_VISUAL_HIS is on, as the legacy toolbar does.</summary>
        public bool HasHistory => HistoryCount > 0 && Settings.VisualHistory;

        public bool HasPatrolHit => _patrol.Any && Settings.VisualPatrol;
        public bool PatrolExact => _patrol.Exact;

        public int SopCount { get => _sopCount; private set { if (Set(ref _sopCount, value)) Raise(nameof(HasSop)); } }
        public bool HasSop => SopCount > 0;

        /// <summary>An E911 call waits at this position (CAD7_VISUAL_911) - the red E911 button.</summary>
        public bool HasE911Waiting => _e911Waiting != null && _e911Loaded == null && Settings.Visual911;

        public bool IsE911Loaded => _e911Loaded != null;

        public string E911Text => _e911Waiting == null ? string.Empty : "E911 " + _e911Waiting.Phone;

        // ---------------------------------------------------------- info list

        public string InfoTitle { get => _infoTitle; private set => Set(ref _infoTitle, value); }
        public bool IsInfoOpen { get => _isInfoOpen; set => Set(ref _isInfoOpen, value); }
        public ObservableCollection<InfoLine> InfoLines { get; } = new ObservableCollection<InfoLine>();

        // ---------------------------------------------------------- commands

        public RelayCommand DispatchCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ToggleCollapseCommand { get; }
        public RelayCommand ExpandCommand { get; }
        public RelayCommand ToggleVehicleCommand { get; }
        public RelayCommand ToggleCallInfoCommand { get; }
        public RelayCommand CopyEventNoCommand { get; }
        public RelayCommand MoreCommand { get; }
        public RelayCommand ValidateLocationCommand { get; }
        public RelayCommand ListPremisesCommand { get; }
        public RelayCommand ClearPremiseCommand { get; }
        public RelayCommand LoadE911Command { get; }
        public RelayCommand ShowDuplicatesCommand { get; }
        public RelayCommand ShowHistoryCommand { get; }
        public RelayCommand ShowPatrolCommand { get; }
        public RelayCommand ShowSopCommand { get; }
        public RelayCommand ShowPremiseCommand { get; }
        public RelayCommand ShowLocationCommand { get; }
        public RelayCommand CloseInfoCommand { get; }

        // ------------------------------------------------------------- flow

        /// <summary>Opens a fresh Take Call - and, with CAD7_E911_XFER 'A', loads the waiting 911 call.</summary>
        public void Open()
        {
            Reset();
            EventNo = _data.ReserveEventNo();
            _openedAt = DateTime.Now;
            Elapsed = "00:00";
            IsCollapsed = false;
            IsOpen = true;

            _e911Waiting = Support?.PendingE911(CadSession.Current.E911Pos > 0 ? CadSession.Current.E911Pos : (short)_data.E911Position);
            RaiseE911();

            if (_e911Waiting != null && Settings.E911Transfer == 'A')
            {
                LoadE911();
                _e911AutoLoaded = true;
            }

            _shell.SetStatus("Take Call " + EventNo + " started." +
                             (HasE911Waiting ? " An E911 call is waiting - F11 loads it." : string.Empty));
        }

        /// <summary>
        /// Esc / F5 / Cancel. "Cancel without creating record?", and first - for a
        /// 911 call that loaded itself - "Are you sure you want to DISCARD this Call?".
        /// </summary>
        public async Task<bool> CancelAsync(bool ask = true)
        {
            if (ask)
            {
                // Web port: the questions are awaited, so a second Esc while one is
                // showing must not ask again.
                if (_asking || _saving) return false;
                _asking = true;
                try
                {
                    if (_e911Loaded != null && _e911AutoLoaded &&
                        !await _shell.ConfirmAsync("An E911 call has been loaded.\n\nAre you sure you want to DISCARD this Call?"))
                    {
                        return false;
                    }

                    if (!await _shell.ConfirmAsync("Cancel without creating record?")) return false;
                }
                finally
                {
                    _asking = false;
                }

                if (!IsOpen) return false;
            }

            var number = EventNo;
            CloseCleanUp(saved: false);
            IsOpen = false;
            IsCollapsed = false;
            Reset();
            _shell.SetStatus("Take Call " + number + " cancelled. No event was created.");
            return true;
        }

        private bool _saving;

        /// <summary>A cancel question is showing.</summary>
        private bool _asking;

        /// <summary>
        /// One save at a time: a key reaching the form while "Save record?" or an
        /// after-save question is showing must not start a second save.
        /// </summary>
        private async Task DispatchAsync()
        {
            if (_saving || _asking) return;

            _saving = true;
            try
            {
                await DispatchCore();
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

        private async Task DispatchCore()
        {
            // TakeCall_ValAll starts with the location; a lookup that needs the
            // operator suspends the save until the location has been picked.
            if (!ValidateLocation(saveAfter: true)) return;
            if (!Validate()) return;

            var draft = BuildDraft();

            if (!await _shell.ConfirmAsync("Save record?")) return;
            if (!IsOpen) return;

            CadCall call;
            try
            {
                call = _shell.DispatchCall(draft);
            }
            catch (Exception ex)
            {
                // Nothing was written - the dispatch is one transaction - so the call
                // stays open on the reason and can be saved again.
                Fail("The call was not saved. " + ex.Message);
                return;
            }

            _mapKey = 0;
            CloseCleanUp(saved: true);

            var code = CallCode;
            var completed = draft.Disposition > 0;

            IsOpen = false;
            IsCollapsed = false;
            Reset();

            var events = _data is SqlCadDataService live && live.LastDispatchedEvents.Count > 1
                ? " Events created: " + string.Join(", ", live.LastDispatchedEvents) + "."
                : string.Empty;

            _shell.SetStatus(completed
                ? "Event " + call.EventNo + " saved and completed (disposition " + draft.Disposition + ")." + events
                : "Event " + call.EventNo + " dispatched to the stack as " + call.CallType + " at " + call.Location + "." + events);

            if (completed || call == null) return;

            // TakeCall_SaveDSE1: CCC1_ALERT asks for an alert; an S / D / U operator is
            // offered the assignment straight away.
            var alert = code != null && code.PromptsAlert && await _shell.ConfirmAsync("Enter Call ALERT?");
            var type = char.ToUpperInvariant(CadSession.Current.OperatorType);
            var assign = (type == 'S' || type == 'D' || type == 'U') && !call.HasAssignedUnits &&
                         await _shell.ConfirmAsync("Assign Unit(s)/Apparatus Immediately?");

            _shell.AfterTakeCall(call, alert, assign);
        }

        private NewCallDraft BuildDraft() => new NewCallDraft
        {
            EventNo = EventNo,
            CallCode = CallCode,
            Priority = Priority,
            PriorityPolice = ToShort(PriorityPolice),
            PriorityFire = ToShort(PriorityFire),
            PriorityEms = ToShort(PriorityEms),
            PriorityOther = ToShort(PriorityOther),
            Jurisdiction = Jurisdiction,
            PoliceCode = PoliceCode,
            FireCode = FireCode,
            EmsCode = EmsCode,
            SupportCode = SupportCode,
            TowCode = TowCode,
            Force = Force,
            Location = Location,
            Place = ApplyTypedAreas(_place),
            Premise = Premise,
            Origin = Origin,
            Disposition = HasDisposition && short.TryParse(Disposition.Code, out var d) ? d : (short)0,
            OtherComment = OtherComment,
            FirstName = FirstName,
            MiddleName = MiddleName,
            LastName = LastName,
            PhoneNumber = WithAreaCode(PhoneNumber),
            Address = Address,
            City = City,
            State = State,
            Narrative = Narrative,
            Vehicle = ShowVehicle && !Vehicle.IsEmpty ? CloneVehicle() : null,
            E911 = _e911Loaded,
            MapRecordKey = _mapKey,
            ReceivedAt = _openedAt == default ? DateTime.Now : _openedAt
        };

        /// <summary>The area boxes are the operator's to correct; what they show is what is saved.</summary>
        private LocationResult ApplyTypedAreas(LocationResult place)
        {
            if (place == null) return null;

            place.Police1 = (PoliceCode ?? string.Empty).Trim();
            place.Fire1 = (FireCode ?? string.Empty).Trim();
            place.Ems1 = (EmsCode ?? string.Empty).Trim();
            place.Other1 = (SupportCode ?? string.Empty).Trim();
            place.Tow = (TowCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(Jurisdiction)) place.Jurisdiction = Jurisdiction.Trim();
            return place;
        }

        /// <summary>
        /// TakeCall_ValAll's field checks after the location, in its order, with its
        /// messages. The rules the first build added (a phone format, "first name
        /// required") are gone - the legacy has neither.
        /// </summary>
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

            ApplySame();

            if (!string.IsNullOrWhiteSpace(State) && !InList(States, State)) return Fail("Reporting Person State is invalid.");

            if (ShowVehicle && !Vehicle.IsEmpty)
            {
                if (!string.IsNullOrWhiteSpace(Vehicle.PlateState) && !InList(States, Vehicle.PlateState)) return Fail("Vehicle State is invalid.");

                var vehicle = Support?.VehicleError(Vehicle.Make, Vehicle.Model, Vehicle.Style, Vehicle.Color) ?? string.Empty;
                if (vehicle.Length > 0) return Fail(vehicle);

                if (!string.IsNullOrWhiteSpace(Vehicle.PlateType) && PlateTypes.Count > 0 && !InList(PlateTypes, Vehicle.PlateType))
                {
                    return Fail("Plate Type is invalid.");
                }
            }

            // TakeCall_ValidateMore.
            if (string.IsNullOrWhiteSpace(Location)) return Fail("Location must be specified.");
            if (ToShort(PriorityPolice) == 0 && ToShort(PriorityFire) == 0 && ToShort(PriorityEms) == 0 && ToShort(PriorityOther) == 0)
            {
                return Fail("Priority must be specified.");
            }

            ValidationMessage = string.Empty;
            return true;
        }

        /// <summary>Val_tbxAV_RpAddr: "SAME" copies the location, the default state and the jurisdiction's city.</summary>
        public void ApplySame()
        {
            if (!string.Equals((Address ?? string.Empty).Trim(), "SAME", StringComparison.OrdinalIgnoreCase)) return;

            Address = (Location ?? string.Empty).Trim();
            State = Settings.DefaultState;
            var city = Support?.CityFor(Jurisdiction) ?? string.Empty;
            if (city.Length > 0) City = city;
        }

        // ================================================================ location

        /// <summary>
        /// Val_tbxAV_Location: validate the Location box - skipped when it, the
        /// Force flag and the jurisdiction are unchanged since the last pass.
        /// False when the location is invalid or the picker is open.
        /// </summary>
        public bool ValidateLocation(bool saveAfter)
        {
            if (Support == null) return !string.IsNullOrWhiteSpace(Location) || Fail("Location must be specified.");
            if (_place != null) return true;

            if (string.IsNullOrWhiteSpace(Jurisdiction)) Jurisdiction = Settings.DefaultJurisdiction;

            var check = Support.Locations.Validate(new LocationRequest
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
                    _shell.LocationPicker.Open(Support.Locations, check,
                        result =>
                        {
                            ApplyPlace(result);
                            if (_saveAfterPick)
                            {
                                _saveAfterPick = false;
                                _saving = false;
                                _ = DispatchAsync();
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

        /// <summary>
        /// ValidateLocationPassed: the location text, jurisdiction, force, areas and
        /// premise from the answer; an alarm's call code and company; then the
        /// indicator checks and the map record.
        /// </summary>
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

                if (place.CallCode > 0)
                {
                    var code = CallCodes.FirstOrDefault(c => c.Code == place.CallCode.ToString());
                    if (code != null) CallCode = code;
                }

                if (place.Contact != null)
                {
                    // UpdateNameInfo: the alarm company is the reporting person.
                    LastName = place.Contact.Name;
                    FirstName = string.Empty;
                    MiddleName = string.Empty;
                    Address = place.Contact.Address;
                    City = place.Contact.City;
                    State = place.Contact.State;
                    PhoneNumber = place.Contact.Phone;
                }
            }
            finally
            {
                _applyingPlace = false;
            }

            ValidationMessage = string.Empty;
            Notice = place.Notice;
            RaisePlace();
            RefreshIndicators();
            SaveMapRecord();
        }

        private void InvalidatePlace()
        {
            if (_place == null) return;

            _place = null;
            DuplicateCount = 0;
            HistoryCount = 0;
            _patrol = new PatrolHit();
            Notice = string.Empty;
            RaisePlace();
        }

        private void RaisePlace()
        {
            Raise(nameof(IsLocationValidated));
            Raise(nameof(Place));
            Raise(nameof(IsGeocoded));
            Raise(nameof(HasPremise));
            Raise(nameof(PremiseHazard));
            Raise(nameof(CanListPremises));
            Raise(nameof(CanClearPremise));
            Raise(nameof(HasPatrolHit));
            Raise(nameof(PatrolExact));
        }

        /// <summary>TakeCall_ListPremises - the premises at the address; the one picked is attached.</summary>
        private void ListPremises()
        {
            if (_place == null || Support == null) return;

            var list = Support.Locations.PremisesAt(_place);
            if (list == null || list.Rows.Count == 0)
            {
                _shell.SetStatus("No premises are on file at " + _place.Formatted + ".");
                return;
            }

            _shell.LocationPicker.OpenPremises(list, fop => Support.Locations.ChoosePremise(_place, fop), ApplyPlace);
        }

        private void ClearPremise()
        {
            if (_place == null) return;

            _place.PremiseNo = 0;
            _place.PremiseHazard = false;
            _place.PremiseName = _place.CommonName;
            Premise = _place.PremiseName;
            RaisePlace();
        }

        // ============================================================ indicators

        private void RefreshIndicators()
        {
            if (Support == null) return;

            var duplicates = Settings.DuplicateCheck ? Support.Duplicates(_place) : Array.Empty<CallSummary>();
            DuplicateCount = duplicates.Count;
            if (DuplicateCount > 0) Notice = "Possible duplicate call(s) found.";

            HistoryCount = Support.History(_place).Count;
            RefreshPatrol();
            RefreshSop();

            Raise(nameof(HasHistory));
        }

        private void PatrolDirty()
        {
            if (!_applyingPlace && _place != null) RefreshPatrol();
        }

        private void RefreshPatrol()
        {
            _patrol = Support?.Patrol(_place, LastName, FirstName, MiddleName, Vehicle.Plate, Vehicle.PlateState) ?? new PatrolHit();
            Raise(nameof(HasPatrolHit));
            Raise(nameof(PatrolExact));
        }

        private void RefreshSop()
        {
            if (Support == null || CallCode == null || !int.TryParse(CallCode.Code, out var code))
            {
                SopCount = 0;
                return;
            }

            SopCount = Support.Sops(code, ToShort(PriorityPolice) > 0, ToShort(PriorityFire) > 0,
                                    ToShort(PriorityEms) > 0, ToShort(PriorityOther) > 0).Count;
        }

        private void ShowDuplicates()
        {
            var rows = Support?.Duplicates(_place) ?? Array.Empty<CallSummary>();
            ShowCalls("Possible duplicate calls - " + (_place?.Formatted ?? Location), rows);
        }

        private void ShowHistory()
        {
            var rows = Support?.History(_place) ?? Array.Empty<CallSummary>();
            ShowCalls("Call history - " + (_place?.Formatted ?? Location), rows);
        }

        private void ShowCalls(string title, IReadOnlyList<CallSummary> rows)
        {
            InfoLines.Clear();
            foreach (var r in rows)
            {
                InfoLines.Add(new InfoLine(r.EventNo + "   " + r.ReceivedText + "   " + r.Status, r.CallType + "  -  " + r.Location));
            }

            if (InfoLines.Count == 0) InfoLines.Add(new InfoLine("Nothing found.", string.Empty));
            InfoTitle = title;
            IsInfoOpen = true;
        }

        private void ShowSop()
        {
            InfoLines.Clear();
            if (Support != null && CallCode != null && int.TryParse(CallCode.Code, out var code))
            {
                foreach (var s in Support.Sops(code, ToShort(PriorityPolice) > 0, ToShort(PriorityFire) > 0,
                                               ToShort(PriorityEms) > 0, ToShort(PriorityOther) > 0))
                {
                    InfoLines.Add(new InfoLine(s.Agency + " SOP", s.Text));
                }
            }

            if (InfoLines.Count == 0) InfoLines.Add(new InfoLine("No SOP for this call code.", string.Empty));
            InfoTitle = "SOP - " + CallCodeDisplay;
            IsInfoOpen = true;
        }

        /// <summary>Patrol: LaPtlHitList when installed, else what the hit was on.</summary>
        private void ShowPatrol()
        {
            if (_shell.LaunchLegacyProgram("LaPtlHitList", null)) return;

            InfoLines.Clear();
            InfoLines.Add(new InfoLine("Location", Level(_patrol.Location)));
            InfoLines.Add(new InfoLine("Name", Level(_patrol.Name)));
            InfoLines.Add(new InfoLine("Vehicle", Level(_patrol.Vehicle)));
            InfoLines.Add(new InfoLine("Patrol file", "LaPtlHitList is not installed beside this console, so the records cannot be listed here."));
            InfoTitle = "Patrol file hits";
            IsInfoOpen = true;
        }

        private void ShowPremise()
        {
            if (_place == null || _place.PremiseNo == 0) return;
            if (_shell.LaunchLegacyProgram("PMM001", null)) return;

            InfoLines.Clear();
            InfoLines.Add(new InfoLine(_place.PremiseName, "Premise " + _place.PremiseNo + (_place.PremiseHazard ? " - HAZARDS ON FILE (PMM2)" : string.Empty)));
            InfoLines.Add(new InfoLine("Premise viewer", "LwPmm001 is not installed beside this console."));
            InfoTitle = "Premise";
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
            InfoLines.Add(new InfoLine("Areas", "Police " + _place.Police1 + " " + _place.Police2 + "   Fire " + _place.Fire1 + " " + _place.Fire2 +
                                                "   EMS " + _place.Ems1 + " " + _place.Ems2 + "   Other " + _place.Other1 + "   Tow " + _place.Tow));
            if (_place.RoutingId.Length > 0) InfoLines.Add(new InfoLine("Routing id", _place.RoutingId));
            InfoTitle = "Location";
            IsInfoOpen = true;
        }

        private static string Level(int level) => level == 1 ? "Exact match" : level == 2 ? "Close match" : "No hit";

        // ================================================================= E911

        /// <summary>
        /// TakeCall_LoadE911Call: the caller's name and phone, the address, the
        /// jurisdiction (JUR3 by ESN, or the city alias), origin 11 - or 8 with the
        /// position in the remarks when the handset reported one.
        /// </summary>
        private void LoadE911()
        {
            var call = _e911Waiting ?? Support?.PendingE911(CadSession.Current.E911Pos > 0 ? CadSession.Current.E911Pos : (short)_data.E911Position);
            if (call == null)
            {
                _shell.SetStatus("No E911 call is waiting at this position.");
                return;
            }

            _e911Loaded = call;
            InvalidatePlace();

            LastName = call.LastName;
            FirstName = call.FirstName;
            MiddleName = call.MiddleName;
            PhoneNumber = call.Phone;
            City = call.City;
            if (call.State.Length > 0) State = call.State;
            Force = "N";
            if (call.Jurisdiction.Length > 0) Jurisdiction = call.Jurisdiction;
            Location = call.LocationText;
            Address = call.LocationText;

            var hasPosition = call.XLow != 0 && call.YLow != 0;
            Origin = Origins.FirstOrDefault(o => o.Code == (hasPosition ? "8" : "11")) ?? Origin;
            if (hasPosition)
            {
                Narrative = "Latitude: +" + call.YLow + "  Longitude: " + call.XLow +
                            (string.IsNullOrWhiteSpace(Narrative) ? string.Empty : Environment.NewLine + Narrative);
            }

            RaiseE911();
            _shell.SetStatus("E911 call from " + call.Phone + " loaded.");
        }

        private void RaiseE911()
        {
            Raise(nameof(HasE911Waiting));
            Raise(nameof(IsE911Loaded));
            Raise(nameof(E911Text));
        }

        /// <summary>
        /// CloseCleanUp: the caller's EMG2 rows go (every row for the position and
        /// phone - cell phones resend), and an unsaved map record is removed.
        /// </summary>
        private void CloseCleanUp(bool saved)
        {
            if (_e911Loaded != null) Support?.ClearE911(_e911Loaded);
            if (!saved && _mapKey > 0) Support?.DeleteMapRecord(_mapKey);
            _mapKey = 0;
        }

        /// <summary>
        /// The user's browser session ended (tab closed, or the connection dropped and was
        /// not resumed) with the call still open. Does what closing the WPF window did:
        /// the unsaved STK2 map record and the caller's EMG2 rows are removed, so the maps
        /// do not keep showing a call nobody is taking. No question is asked - there is no
        /// one left to answer it - and nothing is saved.
        /// </summary>
        public void AbandonOnDisconnect()
        {
            if (!IsOpen && _mapKey == 0 && _e911Loaded == null) return;

            try
            {
                CloseCleanUp(saved: false);
            }
            catch (Exception ex)
            {
                LwCadSystemUX.Services.Data.LaLog.Error("TakeCall", "Clean-up after a closed session failed.", ex);
            }

            IsOpen = false;
        }
        /// <summary>TakeCall_ShowOnMap / CreateSTK2: the call being taken, for the maps.</summary>
        private void SaveMapRecord()
        {
            if (Support == null || _place == null || !IsOpen) return;

            var digits = new string((PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            short.TryParse(digits.Length == 10 ? digits.Substring(0, 3) : string.Empty, out var area);
            var local = digits.Length >= 7 ? digits.Substring(digits.Length - 7) : string.Empty;
            short.TryParse(local.Length == 7 ? local.Substring(0, 3) : string.Empty, out var p1);
            int.TryParse(local.Length == 7 ? local.Substring(3) : string.Empty, out var p2);

            var type = LowestAgency();
            _mapKey = Support.SaveMapRecord(new MapRecord
            {
                Position = CadSession.Current.E911Pos,
                Received = _openedAt == default ? DateTime.Now : _openedAt,
                ReceivedBy = CadSession.Current.EmpId,
                Priority = type.Priority,
                CallCode = CallCode != null && int.TryParse(CallCode.Code, out var cc) ? cc : 0,
                Area = type.Agency == 'F' ? _place.Fire1 : type.Agency == 'E' ? _place.Ems1 : type.Agency == 'O' ? _place.Other1 : _place.Police1,
                Place = _place,
                LastName = LastName,
                FirstName = FirstName,
                MiddleName = MiddleName,
                PhoneArea = area,
                Phone1 = p1,
                Phone2 = p2,
                PcName = CadSession.Current.PcName
            }, _mapKey);
        }

        private (char Agency, short Priority) LowestAgency()
        {
            var list = new[] { ('P', ToShort(PriorityPolice)), ('F', ToShort(PriorityFire)), ('E', ToShort(PriorityEms)), ('O', ToShort(PriorityOther)) }
                .Where(p => p.Item2 > 0).OrderBy(p => p.Item2).ToList();
            return list.Count == 0 ? ('P', (short)0) : list[0];
        }

        // ================================================================ helpers

        private VehicleInfo CloneVehicle() => new VehicleInfo
        {
            Plate = Vehicle.Plate,
            PlateState = Vehicle.PlateState,
            PlateType = Vehicle.PlateType,
            Year = Vehicle.Year,
            Make = Vehicle.Make,
            Model = Vehicle.Model,
            Style = Vehicle.Style,
            Color = Vehicle.Color
        };

        private bool Fail(string message)
        {
            ValidationMessage = message;
            IsCollapsed = false;
            _shell.SetStatus("Take Call: " + message);
            return false;
        }

        private void CopyEventNo()
        {
            try
            {
                _shell.CopyToClipboard?.Invoke(EventNo);
                _shell.SetStatus("Event number " + EventNo + " copied to the clipboard.");
            }
            catch (Exception)
            {
                _shell.SetStatus("Event number could not be copied.");
            }
        }

        /// <summary>A seven-digit number takes the default area code, as tbxN_RpPhoneAc defaulted to 978.</summary>
        private string WithAreaCode(string phone)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 7 && Settings.DefaultAreaCode.Length == 3 ? Settings.DefaultAreaCode + digits : phone;
        }

        private static bool InList(IEnumerable<string> list, string value) =>
            list.Any(v => string.Equals(v, (value ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        private static string Digit(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length == 1 && char.IsDigit(text[0]) ? text : text.Length == 0 ? "0" : new string(text.Where(char.IsDigit).Take(1).DefaultIfEmpty('0').ToArray());
        }

        private static short ToShort(string value) => short.TryParse(value, out var n) ? n : (short)0;

        private static string Squash(string text) => (text ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

        private void Reset()
        {
            _place = null;
            _e911Loaded = null;
            _e911Waiting = null;
            _e911AutoLoaded = false;
            _mapKey = 0;
            _saveAfterPick = false;

            CallCode = null;
            PriorityPolice = PriorityFire = PriorityEms = PriorityOther = "0";
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
            Origin = Origins.FirstOrDefault(o => o.Code == "20");
            Disposition = null;
            OtherComment = string.Empty;
            FirstName = MiddleName = LastName = string.Empty;
            PhoneNumber = string.Empty;
            Address = string.Empty;
            City = _data.DefaultCity;
            State = _data.DefaultState;
            Narrative = string.Empty;
            ValidationMessage = string.Empty;
            Notice = string.Empty;
            ShowVehicle = false;
            IsCallInfoExpanded = false;
            IsInfoOpen = false;
            InfoLines.Clear();
            Vehicle.Plate = Vehicle.PlateType = Vehicle.Year = string.Empty;
            Vehicle.Make = Vehicle.Model = Vehicle.Style = Vehicle.Color = string.Empty;
            Vehicle.PlateState = _data.DefaultState;
            DuplicateCount = 0;
            HistoryCount = 0;
            SopCount = 0;
            _patrol = new PatrolHit();
            RaisePlace();
            RaiseE911();
            Raise(nameof(CallCodeDisplay));
        }

        internal void Tick()
        {
            if (!IsOpen) return;
            var span = DateTime.Now - _openedAt;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            Elapsed = span.TotalHours >= 1
                ? string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds)
                : string.Format("{0:00}:{1:00}", span.Minutes, span.Seconds);
        }
    }

    /// <summary>One line of the Take Call info list - a duplicate, a history call, an SOP.</summary>
    public sealed class InfoLine
    {
        public InfoLine(string heading, string text)
        {
            Heading = heading ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string Heading { get; }
        public string Text { get; }
        public bool HasText => Text.Length > 0;
    }
}
