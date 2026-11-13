using System.Collections.Generic;
using System.Linq;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// The four banks of the legacy keyboard template. Function, Shift and Ctrl
    /// are the 27 KYB2 keys behind the Standard Functions bar (CadMain
    /// LoadKeysCad); Quick is the KYB3 A-Z bank behind the Quick Menu (LoadKeysExt).
    /// </summary>
    public enum KeyBank
    {
        Function,
        Shift,
        Ctrl,
        Quick
    }

    /// <summary>
    /// One key of a keyboard template, as KYB2 / KYB3 hold it, plus what the
    /// loader resolved it to. The raw fields are kept so a stub can show the
    /// operator exactly which row and column a function came from.
    /// </summary>
    public sealed class KeySlot
    {
        public KeyBank Bank { get; init; }

        /// <summary>"1".."9" for the KYB2 banks, "A".."Z" for the Quick Menu.</summary>
        public string Position { get; init; }

        /// <summary>KYB*_TYPE: ' ' empty, 'F' a KYB1 function, 'P' a program id.</summary>
        public char Type { get; init; } = ' ';

        /// <summary>The caption - KYB*_LIT, or KYB1_KEY_LIT when the slot left it blank.</summary>
        public string Literal { get; init; } = string.Empty;

        /// <summary>KYB*_SEC: ' ' or '0'-'9', the lowest security level that sees the key.</summary>
        public char Security { get; init; } = ' ';

        /// <summary>KYB*_AVL: ' ' / A all, or P / F / E / O for one agency.</summary>
        public char Available { get; init; } = ' ';

        /// <summary>KYB*_FUNC as stored - a KYB1 function code or a program id.</summary>
        public string Function { get; init; } = string.Empty;

        /// <summary>The program the key launches, normalized: upper case, no .EXE / .INT.</summary>
        public string ProgramId { get; init; } = string.Empty;

        /// <summary>KYB1_DESC when the key went through KYB1.</summary>
        public string FunctionDescription { get; init; } = string.Empty;

        /// <summary>
        /// False when OkToLoadFunction refused the key for this session. The legacy
        /// loads a refused key as a blank button, and so does this build.
        /// </summary>
        public bool IsPermitted { get; init; } = true;

        /// <summary>A disabled blank cell - nothing configured, or not for this operator.</summary>
        public bool IsEmpty => !IsPermitted || string.IsNullOrWhiteSpace(Literal) || string.IsNullOrWhiteSpace(ProgramId);

        /// <summary>Where the key sits, as the operator presses it.</summary>
        public string KeyText
        {
            get
            {
                switch (Bank)
                {
                    case KeyBank.Shift: return "Shift F" + Position;
                    case KeyBank.Ctrl: return "Ctrl F" + Position;
                    case KeyBank.Quick: return "Ctrl K+" + Position;
                    default: return "F" + Position;
                }
            }
        }

        /// <summary>The short form the Standard Functions bar prints beside the caption.</summary>
        public string KeyHint
        {
            get
            {
                switch (Bank)
                {
                    case KeyBank.Shift: return "\u21E7F" + Position;   // up arrow: Shift
                    case KeyBank.Ctrl: return "^F" + Position;
                    case KeyBank.Quick: return Position;
                    default: return "F" + Position;
                }
            }
        }

        /// <summary>The table and column group the key was read from, e.g. "KYB2_S_*_4".</summary>
        public string SourceColumns
        {
            get
            {
                switch (Bank)
                {
                    case KeyBank.Shift: return "KYB2_S_*_" + Position;
                    case KeyBank.Ctrl: return "KYB2_C_*_" + Position;
                    case KeyBank.Quick: return "KYB3_X_*_" + Position;
                    default: return "KYB2_F_*_" + Position;
                }
            }
        }

        public static KeySlot Blank(KeyBank bank, string position) =>
            new KeySlot { Bank = bank, Position = position };
    }

    /// <summary>
    /// A loaded keyboard template: the 27 Standard Functions keys and the 26 Quick
    /// Menu keys, always full length so a key's index is its position on the bar.
    /// </summary>
    public sealed class Keymap
    {
        public Keymap(IEnumerable<KeySlot> standard, IEnumerable<KeySlot> quick)
        {
            Standard = standard.ToList();
            Quick = quick.ToList();
        }

        /// <summary>F1-F9, then Shift F1-F9, then Ctrl F1-F9 - the order of KybCLit.</summary>
        public IReadOnlyList<KeySlot> Standard { get; }

        /// <summary>A-Z - the order of KybELit.</summary>
        public IReadOnlyList<KeySlot> Quick { get; }

        /// <summary>KYB2_T_ID / KYB3_T_ID the template was read under.</summary>
        public string TemplateId { get; init; } = string.Empty;

        /// <summary>KYB2_T_TYPE: R restricted, C call taker, D dispatcher, S supervisor, U custom.</summary>
        public char OperatorType { get; init; } = ' ';

        /// <summary>Where the keys came from, for the status line.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Set when the template could not be read; the menus are then empty.</summary>
        public string Problem { get; init; } = string.Empty;

        public KeySlot Find(KeyBank bank, string position) =>
            (bank == KeyBank.Quick ? Quick : Standard)
            .FirstOrDefault(s => s.Bank == bank && s.Position == position);

        /// <summary>An empty template - every key a blank cell, as the legacy shows a missing KYB2.</summary>
        public static Keymap Empty(string problem, string templateId = "", char operatorType = ' ') =>
            new Keymap(AllStandardPositions().Select(p => KeySlot.Blank(p.Bank, p.Position)),
                       QuickPositions().Select(p => KeySlot.Blank(KeyBank.Quick, p)))
            {
                Problem = problem ?? string.Empty,
                TemplateId = templateId ?? string.Empty,
                OperatorType = operatorType
            };

        public static IEnumerable<(KeyBank Bank, string Position)> AllStandardPositions()
        {
            foreach (var bank in new[] { KeyBank.Function, KeyBank.Shift, KeyBank.Ctrl })
            {
                for (int n = 1; n <= 9; n++) yield return (bank, n.ToString());
            }
        }

        public static IEnumerable<string> QuickPositions()
        {
            for (char c = 'A'; c <= 'Z'; c++) yield return c.ToString();
        }
    }
}
