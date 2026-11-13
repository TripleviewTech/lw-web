using System;
using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// One call alert - a DST1 row with DST1_ALERT_SW 'P' (pending) or 'X'
    /// (acknowledged), as LwCad301 lists them: pending first, then oldest first.
    /// </summary>
    public sealed class CallAlert : ObservableObject
    {
        private bool _isChecked;

        /// <summary>DST1_T_KEY.</summary>
        public long Key { get; init; }

        /// <summary>DST1_ALERT_SW = 'P'.</summary>
        public bool IsPending { get; init; }

        /// <summary>DST1_TRAN_DATE.</summary>
        public DateTime? EnteredAt { get; init; }

        /// <summary>DST1_ENT_BY.</summary>
        public int EnteredBy { get; init; }

        public string Remarks { get; init; } = string.Empty;

        /// <summary>DST1_ACK_BY / DST1_ACK_DATE, once acknowledged.</summary>
        public int AcknowledgedBy { get; init; }
        public DateTime? AcknowledgedAt { get; init; }

        /// <summary>The acknowledge tick - LwCad301's AlertRow xbx_Ack. Only a pending alert has one.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value && IsPending);
        }

        public string TimeText => EnteredAt?.ToString("HH:mm:ss") ?? string.Empty;

        public string EnteredText => EnteredBy > 0 ? "by " + EnteredBy : string.Empty;

        public string StatusText
        {
            get
            {
                if (IsPending) return "Pending";

                var text = "Acknowledged";
                if (AcknowledgedAt != null) text += " " + AcknowledgedAt.Value.ToString("HH:mm:ss");
                if (AcknowledgedBy > 0) text += " by " + AcknowledgedBy;
                return text;
            }
        }

        /// <summary>Came in over the CADfusion interface; acknowledging it tells FatPot.</summary>
        public bool IsFromCadFusion =>
            (Remarks ?? string.Empty).IndexOf("Call received from CADfusion", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// A stacked or active call with alerts waiting - one row of LwCad301's call
    /// list (TabA_LoadCallList), without the legacy's duplicate row per alert.
    /// </summary>
    public sealed class PendingAlerts
    {
        public char EventType { get; init; }
        public short EventNo1 { get; init; }
        public int EventNo2 { get; init; }

        /// <summary>DST1 rows with DST1_ALERT_SW 'P' on the event.</summary>
        public int Count { get; init; }

        /// <summary>DSE1_FATPOT_ID is set - LwCad301 paints these rows yellow.</summary>
        public bool IsCadFusion { get; init; }

        /// <summary>STK1_S_KEY when known - the demo board has no event numbers to match on.</summary>
        public long StackKey { get; init; }

        public bool Matches(CadCall call) =>
            call != null && (StackKey > 0
                ? call.EventKey == StackKey
                : call.EventType == EventType && call.EventNo1 == EventNo1 && call.EventNo2 == EventNo2);
    }
}
