using System.Windows.Input;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// One selectable action - used by the Quick Menu rail, the Standard Functions
    /// bar and the row menus. The rail and the bar build theirs from the keyboard
    /// template (KeySlot); the row menus from the function catalogue.
    /// </summary>
    public class MenuAction : ObservableObject
    {
        private bool _isVisible = true;
        private string _title;

        public MenuAction(string title, string shortcutKey, bool isEnabled, ICommand command,
                          object commandParameter = null)
        {
            _title = title;
            BaseTitle = title;
            ShortcutKey = shortcutKey;
            IsEnabled = isEnabled;
            Command = command;
            CommandParameter = commandParameter ?? title;
        }

        /// <summary>
        /// Settable because one entry changes what it is called with the board:
        /// Assign Call on a stacked event, Assign / Assist on an active one.
        /// </summary>
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        /// <summary>The title the entry was built with - a key's KYB literal - before any rename.</summary>
        public string BaseTitle { get; }

        /// <summary>Single character that completes the Ctrl+K chord, e.g. "Q".</summary>
        public string ShortcutKey { get; }

        /// <summary>Overrides the Ctrl+K text - "F3", "Shift F3" on the Standard Functions bar.</summary>
        public string KeyHint { get; init; }

        public string ShortcutText =>
            KeyHint ?? (string.IsNullOrEmpty(ShortcutKey) ? string.Empty : "Ctrl K+" + ShortcutKey);

        /// <summary>Tooltip: the key, the legacy program, and whether this build runs it.</summary>
        public string Detail { get; init; }

        /// <summary>True when the entry opens a function's notes rather than a working panel.</summary>
        public bool IsStub { get; init; }

        /// <summary>A key with nothing on it - shown as a blank cell on the bar, as the legacy does.</summary>
        public bool IsBlank { get; init; }

        public bool IsEnabled { get; }

        public ICommand Command { get; }

        /// <summary>Passed to <see cref="Command"/>; defaults to the entry's title.</summary>
        public object CommandParameter { get; }

        /// <summary>Cleared by the Quick Menu search box; always true in a row menu.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => Set(ref _isVisible, value);
        }
    }
}
