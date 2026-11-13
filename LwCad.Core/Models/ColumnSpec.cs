using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One column of a call or unit monitor. The original CAD lists let the
    /// dispatcher show and hide columns from a checklist, and carried a different
    /// default set per list; this is what that checklist edits.
    /// </summary>
    public class ColumnSpec : ObservableObject
    {
        private bool _isVisible;
        private double _width;

        public ColumnSpec(string key, string header, double width, bool isVisible,
                          bool isFlexible = false, bool canHide = true)
        {
            Key = key;
            Header = header;
            _width = width;
            DefaultWidth = width;
            _isVisible = isVisible;
            IsFlexible = isFlexible;
            CanHide = canHide;
        }

        /// <summary>Identifies the value to render; see the cell converters.</summary>
        public string Key { get; }

        public string Header { get; }

        /// <summary>
        /// Fixed width, or the star weight when <see cref="IsFlexible"/>. Settable so the
        /// dispatcher can drag a fixed column wider or narrower from its header (web build,
        /// 2026-11-12); a flexible column keeps whatever the fixed ones leave.
        /// </summary>
        public double Width
        {
            get => _width;
            set => Set(ref _width, value);
        }

        /// <summary>The list's default width, which <c>Reset to default</c> puts back.</summary>
        public double DefaultWidth { get; }

        /// <summary>Narrowest a dragged column may become.</summary>
        public const double MinWidth = 24;

        /// <summary>Flexible columns share whatever width the fixed ones leave.</summary>
        public bool IsFlexible { get; }

        /// <summary>False for a column the list cannot do without, e.g. Call Type.</summary>
        public bool CanHide { get; }

        public bool IsVisible
        {
            get => _isVisible;
            set => Set(ref _isVisible, value);
        }
    }
}
