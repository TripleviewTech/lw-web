using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwTableKyb1;
using LwTableKyb2;
using LwTableKyb3;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Builds the two menus from the keyboard tables, the way LwCadSystem's
    /// CadMain.LoadKeyFunctions does at start-up:
    ///
    ///   KYB2 (T_ID, T_TYPE)  the Standard Functions bar - 27 keys, F / Shift-F / Ctrl-F 1-9
    ///                        (LoadKeysCad). A key's FUNC is a KYB1 function code, which
    ///                        RunCadFunction resolves to KYB1_PRG_ID.
    ///   KYB3 (T_ID, T_TYPE)  the Quick Menu - 26 keys, A-Z (LoadKeysExt). RunCadXFunction
    ///                        launches FUNC as it stands; LwKyb001 only offers type P there.
    ///
    /// Every key goes through OkToLoadFunction first, and a refused key is loaded as
    /// a blank so the others keep their places - the legacy does the same.
    ///
    /// The template key is the session's: DAT1_KBRD_ID and DAT1_OP_TYPE, which the
    /// legacy logon copied from CAD9_KBRD_ID / CAD9_D_TEMP.
    /// </summary>
    public static class KeymapLoader
    {
        private static readonly char[] Banks = { 'F', 'S', 'C' };

        public static Keymap Load(string connectString, CadSession session)
        {
            session ??= new CadSession();

            var templateId = string.IsNullOrWhiteSpace(session.KeyboardId) ? "DEFAULT" : session.KeyboardId.Trim();
            var opType = session.OperatorType;
            var label = "KYB2/KYB3 (" + templateId + ", " + opType + ")";

            var kyb1 = new Kyb1_RW(connectString);
            var kyb2 = new Kyb2_RW(connectString);
            var kyb3 = new Kyb3_RW(connectString);

            var header = kyb2.GetRow(new KYB2 { KYB2_T_ID = templateId, KYB2_T_TYPE = opType });
            if (kyb2.RecordStatus != 0)
            {
                // CadMain.LoadKeyFunctions returns here and the whole bar stays blank.
                return Keymap.Empty(kyb2.RecordStatus == 9
                                        ? "No keyboard template in KYB2 for " + templateId + " / " + opType + "."
                                        : "KYB2 could not be read: " + kyb2.ErrorMsg,
                                    templateId, opType);
            }

            // KYB1 is small and read once per key otherwise; one list read covers it.
            var functions = kyb1.GetList(orderBy: "KYB1_FUNC")
                                .Where(f => !string.IsNullOrWhiteSpace(f.KYB1_FUNC))
                                .GroupBy(f => f.KYB1_FUNC.Trim().ToUpperInvariant())
                                .ToDictionary(g => g.Key, g => g.First());

            KYB1 Lookup(string code) =>
                functions.TryGetValue((code ?? string.Empty).Trim().ToUpperInvariant(), out var row) ? row : null;

            var standard = new List<KeySlot>();
            foreach (var bank in Banks)
            {
                for (int n = 1; n <= 9; n++)
                {
                    var suffix = "_" + bank + "_{0}_" + n;
                    standard.Add(BuildSlot(BankOf(bank), n.ToString(),
                                           Get<char>(header, "KYB2" + string.Format(suffix, "TYPE")),
                                           Get<string>(header, "KYB2" + string.Format(suffix, "LIT")),
                                           Get<char>(header, "KYB2" + string.Format(suffix, "SEC")),
                                           Get<char>(header, "KYB2" + string.Format(suffix, "AVL")),
                                           Get<string>(header, "KYB2" + string.Format(suffix, "FUNC")),
                                           session, Lookup, alwaysThroughKyb1: true));
                }
            }

            var quick = new List<KeySlot>();
            var problem = string.Empty;
            var ext = kyb3.GetRow(new KYB3 { KYB3_T_ID = templateId, KYB3_T_TYPE = opType });

            if (kyb3.RecordStatus == 0)
            {
                foreach (var letter in Keymap.QuickPositions())
                {
                    var suffix = "KYB3_X_{0}_" + letter;
                    quick.Add(BuildSlot(KeyBank.Quick, letter,
                                        Get<char>(ext, string.Format(suffix, "TYPE")),
                                        Get<string>(ext, string.Format(suffix, "LIT")),
                                        Get<char>(ext, string.Format(suffix, "SEC")),
                                        Get<char>(ext, string.Format(suffix, "AVL")),
                                        Get<string>(ext, string.Format(suffix, "FUNC")),
                                        session, Lookup, alwaysThroughKyb1: false));
                }
            }
            else
            {
                // The legacy also returns before MapCadKeyboard here, which leaves the
                // F-key bar blank as well. Keeping the KYB2 keys is kinder and changes
                // nothing about what the operator may run.
                quick.AddRange(Keymap.QuickPositions().Select(p => KeySlot.Blank(KeyBank.Quick, p)));
                problem = kyb3.RecordStatus == 9
                    ? "No Quick Menu in KYB3 for " + templateId + " / " + opType + "."
                    : "KYB3 could not be read: " + kyb3.ErrorMsg;
            }

            return new Keymap(standard, quick)
            {
                TemplateId = templateId,
                OperatorType = opType,
                Source = label + (string.IsNullOrWhiteSpace(header.KYB2_T_DESC) ? string.Empty : " " + header.KYB2_T_DESC.Trim()),
                Problem = problem
            };
        }

        /// <summary>
        /// One key. A type F key is looked up in KYB1 - its program is KYB1_PRG_ID and
        /// a blank caption falls back to KYB1_KEY_LIT, as LwKyb001 fills it in. A type
        /// P key's FUNC is the program itself. The Quick Menu launches FUNC as stored
        /// (RunCadXFunction), so there KYB1 is consulted only for a type F key.
        /// </summary>
        public static KeySlot BuildSlot(KeyBank bank, string position, char type, string literal,
                                        char security, char available, string function,
                                        CadSession session, Func<string, KYB1> lookup, bool alwaysThroughKyb1)
        {
            type = char.ToUpperInvariant(type == '\0' ? ' ' : type);
            literal = (literal ?? string.Empty).Trim();
            function = (function ?? string.Empty).Trim();

            if (type == ' ' && function.Length == 0)
            {
                return KeySlot.Blank(bank, position);
            }

            var program = function;
            var description = string.Empty;

            if (type == 'F' || (alwaysThroughKyb1 && type != 'P'))
            {
                var row = lookup?.Invoke(function);
                if (row != null)
                {
                    if (!string.IsNullOrWhiteSpace(row.KYB1_PRG_ID)) program = row.KYB1_PRG_ID.Trim();
                    description = (row.KYB1_DESC ?? string.Empty).Trim();
                    if (literal.Length == 0) literal = (row.KYB1_KEY_LIT ?? string.Empty).Trim();
                }
            }

            return new KeySlot
            {
                Bank = bank,
                Position = position,
                Type = type,
                Literal = literal,
                Security = security == '\0' ? ' ' : security,
                Available = available == '\0' ? ' ' : char.ToUpperInvariant(available),
                Function = function,
                ProgramId = NormalizeProgramId(program),
                FunctionDescription = description,
                IsPermitted = OkToLoadFunction(security, available, session)
            };
        }

        /// <summary>
        /// CadMain.OkToLoadFunction. Security 0 sees every key; otherwise the key's
        /// level must not exceed the operator's, and a key for one agency needs the
        /// session to be working that agency (DAT1_AGC_P / F / E / O).
        /// </summary>
        public static bool OkToLoadFunction(char security, char available, CadSession session)
        {
            if (session == null || session.SecurityLevel == 0) return true;

            int.TryParse(security.ToString(), out var required);
            if (session.SecurityLevel < required) return false;

            switch (char.ToUpperInvariant(available))
            {
                case 'P': return session.AgencyPolice == 'Y';
                case 'F': return session.AgencyFire == 'Y';
                case 'E': return session.AgencyEms == 'Y';
                case 'O': return session.AgencyOther == 'Y';
                default: return true;
            }
        }

        /// <summary>
        /// DAT1_ACC_LEVEL as the digit it was meant to be. The legacy logon writes
        /// <c>(short)gl_HoldAccLevel</c> where gl_HoldAccLevel is the char CAD9_SEC_LVL,
        /// so a level of '5' lands in DAT1 as 53, its character code. Read back as
        /// stored, every operator would outrank every key. 48-57 are taken to be that
        /// mistake and turned back into 0-9; anything else is used as it is.
        /// </summary>
        public static int SecurityDigit(short stored)
        {
            if (stored >= '0' && stored <= '9') return stored - '0';
            return stored;
        }

        /// <summary>
        /// A program id as RunCadFunction compares it: upper case, with any folder
        /// and the .EXE / .INT extension removed.
        /// </summary>
        public static string NormalizeProgramId(string program)
        {
            var text = (program ?? string.Empty).Trim().Trim('"');
            if (text.Length == 0) return string.Empty;

            var slash = text.LastIndexOfAny(new[] { '\\', '/' });
            if (slash >= 0) text = text.Substring(slash + 1);

            text = text.ToUpperInvariant();
            if (text.EndsWith(".EXE", StringComparison.Ordinal) || text.EndsWith(".INT", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 4);
            }

            return text.Trim();
        }

        private static KeyBank BankOf(char bank) =>
            bank == 'S' ? KeyBank.Shift : bank == 'C' ? KeyBank.Ctrl : KeyBank.Function;

        private static T Get<T>(object record, string field)
        {
            var info = record.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (info == null) throw new MissingFieldException(record.GetType().Name, field);

            var value = info.GetValue(record);
            return value is T typed ? typed : default;
        }
    }
}
