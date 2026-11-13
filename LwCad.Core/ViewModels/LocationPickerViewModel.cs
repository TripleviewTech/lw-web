using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Services.Location;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The location select screens - LaGeo041 Address, 042 Premise, 043
    /// Intersection, 044 Alarm Number, 045 Common Name - as one card, plus the
    /// premise list behind "Multiple Premises" (LwPmm905) and the split-segment
    /// L / R question (LaGeo043 DefaultSide).
    ///
    /// Buttons as the legacy windows have them: OK (or Enter or double-click)
    /// takes the row, Force accepts the entry as typed, Cancel (or Esc) abandons
    /// the lookup, View opens the geocode or premise record.
    /// </summary>
    public class LocationPickerViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;

        private bool _isOpen;
        private bool _isSideQuestion;
        private LocationPickList _list;
        private LocationEngine _engine;
        private LocationCandidate _pendingSide;
        private PickRow _selected;
        private string _message = string.Empty;
        private Action<LocationResult> _done;
        private Action _cancelled;
        private Func<int, LocationResult> _choosePremise;

        public LocationPickerViewModel(ShellViewModel shell)
        {
            _shell = shell;

            OkCommand = new RelayCommand(Ok);
            ForceCommand = new RelayCommand(Force);
            CancelCommand = new RelayCommand(Cancel);
            ViewCommand = new RelayCommand(View);
            LeftCommand = new RelayCommand(() => ChooseSide('L'));
            RightCommand = new RelayCommand(() => ChooseSide('R'));
        }

        public RelayCommand OkCommand { get; }
        public RelayCommand ForceCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ViewCommand { get; }
        public RelayCommand LeftCommand { get; }
        public RelayCommand RightCommand { get; }

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        /// <summary>LaGeo043 DefaultSide: "Split Record: Please select Street Side".</summary>
        public bool IsSideQuestion
        {
            get => _isSideQuestion;
            private set
            {
                if (Set(ref _isSideQuestion, value)) Raise(nameof(IsList));
            }
        }

        public bool IsList => !IsSideQuestion;

        public string Title => _list?.Title ?? string.Empty;

        /// <summary>tbx_LocEntered - what the operator typed.</summary>
        public string Entered => _list?.Query?.Original ?? string.Empty;

        public IReadOnlyList<string> Columns => _list?.Columns ?? Array.Empty<string>();

        public ObservableCollection<PickRow> Rows { get; } = new ObservableCollection<PickRow>();

        public PickRow SelectedRow
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        public bool CanForce => _list != null && _list.CanForce;

        public bool HasRows => Rows.Count > 0;

        public string Message
        {
            get => _message;
            private set
            {
                if (Set(ref _message, value)) Raise(nameof(HasMessage));
            }
        }

        public bool HasMessage => !string.IsNullOrEmpty(Message);

        /// <summary>Raised when the columns change, so the view can rebuild its grid.</summary>
        public event EventHandler ColumnsChanged;

        /// <summary>A lookup that needs the operator - a pick list or the side question.</summary>
        public void Open(LocationEngine engine, LocationCheck check, Action<LocationResult> done, Action cancelled)
        {
            _engine = engine;
            _done = done;
            _cancelled = cancelled;
            _choosePremise = null;
            Show(check);
        }

        /// <summary>The premises at a location - "Multiple Premises" (TakeCall_ListPremises).</summary>
        public void OpenPremises(LocationPickList list, Func<int, LocationResult> choose, Action<LocationResult> done)
        {
            _engine = null;
            _done = done;
            _cancelled = null;
            _choosePremise = choose;
            Show(new LocationCheck { Status = LocationStatus.Choose, Pick = list });
        }

        private void Show(LocationCheck check)
        {
            _list = check.Pick;
            _pendingSide = check.Status == LocationStatus.ChooseSide ? check.Pending : null;
            IsSideQuestion = check.Status == LocationStatus.ChooseSide;

            Rows.Clear();
            if (_list?.Rows != null)
            {
                foreach (var row in _list.Rows) Rows.Add(new PickRow(row));
            }

            SelectedRow = Rows.FirstOrDefault();
            Message = IsSideQuestion ? check.Message : Rows.Count == 0 ? "No matching location was found - Force accepts it as entered." : string.Empty;

            Raise(nameof(Title));
            Raise(nameof(Entered));
            Raise(nameof(Columns));
            Raise(nameof(CanForce));
            Raise(nameof(HasRows));
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
            IsOpen = true;
        }

        private void Ok()
        {
            if (IsSideQuestion) return;
            if (SelectedRow == null)
            {
                Message = "Select a location from the list.";
                return;
            }

            if (_choosePremise != null)
            {
                Finish(_choosePremise(SelectedRow.Candidate.PremiseNo));
                return;
            }

            Handle(_engine.Choose(_list.Query, SelectedRow.Candidate));
        }

        private void ChooseSide(char side)
        {
            if (!IsSideQuestion || _pendingSide == null) return;
            Handle(_engine.Choose(_list.Query, _pendingSide, side));
        }

        private void Force()
        {
            if (!CanForce || _engine == null) return;
            Handle(_engine.Force(_list.Query));
        }

        private void Handle(LocationCheck check)
        {
            switch (check.Status)
            {
                case LocationStatus.Resolved:
                    Finish(check.Result);
                    break;
                case LocationStatus.ChooseSide:
                    // Keep the list's query; the side question is asked over it.
                    Show(new LocationCheck { Status = LocationStatus.ChooseSide, Pick = _list, Pending = check.Pending, Message = check.Message });
                    break;
                case LocationStatus.Choose:
                    Show(check);
                    break;
                default:
                    Message = check.Message;
                    break;
            }
        }

        private void Finish(LocationResult result)
        {
            IsOpen = false;
            var done = _done;
            _done = null;
            _cancelled = null;
            done?.Invoke(result);
        }

        public void Cancel()
        {
            if (!IsOpen) return;

            // From the side question, back to the list; from the list, out.
            if (IsSideQuestion && _list?.Rows != null && _list.Rows.Count > 0)
            {
                Show(new LocationCheck { Status = LocationStatus.Choose, Pick = _list });
                return;
            }

            IsOpen = false;
            var cancelled = _cancelled;
            _done = null;
            _cancelled = null;
            cancelled?.Invoke();
        }

        /// <summary>View: LaGeo001 for a geocode, PMM001 for a premise - the legacy programs, when present.</summary>
        private void View()
        {
            if (SelectedRow == null) return;

            var c = SelectedRow.Candidate;
            var program = c.PremiseNo > 0 ? "PMM001" : "LaGeo001";
            if (!_shell.LaunchLegacyProgram(program, null))
            {
                Message = (c.PremiseNo > 0 ? "Premise " + c.PremiseNo : "GeoCode " + c.GeoKey) +
                          " - the record viewer (" + program + ") is not installed beside this console.";
            }
        }
    }

    public sealed class PickRow
    {
        public PickRow(LocationCandidate candidate)
        {
            Candidate = candidate;
        }

        public LocationCandidate Candidate { get; }

        public IReadOnlyList<string> Cells => Candidate.Cells;
    }
}
