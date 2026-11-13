using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.ViewModels
{
    public enum UnitFilter
    {
        All,
        Available,
        Overdue
    }

    /// <summary>
    /// The Unit Monitor panel: the All / Available / Overdue segmented filter,
    /// the live counts beside each segment and the filtered unit rows.
    /// </summary>
    public class UnitMonitorViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private UnitFilter _filter = UnitFilter.All;
        private bool _isLocked;
        private bool _agencyFilterOn;
        private CadUnit _selectedUnit;

        public UnitMonitorViewModel(ShellViewModel shell)
        {
            _shell = shell;

            SetFilterCommand = new RelayCommand(p =>
            {
                if (p is UnitFilter f) Filter = f;
                else if (p is string s && Enum.TryParse(s, out UnitFilter parsed)) Filter = parsed;
            });

            ToggleLockCommand = new RelayCommand(() =>
            {
                IsLocked = !IsLocked;
                _shell.SetStatus("Unit Monitor " + (IsLocked ? "locked - auto refresh held." : "unlocked."));
                if (!IsLocked) Rebuild();
            });

            ToggleAgencyFilterCommand = new RelayCommand(() =>
            {
                AgencyFilterOn = !AgencyFilterOn;
                Rebuild();
                _shell.SetStatus(AgencyFilterOn
                    ? "Unit Monitor showing police units only."
                    : "Unit Monitor showing all agencies.");
            });

            PopOutCommand = new RelayCommand(() =>
                _shell.SetStatus("Unit Monitor pop-out is not enabled in this build."));

            MoreCommand = new RelayCommand(() =>
                _shell.SetStatus("Unit Monitor options menu is not enabled in this build."));

            SelectUnitCommand = new RelayCommand(p => SelectedUnit = p as CadUnit);
            ResetColumnsCommand = new RelayCommand(ResetColumns);

            Columns = BuildColumns();
            foreach (var column in Columns) column.PropertyChanged += OnColumnChanged;
        }

        /// <summary>
        /// The LwCadSystem Unit Monitor shows every column - Unit, Station, Type,
        /// Veh ID, Call Type, Area, Location, Time - with Status last (CadMain.xaml).
        /// Easthampton hid Station, Veh ID and Area and put Status fourth. The
        /// timeout indicator was its own narrow column; here it rides along in Time.
        /// </summary>
        private static ObservableCollection<ColumnSpec> BuildColumns()
        {
            return new ObservableCollection<ColumnSpec>
            {
                new ColumnSpec("Unit", "Unit", 88, true, canHide: false),
                new ColumnSpec("Station", "Station", 66, true),
                new ColumnSpec("Type", "Type", 62, true),
                new ColumnSpec("VehId", "Veh ID", 62, true),
                new ColumnSpec("CallType", "Call Type", 128, true),
                new ColumnSpec("Area", "Area", 52, true),
                new ColumnSpec("Location", "Location", 1, true, isFlexible: true),
                new ColumnSpec("Time", "Time", 68, true),
                new ColumnSpec("Status", "Status", 58, true)
            };
        }

        public ObservableCollection<CadUnit> Items { get; } = new ObservableCollection<CadUnit>();

        public ObservableCollection<ColumnSpec> Columns { get; }

        public IEnumerable<ColumnSpec> VisibleColumns => Columns.Where(c => c.IsVisible);

        public RelayCommand ResetColumnsCommand { get; }

        private void OnColumnChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColumnSpec.IsVisible)) Raise(nameof(VisibleColumns));
        }

        private void ResetColumns()
        {
            var defaults = BuildColumns();

            foreach (var column in Columns)
            {
                var match = defaults.FirstOrDefault(d => d.Key == column.Key);
                if (match != null) { column.IsVisible = match.IsVisible; column.Width = match.Width; }
            }

            _shell.SetStatus("Unit Monitor columns reset.");
        }

        public UnitFilter Filter
        {
            get => _filter;
            set
            {
                if (Set(ref _filter, value))
                {
                    Raise(nameof(IsAllSelected));
                    Raise(nameof(IsAvailableSelected));
                    Raise(nameof(IsOverdueSelected));
                    Rebuild();
                }
            }
        }

        public bool IsAllSelected => Filter == UnitFilter.All;
        public bool IsAvailableSelected => Filter == UnitFilter.Available;
        public bool IsOverdueSelected => Filter == UnitFilter.Overdue;

        public int AllCount => _shell.SourceUnits.Count;
        public int AvailableCount => _shell.SourceUnits.Count(u => u.IsAvailable);
        public int OverdueCount => _shell.SourceUnits.Count(u => u.IsOverdue);

        public bool IsLocked
        {
            get => _isLocked;
            set => Set(ref _isLocked, value);
        }

        public bool AgencyFilterOn
        {
            get => _agencyFilterOn;
            set => Set(ref _agencyFilterOn, value);
        }

        public CadUnit SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                var previous = _selectedUnit;
                if (!Set(ref _selectedUnit, value)) return;

                if (previous != null) previous.IsSelected = false;
                if (value != null)
                {
                    value.IsSelected = true;
                    _shell.SetStatus("Unit " + value.UnitNo + " - " + value.Status + " at " + value.Location + ".");
                }
            }
        }

        public RelayCommand SetFilterCommand { get; }
        public RelayCommand ToggleLockCommand { get; }
        public RelayCommand ToggleAgencyFilterCommand { get; }
        public RelayCommand PopOutCommand { get; }
        public RelayCommand MoreCommand { get; }
        public RelayCommand SelectUnitCommand { get; }

        internal void RaiseCounts()
        {
            Raise(nameof(AllCount));
            Raise(nameof(AvailableCount));
            Raise(nameof(OverdueCount));
        }

        internal void Rebuild()
        {
            RaiseCounts();
            if (IsLocked) return;

            IEnumerable<CadUnit> query = _shell.SourceUnits;

            if (AgencyFilterOn) query = query.Where(u => u.Agency == Agency.Police);

            switch (Filter)
            {
                case UnitFilter.Available:
                    query = query.Where(u => u.IsAvailable);
                    break;
                case UnitFilter.Overdue:
                    query = query.Where(u => u.IsOverdue);
                    break;
            }

            var wanted = query.ToList();

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(Items[i])) Items.RemoveAt(i);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                var unit = wanted[i];
                int current = Items.IndexOf(unit);
                if (current < 0) Items.Insert(i, unit);
                else if (current != i) Items.Move(current, i);
            }
        }
    }
}
