namespace LwCadSystem.Web.Services;

/// <summary>
/// The <c>CadSystem</c> section of appsettings - what the WPF console took as
/// command-line switches. The same switches are still accepted on the command
/// line (see <see cref="CadStartup"/>) and win over appsettings.
/// </summary>
public sealed class CadSystemOptions
{
    public const string Section = "CadSystem";

    /// <summary>Skip the database and run on the demo board (WPF <c>--demo</c>).</summary>
    public bool Demo { get; set; }

    /// <summary>The La.cfg to use (WPF <c>--cfg</c>). Blank: LWCAD_LA_CFG, then beside the app, then the working directory.</summary>
    public string? LaCfgPath { get; set; }

    /// <summary>Employee every write is stamped with (WPF <c>--emp</c> / LWCAD_EMP_ID). Default 0.</summary>
    public int? EmpId { get; set; }

    /// <summary>DAT1 session number (WPF <c>--sess</c> / LWCAD_SESS_ID). Default 1.</summary>
    public int? SessId { get; set; }

    /// <summary>Folder the legacy programs are looked for in (WPF <c>--legacy</c> / LWCAD_LEGACY_DIR).</summary>
    public string? LegacyFolder { get; set; }

    /// <summary>Keyboard template, instead of the session's DAT1_KBRD_ID (WPF <c>--kbrd</c>).</summary>
    public string? Keyboard { get; set; }

    /// <summary>Operator type R / C / D / S / U, instead of DAT1_OP_TYPE (WPF <c>--optype</c>).</summary>
    public string? OpType { get; set; }

    /// <summary>Security level 0-9, instead of DAT1_ACC_LEVEL (WPF <c>--sec</c>). 0 sees every key.</summary>
    public int? SecurityLevel { get; set; }

    /// <summary>
    /// Address of the web CAD Mapping app (LwCadMapping.Web) that the Map button opens, e.g.
    /// <c>https://localhost:7195/</c>. Blank: the Map button falls back to the legacy program or its notes.
    /// </summary>
    public string? MappingUrl { get; set; }
}
