using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Functions;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// The stub section a key opens when this build has no panel for its function.
    /// It says which key and which legacy program it is, what the program needs
    /// from the board and whether the board has it, and carries the notes taken
    /// from the legacy source - so an operator sees a function that is planned
    /// rather than a button that does nothing, and a developer sees what to build.
    /// </summary>
    public class FunctionStubViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;

        private bool _isOpen;
        private CadFunctionInfo _function;
        private KeySlot _slot;
        private string _contextText = string.Empty;
        private bool _hasContext = true;

        public FunctionStubViewModel(ShellViewModel shell)
        {
            _shell = shell;
            CloseCommand = new RelayCommand(Cancel);
        }

        public RelayCommand CloseCommand { get; }

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        public CadFunctionInfo Function
        {
            get => _function;
            private set => Set(ref _function, value);
        }

        public KeySlot Slot
        {
            get => _slot;
            private set => Set(ref _slot, value);
        }

        /// <summary>The key's caption when it came from a key, else the catalogue's name.</summary>
        public string Title =>
            Slot != null && !string.IsNullOrWhiteSpace(Slot.Literal) ? Slot.Literal : Function?.Title ?? string.Empty;

        /// <summary>"Ctrl F8 - LwCad137 Reset Stack Timeout", or the catalogue name from a row menu.</summary>
        public string Subtitle
        {
            get
            {
                if (Function == null) return string.Empty;

                var name = Function.IsUncatalogued ? "Uncatalogued program" : Function.Title;
                if (Slot != null) return Slot.KeyText + "  -  " + name;

                // Opened from the top bar or a row menu: the title already is the name.
                return string.Equals(name, Title, StringComparison.OrdinalIgnoreCase) ? string.Empty : name;
            }
        }

        public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

        public string Legacy => Function?.Legacy ?? string.Empty;
        public string Source => Function?.Source ?? string.Empty;
        public string Purpose => Function?.Purpose ?? string.Empty;
        public string Invocation => Function?.Invocation ?? string.Empty;
        public string Reads => Function?.Reads ?? string.Empty;
        public string Writes => Function?.Writes ?? string.Empty;

        public IReadOnlyList<string> Rules => Function?.Rules ?? Array.Empty<string>();
        public IReadOnlyList<string> Notes => Function?.Notes ?? Array.Empty<string>();
        public IReadOnlyList<string> ToBuild => Function?.ToBuild ?? Array.Empty<string>();

        public bool HasSource => !string.IsNullOrWhiteSpace(Source);
        public bool HasInvocation => !string.IsNullOrWhiteSpace(Invocation);
        public bool HasReads => !string.IsNullOrWhiteSpace(Reads);
        public bool HasWrites => !string.IsNullOrWhiteSpace(Writes);
        public bool HasRules => Rules.Count > 0;
        public bool HasNotes => Notes.Count > 0;
        public bool HasToBuild => ToBuild.Count > 0;

        /// <summary>
        /// Where the key was read from - "KYB2_C_*_8  type P  FUNC LWCAD137" - plus
        /// the KYB1 description when the key went through KYB1.
        /// </summary>
        public string KeySource
        {
            get
            {
                if (Slot == null) return string.Empty;

                var text = Slot.SourceColumns + "   type " + (Slot.Type == ' ' ? "-" : Slot.Type.ToString()) +
                           "   FUNC " + Slot.Function;

                if (!string.Equals(Slot.Function, Slot.ProgramId, StringComparison.OrdinalIgnoreCase))
                {
                    text += "  ->  KYB1_PRG_ID " + Slot.ProgramId;
                }

                if (!string.IsNullOrWhiteSpace(Slot.FunctionDescription)) text += "   (" + Slot.FunctionDescription + ")";

                return text;
            }
        }

        public bool HasKeySource => Slot != null;

        /// <summary>What the legacy program needs from the board, and whether the board has it now.</summary>
        public string ContextText
        {
            get => _contextText;
            private set => Set(ref _contextText, value);
        }

        /// <summary>False when the function needs a call or unit the board has not got selected.</summary>
        public bool HasContext
        {
            get => _hasContext;
            private set => Set(ref _hasContext, value);
        }

        public void Open(CadFunctionInfo function, KeySlot slot)
        {
            if (function == null) return;

            Function = function;
            Slot = slot;
            DescribeContext();

            Raise(string.Empty);
            IsOpen = true;

            _shell.SetStatus(Title + " is not built in this console yet - its notes are open.");
        }

        public void Cancel()
        {
            IsOpen = false;
        }

        private void DescribeContext()
        {
            var call = _shell.SelectedCall;
            var unit = _shell.Units.SelectedUnit;

            switch (Function.Needs)
            {
                case FunctionNeeds.SelectedCall:
                    Describe("a selected call", call != null, call?.EventNo);
                    break;

                case FunctionNeeds.StackedCall:
                    Describe("a call on the Stacked monitor", call != null && call.Queue == CallQueue.Stacked,
                             call == null ? null : call.EventNo + " (" + call.Queue + ")");
                    break;

                case FunctionNeeds.ActiveCall:
                    Describe("a call on the Active monitor", call != null && call.Queue == CallQueue.Active,
                             call == null ? null : call.EventNo + " (" + call.Queue + ")");
                    break;

                case FunctionNeeds.SelectedUnit:
                    Describe("a selected unit", unit != null, unit?.UnitNo);
                    break;

                case FunctionNeeds.UnitAssignment:
                    Describe("a unit's assignment on a call (DSU1)",
                             call != null && call.HasAssignedUnits,
                             call == null ? null : call.EventNo + (call.HasAssignedUnits ? " with units" : " with no units"));
                    break;

                default:
                    ContextText = "Needs nothing selected on the board.";
                    HasContext = true;
                    break;
            }
        }

        private void Describe(string wanted, bool satisfied, string current)
        {
            HasContext = satisfied;
            ContextText = "Needs " + wanted + ". " +
                          (string.IsNullOrWhiteSpace(current)
                              ? "Nothing is selected."
                              : (satisfied ? "Would run on " : "Selected: ") + current + ".");
        }
    }
}
