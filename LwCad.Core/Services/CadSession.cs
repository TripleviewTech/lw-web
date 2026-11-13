using System;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Who is writing. The legacy console gets these from its logon: Gl_UserID /
    /// Gl_EmpId from the link parameters and the DAT1 session row (DAT1_EMP_ID,
    /// DAT1_SESS_ID, DAT1_911_POS). This build has no logon screen, so they come
    /// from the command line or the environment, and every write stamps them the
    /// way the legacy stamps Gl_UserID - DSE1_RCV_ID, DSU1_DSP_ID, DST1_ENT_BY,
    /// RST1_LOCK_ID and the rest.
    ///
    ///   --emp &lt;id&gt;   or LWCAD_EMP_ID    employee id (default 0, as LwCadSystem's gl_UserID)
    ///   --sess &lt;id&gt;  or LWCAD_SESS_ID   DAT1 session number (default 1)
    ///
    /// The keyboard template the menus are built from is the session's too -
    /// DAT1_KBRD_ID / DAT1_OP_TYPE / DAT1_ACC_LEVEL / DAT1_AGC_P-O, written by the
    /// legacy logon from CAD9. These override the DAT1 row, for testing a template:
    ///
    ///   --kbrd &lt;id&gt;    or LWCAD_KBRD_ID   KYB2_T_ID / KYB3_T_ID (default DEFAULT)
    ///   --optype &lt;c&gt;   or LWCAD_OP_TYPE   KYB2_T_TYPE: R, C, D, S or U (default D)
    ///   --sec &lt;0-9&gt;    or LWCAD_SEC       security level; 0 sees every key (default 0)
    /// </summary>
    public sealed class CadSession
    {
        public static CadSession Current { get; set; } = new CadSession();

        public int EmpId { get; set; }

        public short SessId { get; set; } = 1;

        /// <summary>DAT1_911_POS, read from the session row when there is one.</summary>
        public short E911Pos { get; set; }

        /// <summary>DAT1_KBRD_ID - the keyboard template. Blank means DEFAULT, as LwKyb001 requires.</summary>
        public string KeyboardId { get; set; } = "DEFAULT";

        /// <summary>DAT1_OP_TYPE - R restricted, C call taker, D dispatcher, S supervisor, U custom.</summary>
        public char OperatorType { get; set; } = 'D';

        /// <summary>DAT1_ACC_LEVEL, as a digit. 0 loads every key (OkToLoadFunction).</summary>
        public int SecurityLevel { get; set; }

        /// <summary>DAT1_AGC_P / F / E / O - the agencies the operator works this session.</summary>
        public char AgencyPolice { get; set; } = 'Y';
        public char AgencyFire { get; set; } = 'Y';
        public char AgencyEms { get; set; } = 'Y';
        public char AgencyOther { get; set; } = 'Y';

        /// <summary>Which keyboard values came from the command line and must not be replaced by DAT1.</summary>
        public bool KeyboardIdOverridden { get; private set; }
        public bool OperatorTypeOverridden { get; private set; }
        public bool SecurityOverridden { get; private set; }

        /// <summary>DSE1_PC_NAME / DSU1_PC_NAME - Environment.MachineName, as the legacy writes it.</summary>
        public string PcName { get; } = SafeMachineName();

        public static CadSession FromArgs(string[] args)
        {
            var session = new CadSession
            {
                EmpId = ReadInt(args, "--emp", "LWCAD_EMP_ID", 0),
                SessId = (short)ReadInt(args, "--sess", "LWCAD_SESS_ID", 1)
            };

            var kbrd = ReadText(args, "--kbrd", "LWCAD_KBRD_ID");
            if (!string.IsNullOrWhiteSpace(kbrd))
            {
                session.KeyboardId = kbrd.Trim().ToUpperInvariant();
                session.KeyboardIdOverridden = true;
            }

            var opType = ReadText(args, "--optype", "LWCAD_OP_TYPE");
            if (!string.IsNullOrWhiteSpace(opType))
            {
                session.OperatorType = char.ToUpperInvariant(opType.Trim()[0]);
                session.OperatorTypeOverridden = true;
            }

            var sec = ReadText(args, "--sec", "LWCAD_SEC");
            if (int.TryParse((sec ?? string.Empty).Trim(), out var level))
            {
                session.SecurityLevel = level;
                session.SecurityOverridden = true;
            }

            return session;
        }

        private static int ReadInt(string[] args, string name, string environment, int fallback)
        {
            var text = ReadText(args, name, environment);

            return int.TryParse((text ?? string.Empty).Trim('"', ' '), out var value) ? value : fallback;
        }

        private static string ReadText(string[] args, string name, string environment)
        {
            string text = null;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        text = args[i].Substring(name.Length + 1);
                    }
                    else if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        text = args[i + 1];
                    }
                }
            }

            text ??= Environment.GetEnvironmentVariable(environment);

            return text?.Trim('"', ' ');
        }

        private static string SafeMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
