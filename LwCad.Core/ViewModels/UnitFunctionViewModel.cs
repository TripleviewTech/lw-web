using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The three unit functions - Unit Status, Location Change and Release Unit.
    /// The legacy LwCad135 screen runs Status and Location as two modes of one form,
    /// which is why they share a panel here; Release is the third mode.
    ///
    /// Validation follows that screen: the unit must be one the monitor is showing,
    /// a status change needs a status, a location change needs a location, and a
    /// release needs a unit that is actually on a call.
    /// </summary>
    public class UnitFunctionViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private bool _isOpen;
        private UnitFunction _function = UnitFunction.Status;
        private CadUnit _unit;
        private string _unitNo = string.Empty;
        private UnitStatusOption _status;
        private string _location = string.Empty;
        private string _timeout = "0";
        private string _remarks = string.Empty;
        private string _validationMessage = string.Empty;

        public UnitFunctionViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            ApplyCommand = new RelayCommand(Apply);
            CancelCommand = new RelayCommand(Cancel);
            SetFunctionCommand = new RelayCommand(p =>
            {
                if (p is UnitFunction f) Function = f;
                else if (p is string s && Enum.TryParse(s, out UnitFunction parsed)) Function = parsed;
            });
        }

        public IReadOnlyList<UnitStatusOption> Statuses => _data.UnitStatuses;

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        public UnitFunction Function
        {
            get => _function;
            set
            {
                if (!Set(ref _function, value)) return;

                Raise(nameof(Title));
                Raise(nameof(IsStatus));
                Raise(nameof(IsLocation));
                Raise(nameof(IsRelease));
                Raise(nameof(ApplyLabel));
                Raise(nameof(Summary));
                ValidationMessage = string.Empty;
            }
        }

        public bool IsStatus => Function == UnitFunction.Status;
        public bool IsLocation => Function == UnitFunction.Location;
        public bool IsRelease => Function == UnitFunction.Release;

        public string Title
        {
            get
            {
                switch (Function)
                {
                    case UnitFunction.Location: return "Location Change";
                    case UnitFunction.Release: return "Release Unit";
                    default: return "Unit Status";
                }
            }
        }

        public string ApplyLabel => IsRelease ? "Release" : "Apply";

        /// <summary>The unit the function is acting on, shown read-only in the header.</summary>
        public CadUnit Unit
        {
            get => _unit;
            private set
            {
                if (!Set(ref _unit, value)) return;

                Raise(nameof(Summary));
                Raise(nameof(HasUnit));
                Raise(nameof(StatusHint));
                Raise(nameof(HasStatusHint));
            }
        }

        public bool HasUnit => Unit != null;

        public string UnitNo
        {
            get => _unitNo;
            set
            {
                if (!Set(ref _unitNo, value)) return;

                Unit = _data.Units.FirstOrDefault(
                    u => string.Equals(u.UnitNo, (value ?? string.Empty).Trim(),
                                       StringComparison.OrdinalIgnoreCase));

                AssignedCall = Unit == null
                    ? null
                    : _data.Calls.FirstOrDefault(
                        c => c.AssignedUnits.Any(a => a.UnitNo == Unit.UnitNo));

                // Naming a different unit re-defaults the status: the step being
                // confirmed belongs to the unit, not to the screen.
                Status = NextStatusFor(Unit);
            }
        }

        /// <summary>What the unit is doing now, so the operator can see what they are changing.</summary>
        public string Summary
        {
            get
            {
                if (Unit == null) return "No unit selected.";

                var parts = new List<string> { Unit.Status };
                if (!string.IsNullOrWhiteSpace(Unit.CallType)) parts.Add(Unit.CallType);
                if (!string.IsNullOrWhiteSpace(Unit.Location)) parts.Add(Unit.Location);

                return string.Join("  -  ", parts);
            }
        }

        /// <summary>
        /// The call the unit is on, shown before a release so the operator can see
        /// what they are taking the unit off.
        /// </summary>
        private CadCall _assignedCall;
        public CadCall AssignedCall
        {
            get => _assignedCall;
            private set
            {
                Set(ref _assignedCall, value);

                // Raised whether or not the reference changed. Two units on one call
                // resolve to the same CadCall, so reopening the panel on the second
                // of them would otherwise leave the note reading the answer worked
                // out for the first - and the count it turns on has moved since.
                Raise(nameof(HasAssignedCall));
                Raise(nameof(ReleaseEffect));
                Raise(nameof(IsLastUnitOnCall));
                Raise(nameof(CallWillRestack));
            }
        }

        public bool HasAssignedCall => AssignedCall != null;

        /// <summary>
        /// What releasing this unit actually does to the call.
        ///
        /// The panel used to say flatly that the call stays on the board, which is
        /// only true in one of three cases. While other units are still on the call
        /// the release does not touch it at all. It is the last unit coming off that
        /// decides the event, and then it is the call code that decides: CCC1_RESTACK
        /// marks the codes significant enough to go back on the stack for another
        /// unit, and a code without it closes when its last unit clears.
        /// </summary>
        public string ReleaseEffect
        {
            get
            {
                if (AssignedCall == null)
                {
                    return "Releasing takes the unit off its call and returns it to service.";
                }

                if (!IsLastUnitOnCall)
                {
                    var others = AssignedCall.AssignedUnits.Count - 1;
                    return "Releasing takes the unit off its call and returns it to service. " +
                           others + " other unit" + (others == 1 ? " is" : "s are") +
                           " still assigned, so the call is not affected.";
                }

                if (AssignedCall.RestackOnRelease)
                {
                    return "This is the last unit on the call. Call code " + CallCodeText() +
                           " is flagged to restack, so releasing it puts the event back on the " +
                           "Stacked monitor for another unit rather than closing it.";
                }

                return "This is the last unit on the call. Call code " + CallCodeText() +
                       " is not flagged to restack, so releasing it closes the event.";
            }
        }

        /// <summary>True when no other unit is left on the call being released from.</summary>
        public bool IsLastUnitOnCall =>
            AssignedCall != null && AssignedCall.AssignedUnits.Count <= 1;

        /// <summary>The call is going back on the stack rather than closing.</summary>
        public bool CallWillRestack => IsLastUnitOnCall && AssignedCall.RestackOnRelease;

        private string CallCodeText()
        {
            var code = (AssignedCall.CallCode ?? string.Empty).Trim();
            return code.Length > 0 ? code : "for this event";
        }

        public UnitStatusOption Status
        {
            get => _status;
            set
            {
                Set(ref _status, value);

                // Raised whether or not the status changed. Reopening on a unit that
                // has since reached the end of its chain defaults to the same status
                // it defaulted to last time, so the setter would not run - and the
                // hint would still be explaining a step that is no longer being made.
                Raise(nameof(StatusHint));
                Raise(nameof(HasStatusHint));
            }
        }

        /// <summary>
        /// The status the screen opens on: the one UST1_N_STATUS says follows what
        /// the unit is doing now. A unit part-way through a call is nearly always
        /// making the next step - dispatched to en route, en route to arrived - so
        /// opening on the current status meant retyping what the unit already was.
        ///
        /// Falls back to the current status when nothing follows it, which is how an
        /// in-service unit is set up: being available leads nowhere in particular.
        /// </summary>
        private UnitStatusOption NextStatusFor(CadUnit unit)
        {
            if (unit == null) return Statuses.FirstOrDefault();

            var current = Statuses.FirstOrDefault(
                s => string.Equals(s.Abbreviation, unit.Status, StringComparison.OrdinalIgnoreCase));

            if (current == null) return Statuses.FirstOrDefault();

            var next = current.HasNextStatus
                ? Statuses.FirstOrDefault(s => s.Code == current.NextStatusCode)
                : null;

            return next ?? current;
        }

        /// <summary>
        /// Says why the status is preselected, so a defaulted step reads as a
        /// suggestion the operator is confirming rather than as something already
        /// entered. Empty when nothing was advanced.
        /// </summary>
        public string StatusHint
        {
            get
            {
                if (Unit == null || Status == null) return string.Empty;
                if (string.Equals(Status.Abbreviation, Unit.Status, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                var current = Statuses.FirstOrDefault(
                    s => string.Equals(s.Abbreviation, Unit.Status, StringComparison.OrdinalIgnoreCase));

                if (current == null || current.NextStatusCode != Status.Code) return string.Empty;

                return "Next after " + current.Abbreviation + ".";
            }
        }

        public bool HasStatusHint => StatusHint.Length > 0;

        public string Location
        {
            get => _location;
            set => Set(ref _location, value);
        }

        /// <summary>Minutes before the monitor flags the unit; 0 means no timer.</summary>
        public string Timeout
        {
            get => _timeout;
            set => Set(ref _timeout, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => Set(ref _remarks, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (Set(ref _validationMessage, value)) Raise(nameof(HasValidationMessage));
            }
        }

        public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

        public RelayCommand ApplyCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand SetFunctionCommand { get; }

        /// <summary>Opens the panel for a function, pre-filled with the unit if one is selected.</summary>
        public void Open(UnitFunction function, CadUnit unit)
        {
            Function = function;
            ValidationMessage = string.Empty;
            Remarks = string.Empty;

            UnitNo = unit != null ? unit.UnitNo : string.Empty;
            Unit = unit;

            // Resolved explicitly: reopening on the same unit leaves UnitNo unchanged,
            // so its setter would not run and the call detail would go stale.
            AssignedCall = unit == null
                ? null
                : _data.Calls.FirstOrDefault(c => c.AssignedUnits.Any(a => a.UnitNo == unit.UnitNo));

            Location = unit != null && function == UnitFunction.Status ? unit.Location : string.Empty;
            Timeout = unit != null ? unit.TimeoutMinutes.ToString() : "0";

            Status = NextStatusFor(unit);

            IsOpen = true;
            _shell.SetStatus(Title + " - choose a unit and apply.");
        }

        public void Cancel()
        {
            IsOpen = false;
            _shell.SetStatus(Title + " cancelled.");
        }

        private void Apply()
        {
            if (!Validate()) return;

            var change = new UnitChange(Function, Unit)
            {
                Status = Status,
                Location = Location,
                TimeoutMinutes = ParsedTimeout(),
                Remarks = Remarks
            };

            var callEffect = DescribeCallEffect();

            var report = _shell.ApplyUnitChange(change);

            // Refused by the tables - the unit is locked by another console, or the
            // write failed. Stay open on the reason; nothing was changed.
            if (report.Failed)
            {
                Fail(report.Message);
                return;
            }

            IsOpen = false;
            _shell.SetStatus(report.Saved ? report.Message : DescribeResult(callEffect) + report.Message);
        }

        /// <summary>
        /// Says what became of the call, read before the release is applied because
        /// applying it is what takes the unit off the call's assigned list.
        /// </summary>
        private string DescribeCallEffect()
        {
            if (AssignedCall == null || !IsLastUnitOnCall) return "The call is unchanged.";

            return AssignedCall.RestackOnRelease
                ? "Last unit off, and call code " + CallCodeText() + " restacks, so event " +
                  AssignedCall.EventNo + " is back on the stack."
                : "Last unit off, and call code " + CallCodeText() + " does not restack, so event " +
                  AssignedCall.EventNo + " is closed.";
        }

        private string DescribeResult(string releasedCallEffect)
        {
            switch (Function)
            {
                case UnitFunction.Location:
                    return "Unit " + Unit.UnitNo + " location set to " + Unit.Location + ".";
                case UnitFunction.Release:
                    return "Unit " + Unit.UnitNo + " released and back in service. " + releasedCallEffect;
                default:
                    return "Unit " + Unit.UnitNo + " set to " + Unit.Status + ".";
            }
        }

        private int ParsedTimeout() => int.TryParse((Timeout ?? string.Empty).Trim(), out var v) && v > 0 ? v : 0;

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(UnitNo)) return Fail("Unit must be specified.");
            if (Unit == null) return Fail("Unit " + UnitNo.Trim() + " is not on the monitor.");

            if (IsStatus && Status == null) return Fail("Status must be specified.");

            if (IsLocation && string.IsNullOrWhiteSpace(Location))
            {
                return Fail("Location must be specified.");
            }

            if (IsRelease && Unit.AssignedCallKey == 0 && string.IsNullOrWhiteSpace(Unit.CallType))
            {
                return Fail("Unit " + Unit.UnitNo + " is not assigned to a call.");
            }

            if (!string.IsNullOrWhiteSpace(Timeout) && !int.TryParse(Timeout.Trim(), out _))
            {
                return Fail("Timeout must be a number of minutes.");
            }

            ValidationMessage = string.Empty;
            return true;
        }

        private bool Fail(string message)
        {
            ValidationMessage = message;
            _shell.SetStatus(Title + ": " + message);
            return false;
        }
    }
}
