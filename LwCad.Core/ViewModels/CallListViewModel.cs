using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// One call monitor - Active, Stacked or Delayed. Each monitor owns its
    /// own ordered view of the shared call list plus the lock the dispatcher can
    /// apply to stop the list re-sorting under the mouse.
    /// </summary>
    public class CallListViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private bool _isLocked;

        public CallListViewModel(ShellViewModel shell, string title, CallQueue queue,
                                 string iconKey, bool badgeIsAlert,
                                 double fillWeight, double fillMinHeight,
                                 bool shrinksToHeader = false)
        {
            _shell = shell;
            Title = title;
            Queue = queue;
            IconKey = iconKey;
            BadgeIsAlert = badgeIsAlert;
            ShrinksToHeader = shrinksToHeader;

            _fillWeight = fillWeight;
            _fillMinHeight = fillMinHeight;
            _fillHeight = new GridLength(fillWeight, GridUnitType.Star);
            _rowHeight = _fillHeight;
            _rowMinHeight = fillMinHeight;

            ToggleLockCommand = new RelayCommand(() =>
            {
                IsLocked = !IsLocked;
                _shell.SetStatus(Title + (IsLocked ? " list locked - auto refresh held." : " list unlocked."));
                if (!IsLocked) Rebuild();
            });

            PopOutCommand = new RelayCommand(() =>
                _shell.SetStatus(Title + " pop-out monitor is not enabled in this build."));

            MoreCommand = new RelayCommand(() =>
                _shell.SetStatus(Title + " options menu is not enabled in this build."));

            ResetColumnsCommand = new RelayCommand(ResetColumns);

            // Space toggles the preview on the row the cursor is on.
            ToggleExpandCommand = new RelayCommand(p => _shell.ToggleCallExpand(p as CadCall));

            ToggleSizeCommand = new RelayCommand(() =>
            {
                if (IsShrunk) Restore(); else Shrink();
                _shell.SetStatus(DescribeSize());
            });

            Columns = BuildColumns(queue);
            foreach (var column in Columns) column.PropertyChanged += OnColumnChanged;
        }

        /// <summary>
        /// Columns in the order the LwCadSystem list showed them, with the same
        /// default visibility (CadMain.xaml). Everything the original carried is
        /// present; the ones it kept hidden are one click away in the column chooser.
        ///
        /// Lowell differs from Easthampton here: Jur is carried but hidden, Area
        /// and P are shown on all three monitors and Duration on Stacked and Active,
        /// and there is no Crew column - the EMS crew check is Easthampton's.
        /// </summary>
        private static ObservableCollection<ColumnSpec> BuildColumns(CallQueue queue)
        {
            var columns = new ObservableCollection<ColumnSpec>
            {
                // DSE1_EVENT_TYPE as a badge, the same PD / FD / EMS / OTH the Unit
                // Monitor puts in front of a unit number. Stacked and Active carry it
                // by default; Delayed is the narrow list and keeps it one click away.
                new ColumnSpec("Agency", "Agency", 58, queue != CallQueue.Delayed),
                new ColumnSpec("CallType", "Call Type", 132, true, canHide: false),
                new ColumnSpec("Jur", "Jur", 42, false),
                new ColumnSpec("Area", "Area", 44, true),
                new ColumnSpec("Location", "Location", 1, true, isFlexible: true),
                new ColumnSpec("Time", "Time", 50, true),
                new ColumnSpec("Duration", "Duration", 64, queue != CallQueue.Delayed),
                new ColumnSpec("Priority", "P", 26, true)
            };

            // All three LW lists show Additional Info - on Delayed it is the
            // activation time. Only the Active list carried Case No.
            columns.Add(new ColumnSpec("Info", "Additional Info", 118, true));
            columns.Add(new ColumnSpec("CaseNo", "Case No.", 94, queue == CallQueue.Active));

            return columns;
        }

        public string Title { get; }
        public CallQueue Queue { get; }
        public string IconKey { get; }


        /// <summary>Stacked / Delayed count badges are red; Active is green.</summary>
        public bool BadgeIsAlert { get; }

        public ObservableCollection<CadCall> Items { get; } = new ObservableCollection<CadCall>();

        public ObservableCollection<ColumnSpec> Columns { get; }

        /// <summary>Drives the chooser list and the rendered strip.</summary>
        public IEnumerable<ColumnSpec> VisibleColumns => Columns.Where(c => c.IsVisible);

        public RelayCommand ResetColumnsCommand { get; }

        public RelayCommand ToggleExpandCommand { get; }

        public RelayCommand ToggleSizeCommand { get; }

        // ------------------------------------------------------------- sizing

        // The monitors share the board's height, and the dispatcher decides how.
        // Two things move these: the splitter between two monitors, which writes a
        // dragged height straight back through RowHeight, and the chevron, which
        // shrinks a monitor down to what it is holding.
        //
        // Both end up in the same property so there is one source of truth.
        //
        // What "shrink" means is set per monitor. Stacked and Active fit to their
        // rows: their calls stay on screen and the monitor is only as tall as it
        // needs to be, which is an Auto row, which is what makes it follow its own
        // row count as calls arrive and clear. Delayed collapses to its header bar
        // instead - some agencies carry a great many delayed calls, and fitting to
        // a dozen of them would take more of the board than a list nobody is
        // working deserves. Its count badge stays visible either way.

        private readonly double _fillWeight;
        private readonly double _fillMinHeight;

        /// <summary>The share this monitor had before it was shrunk, restored by the chevron.</summary>
        private GridLength _fillHeight;

        private GridLength _rowHeight;
        private double _rowMinHeight;
        private bool _isShrunk;

        /// <summary>
        /// True for a monitor whose chevron collapses it to its header bar rather
        /// than fitting it to its rows. Delayed, and only Delayed.
        /// </summary>
        public bool ShrinksToHeader { get; }

        /// <summary>
        /// Bound two-way to the monitor's grid row. Two-way because the splitter
        /// resizes by writing the row's Height, and a one-way binding would be
        /// replaced by that write - after which the chevron would stop working.
        /// </summary>
        public GridLength RowHeight
        {
            get => _rowHeight;
            set
            {
                if (_rowHeight == value) return;

                _rowHeight = value;

                // A dragged splitter hands back a concrete height. That is the
                // dispatcher overruling the chevron, so the shrink is dropped rather
                // than fighting the drag on the next relist.
                if (!value.IsAuto)
                {
                    _fillHeight = value;
                    if (_isShrunk)
                    {
                        _isShrunk = false;
                        _rowMinHeight = _fillMinHeight;
                        RaiseSizing();
                    }
                }

                Raise(nameof(RowHeight));
            }
        }

        /// <summary>
        /// The row's minimum. Nothing while shrunk, so a monitor holding one call -
        /// or none - really does give the rest of its space back.
        /// </summary>
        public double RowMinHeight => _rowMinHeight;

        /// <summary>Shrunk by the chevron: fitted to its rows, or collapsed to its header.</summary>
        public bool IsShrunk => _isShrunk;

        /// <summary>Shrunk to its rows, which is every monitor but Delayed.</summary>
        public bool IsFitted => _isShrunk && !ShrinksToHeader;

        /// <summary>
        /// False only for a monitor collapsed to its header bar. A fitted monitor
        /// still shows its calls - that is the point of fitting rather than
        /// collapsing.
        /// </summary>
        public bool ShowRows => !_isShrunk || !ShrinksToHeader;

        /// <summary>Column headers need rows to head, and a list to head them in.</summary>
        public bool ShowColumnHeader => ShowRows && HasItems;

        /// <summary>What the chevron will do, for its tooltip and its automation name.</summary>
        public string ShrinkActionText
        {
            get
            {
                if (_isShrunk) return "Give " + Title + " its share of the board back";

                return ShrinksToHeader
                    ? "Collapse " + Title + " to its header bar"
                    : "Fit " + Title + " to the calls it is holding";
            }
        }

        /// <summary>
        /// How tall the rows may grow while fitted. This is what keeps a monitor
        /// holding forty calls from swallowing the board - and, because WPF applies
        /// MaxHeight to the measure constraint, it is also what keeps the list
        /// virtualised: the panel is still measured against a bounded height, so it
        /// realises a screenful of rows rather than all of them.
        /// </summary>
        public double RowsMaxHeight => IsFitted ? FitRowLimit * RowPixels : double.PositiveInfinity;

        /// <summary>Rows a fitted monitor shows before it starts scrolling instead of growing.</summary>
        public const int FitRowLimit = 12;

        /// <summary>Height of one unexpanded row, matching the row template.</summary>
        private const double RowPixels = 27;

        /// <summary>Shrinks the monitor - to its rows, or to its header bar.</summary>
        public void Shrink()
        {
            if (_isShrunk) return;

            if (!_rowHeight.IsAuto) _fillHeight = _rowHeight;

            _isShrunk = true;
            _rowHeight = GridLength.Auto;
            _rowMinHeight = 0;

            RaiseSizing();
            Raise(nameof(RowHeight));
        }

        /// <summary>Gives the monitor its share of the board back.</summary>
        public void Restore()
        {
            if (!_isShrunk) return;

            _isShrunk = false;
            _rowHeight = _fillHeight.IsAuto ? new GridLength(_fillWeight, GridUnitType.Star) : _fillHeight;
            _rowMinHeight = _fillMinHeight;

            RaiseSizing();
            Raise(nameof(RowHeight));
        }

        private void RaiseSizing()
        {
            Raise(nameof(IsShrunk));
            Raise(nameof(IsFitted));
            Raise(nameof(ShowRows));
            Raise(nameof(ShowColumnHeader));
            Raise(nameof(ShrinkActionText));
            Raise(nameof(RowMinHeight));
            Raise(nameof(RowsMaxHeight));
        }

        private string DescribeSize()
        {
            if (!IsShrunk) return Title + " filling its share of the board.";

            if (ShrinksToHeader)
            {
                return Count == 0
                    ? Title + " collapsed - nothing held."
                    : Title + " collapsed - " + Count + (Count == 1 ? " call held." : " calls held.");
            }

            if (Count == 0) return Title + " fitted - no calls, so the header bar only.";

            return Count > FitRowLimit
                ? Title + " fitted to " + FitRowLimit + " rows of " + Count + "; the rest scroll."
                : Title + " fitted to its " + Count + (Count == 1 ? " call." : " calls.");
        }

        private void OnColumnChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColumnSpec.IsVisible)) Raise(nameof(VisibleColumns));
        }

        private void ResetColumns()
        {
            var defaults = BuildColumns(Queue);

            foreach (var column in Columns)
            {
                var match = defaults.FirstOrDefault(d => d.Key == column.Key);
                if (match != null) { column.IsVisible = match.IsVisible; column.Width = match.Width; }
            }

            _shell.SetStatus(Title + " columns reset.");
        }

        public int Count => Items.Count;

        public bool HasItems => Items.Count > 0;

        /// <summary>
        /// While locked the monitor keeps its current rows and ordering even when
        /// the underlying call list changes, which is what stops a timer relist
        /// from pulling a row out from under the dispatcher.
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => Set(ref _isLocked, value);
        }

        public RelayCommand ToggleLockCommand { get; }
        public RelayCommand PopOutCommand { get; }
        public RelayCommand MoreCommand { get; }

        internal void Rebuild()
        {
            if (IsLocked) return;

            var wanted = _shell.SourceCalls
                .Where(c => c.Queue == Queue)
                .OrderBy(c => c.PriorityNumber)
                .ThenBy(c => c.ReceivedAt)
                .ToList();

            if (SameAs(wanted)) return;

            // Replace in place rather than clearing, so the selected row keeps its
            // container (and therefore its highlight) whenever it survives the relist.
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(Items[i])) Items.RemoveAt(i);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                var call = wanted[i];
                int current = Items.IndexOf(call);
                if (current < 0) Items.Insert(i, call);
                else if (current != i) Items.Move(current, i);
            }

            Raise(nameof(Count));
            Raise(nameof(HasItems));
            Raise(nameof(ShowColumnHeader));
        }

        private bool SameAs(IList<CadCall> wanted)
        {
            if (wanted.Count != Items.Count) return false;
            for (int i = 0; i < wanted.Count; i++)
            {
                if (!ReferenceEquals(wanted[i], Items[i])) return false;
            }
            return true;
        }
    }
}
