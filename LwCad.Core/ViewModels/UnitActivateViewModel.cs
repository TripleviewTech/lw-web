using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;
using LwCadSystemUX.Services.SelfInit;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// Activate Unit - LwCad129 "CAD - Activate Unit/Apparatus". Puts a unit that
    /// is not logged on onto the roster: agency, type, jurisdiction, crew, report
    /// areas (police) or station (fire / EMS), pre-filled from the unit file
    /// (UNT1). Opened by Self Initiate / Traffic Stop for a unit that is not
    /// active, or from the menu with the unit to type.
    ///
    /// The legacy screen reported success when it was closed with the X or
    /// Cancel, so the caller carried on with a unit that was never activated; here
    /// only a saved activation calls back. Its agency and report-area checks
    /// always passed; the agency is checked here.
    /// </summary>
    public class UnitActivateViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private bool _isOpen;
        private bool _unitEditable;
        private string _unitNo = string.Empty;
        private AgencyItem _agency;
        private LookupItem _unitType;
        private LookupItem _jurisdiction;
        private LookupItem _station;
        private string _employee1 = string.Empty;
        private string _employee2 = string.Empty;
        private string _area1 = string.Empty, _area2 = string.Empty, _area3 = string.Empty, _area4 = string.Empty, _area5 = string.Empty;
        private string _message = string.Empty;
        private string _prefilledFor = string.Empty;

        private Action<string> _activated;

        public UnitActivateViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private ISelfInitSupport Support => _data.SelfInitSupport;

        public IReadOnlyList<AgencyItem> Agencies => Support?.Agencies ?? Array.Empty<AgencyItem>();

        /// <summary>"N/A" and MLB1 05/04.</summary>
        public IReadOnlyList<LookupItem> UnitTypes =>
            _unitTypes ??= new[] { new LookupItem("0", "N/A") }.Concat(Support?.UnitTypes ?? Array.Empty<LookupItem>()).ToList();

        private IReadOnlyList<LookupItem> _unitTypes;
        private IReadOnlyList<LookupItem> _stations;

        public IReadOnlyList<LookupItem> Jurisdictions => Support?.UnitJurisdictions ?? Array.Empty<LookupItem>();

        /// <summary>"N/A" and MLB1 05/10.</summary>
        public IReadOnlyList<LookupItem> Stations =>
            _stations ??= new[] { new LookupItem("0", "N/A") }.Concat(Support?.Stations ?? Array.Empty<LookupItem>()).ToList();

        public bool IsOpen { get => _isOpen; private set => Set(ref _isOpen, value); }

        /// <summary>From the menu the unit is typed; from Self Initiate it is the one entered there.</summary>
        public bool IsUnitEditable { get => _unitEditable; private set => Set(ref _unitEditable, value); }

        public string Title => "Activate Unit/Apparatus";

        public string UnitNo
        {
            get => _unitNo;
            set => Set(ref _unitNo, (value ?? string.Empty).Trim().ToUpperInvariant());
        }

        public AgencyItem Agency
        {
            get => _agency;
            set
            {
                if (!Set(ref _agency, value)) return;
                Raise(nameof(AgencyTypeName));
                Raise(nameof(IsPolice));
                Raise(nameof(HasStation));
                Raise(nameof(HasCrew));
            }
        }

        public string AgencyTypeName => Agency?.TypeName ?? string.Empty;

        /// <summary>SetFields: only police have report areas.</summary>
        public bool IsPolice => Agency != null && Agency.Type == 'P';

        /// <summary>SetFields: fire and EMS have a station.</summary>
        public bool HasStation => Agency != null && (Agency.Type == 'F' || Agency.Type == 'E');

        /// <summary>SetFields: fire apparatus carry no employee ids ("000000").</summary>
        public bool HasCrew => Agency == null || Agency.Type != 'F';

        public LookupItem UnitType { get => _unitType; set => Set(ref _unitType, value); }
        public LookupItem Jurisdiction { get => _jurisdiction; set => Set(ref _jurisdiction, value); }
        public LookupItem Station { get => _station; set => Set(ref _station, value); }

        public string Employee1
        {
            get => _employee1;
            set { if (Set(ref _employee1, Digits(value))) Raise(nameof(Employee1Name)); }
        }

        public string Employee2
        {
            get => _employee2;
            set { if (Set(ref _employee2, Digits(value))) Raise(nameof(Employee2Name)); }
        }

        public string Employee1Name => Name(Employee1);
        public string Employee2Name => Name(Employee2);

        public string Area1 { get => _area1; set => Set(ref _area1, Upper(value)); }
        public string Area2 { get => _area2; set => Set(ref _area2, Upper(value)); }
        public string Area3 { get => _area3; set => Set(ref _area3, Upper(value)); }
        public string Area4 { get => _area4; set => Set(ref _area4, Upper(value)); }
        public string Area5 { get => _area5; set => Set(ref _area5, Upper(value)); }

        public string Message
        {
            get => _message;
            private set { if (Set(ref _message, value)) Raise(nameof(HasMessage)); }
        }

        public bool HasMessage => Message.Length > 0;

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        /// <summary>
        /// Opens on a unit, pre-filled from UNT1 when the unit file has it. With no
        /// unit (from the menu) the unit is typed and the pre-fill happens when it is.
        /// </summary>
        public void Open(string unitNo, Action<string> activated)
        {
            _activated = activated;
            _prefilledFor = string.Empty;
            Message = string.Empty;
            IsUnitEditable = string.IsNullOrWhiteSpace(unitNo);
            UnitNo = unitNo ?? string.Empty;

            // GeneralInitialization defaults: AGC1_RST_DFLT and JUR1_CAD_DFLT (listed first).
            Agency = Agencies.FirstOrDefault(a => a.IsDefault) ?? Agencies.FirstOrDefault();
            UnitType = UnitTypes.FirstOrDefault();
            Jurisdiction = Jurisdictions.FirstOrDefault();
            Station = Stations.FirstOrDefault();
            Employee1 = Employee2 = string.Empty;
            Area1 = Area2 = Area3 = Area4 = Area5 = string.Empty;

            Prefill();
            IsOpen = true;
            _shell.SetStatus(Title + (UnitNo.Length > 0 ? " - " + UnitNo : " - enter the unit") + ".");
        }

        /// <summary>Cad129 TabA_LoadUnitDefaults: UNT1 agency, station, type, crew, areas.</summary>
        public void Prefill()
        {
            if (UnitNo.Length == 0 || string.Equals(_prefilledFor, UnitNo, StringComparison.OrdinalIgnoreCase)) return;
            _prefilledFor = UnitNo;

            var defaults = Support?.UnitDefaults(UnitNo);
            if (defaults == null) return;

            // An agency not on the licensed list left the legacy screen to crash; it
            // is left at the default here and the operator picks.
            Agency = Agencies.FirstOrDefault(a => string.Equals(a.Code, defaults.Agency, StringComparison.OrdinalIgnoreCase)) ?? Agency;
            Jurisdiction = Jurisdictions.FirstOrDefault(j => string.Equals(j.Code, defaults.Jurisdiction, StringComparison.OrdinalIgnoreCase)) ?? Jurisdiction;
            UnitType = UnitTypes.FirstOrDefault(t => t.Code == defaults.UnitType.ToString()) ?? UnitType;
            Station = Stations.FirstOrDefault(s => string.Equals(s.Description, defaults.Station, StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(s.Code, defaults.Station, StringComparison.OrdinalIgnoreCase)) ?? Station;
            Employee1 = defaults.Employee1 > 0 ? defaults.Employee1.ToString() : string.Empty;
            Employee2 = defaults.Employee2 > 0 ? defaults.Employee2.ToString() : string.Empty;

            var areas = defaults.Areas ?? Array.Empty<string>();
            Area1 = areas.Length > 0 ? areas[0] : string.Empty;
            Area2 = areas.Length > 1 ? areas[1] : string.Empty;
            Area3 = areas.Length > 2 ? areas[2] : string.Empty;
            Area4 = areas.Length > 3 ? areas[3] : string.Empty;
            Area5 = areas.Length > 4 ? areas[4] : string.Empty;
        }

        public void Cancel()
        {
            IsOpen = false;
            _activated = null;
            _shell.SetStatus("Unit was not added to Roster.");
        }

        private void Save()
        {
            Prefill();
            if (!Validate()) return;

            var activation = new UnitActivation
            {
                UnitNo = UnitNo,
                Agency = Agency.Code,
                AgencyType = Agency.Type,
                Jurisdiction = Jurisdiction.Code,
                Employee1 = HasCrew ? ToInt(Employee1) : 0,
                Employee2 = HasCrew ? ToInt(Employee2) : 0,
                UnitType = short.TryParse(UnitType?.Code, out var type) ? type : (short)0,
                Station = HasStation && Station != null && Station.Code != "0" ? Station.Description : string.Empty,

                // Only police carry report areas - the legacy saved the disabled boxes too.
                Areas = IsPolice ? new[] { Area1, Area2, Area3, Area4, Area5 } : Array.Empty<string>()
            };

            var report = _shell.ActivateUnit(activation);
            if (report.Failed)
            {
                Message = report.Message;
                _shell.SetStatus(report.Message);
                return;
            }

            var done = _activated;
            _activated = null;
            IsOpen = false;
            _shell.SetStatus(report.Message);
            done?.Invoke(UnitNo);
        }

        /// <summary>Cad129 TabA_ValAll, in its order: employees, areas, agency, jurisdiction, station.</summary>
        private bool Validate()
        {
            if (UnitNo.Length == 0) return Fail("A Unit/Apparatus must be entered.");

            if (HasCrew)
            {
                if (IsPolice && ToInt(Employee1) == 0) return Fail("Police vehicles require Emp ID");
                if (ToInt(Employee1) > 0 && Employee1Name.Length == 0) return Fail("Employee ID is invalid.");
                if (ToInt(Employee2) > 0 && Employee2Name.Length == 0) return Fail("Employee ID is invalid.");
            }

            if (Agency == null) return Fail("Agency is invalid.");
            if (Jurisdiction == null) return Fail("Unit/Apparatus jurisdiction required");
            if (HasStation && (Station == null || Station.Code == "0")) return Fail("Fire/EMS vehicles require a Station.");

            Message = string.Empty;
            return true;
        }

        private bool Fail(string message)
        {
            Message = message;
            _shell.SetStatus(Title + ": " + message);
            return false;
        }

        private string Name(string id) => ToInt(id) > 0 ? Support?.PersonName(ToInt(id)) ?? string.Empty : string.Empty;

        private static string Digits(string value) => new string((value ?? string.Empty).Where(char.IsDigit).Take(6).ToArray());
        private static string Upper(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        private static int ToInt(string value) => int.TryParse(value, out var n) ? n : 0;
    }
}
