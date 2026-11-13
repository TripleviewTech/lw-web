using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwTableDse1;
using LwTableDst1;

namespace LwCadSystemUX.Services
{
    // CAD Alerts on the live tables - LwCad301 / LwCad302.
    public partial class SqlCadDataService
    {
        // A pending alert on a call still being worked: CadMain Timers.CheckAlerts
        // and LwCad301 TabA_LoadCallList both join STK1 status S or A to DST1 'P'.
        private const string PendingOnLiveCall =
            "DST1_ALERT_SW = 'P' AND EXISTS (SELECT 1 FROM dbo.STK1 (NOLOCK) " +
            "WHERE STK1_EVENT_TYPE = DST1_EVENT_TYPE AND STK1_EVENT_NO_1 = DST1_EVENT_NO_1 " +
            "AND STK1_EVENT_NO_2 = DST1_EVENT_NO_2 AND STK1_STATUS IN ('S', 'A'))";

        /// <summary>Calls with an alert waiting, as of the last refresh - the top bar's badge.</summary>
        public int PendingAlertCallCount { get; private set; }

        /// <summary>
        /// Counted on every monitor cycle, as the legacy console's alert timer checks.
        /// A failure here does not set LastError - the monitors are fine without it.
        /// </summary>
        private void RefreshAlertCount()
        {
            try
            {
                var rows = _dst1.GetList(where: PendingOnLiveCall);
                PendingAlertCallCount = rows
                    .Select(r => (r.DST1_EVENT_TYPE, r.DST1_EVENT_NO_1, r.DST1_EVENT_NO_2))
                    .Distinct()
                    .Count();
            }
            catch (Exception ex)
            {
                LaLog.Error("DST1", "Pending alert count not read.", ex);
            }
        }

        /// <summary>LwCad301's call list: every stacked or active call with pending alerts.</summary>
        public IReadOnlyList<PendingAlerts> GetPendingAlerts()
        {
            var rows = _dst1.GetList(where: PendingOnLiveCall);

            var list = rows
                .GroupBy(r => (r.DST1_EVENT_TYPE, r.DST1_EVENT_NO_1, r.DST1_EVENT_NO_2))
                .Select(g =>
                {
                    var dse1 = _dse1.GetRow(new DSE1
                    {
                        DSE1_EVENT_TYPE = g.Key.DST1_EVENT_TYPE,
                        DSE1_EVENT_NO_1 = g.Key.DST1_EVENT_NO_1,
                        DSE1_EVENT_NO_2 = g.Key.DST1_EVENT_NO_2
                    });

                    return new PendingAlerts
                    {
                        EventType = g.Key.DST1_EVENT_TYPE,
                        EventNo1 = g.Key.DST1_EVENT_NO_1,
                        EventNo2 = g.Key.DST1_EVENT_NO_2,
                        Count = g.Count(),
                        IsCadFusion = _dse1.RecordStatus == 0 && !string.IsNullOrWhiteSpace(dse1.DSE1_FATPOT_ID)
                    };
                })
                .ToList();

            PendingAlertCallCount = list.Count;
            return list;
        }

        /// <summary>
        /// LwCad301 TabA_LoadAlerts: the event's alerts, pending and acknowledged,
        /// pending first and then in the order they were entered.
        /// </summary>
        public IReadOnlyList<CallAlert> GetAlerts(CadCall call)
        {
            if (call == null) return Array.Empty<CallAlert>();

            var rows = _dst1.GetList(
                where: "DST1_EVENT_TYPE = @t AND DST1_EVENT_NO_1 = @n1 AND DST1_EVENT_NO_2 = @n2 " +
                       "AND DST1_ALERT_SW IN ('P', 'X')",
                parameters: new { t = call.EventType.ToString(), n1 = call.EventNo1, n2 = call.EventNo2 },
                orderBy: "DST1_ALERT_SW, DST1_T_KEY");

            return rows.Select(ToAlert).ToList();
        }

        public WriteReport AcknowledgeAlerts(CadCall call, IReadOnlyCollection<long> alertKeys)
        {
            if (call == null) return new WriteReport { Failed = true, Message = "No call was given." };

            var result = _writer.AcknowledgeAlerts(call.EventType, call.EventNo1, call.EventNo2, alertKeys);
            return AlertReport(result);
        }

        public WriteReport AddAlert(CadCall call, string remarks)
        {
            if (call == null) return new WriteReport { Failed = true, Message = "No call was given." };

            var result = _writer.AddAlert(call.EventType, call.EventNo1, call.EventNo2, remarks);
            return AlertReport(result);
        }

        private WriteReport AlertReport(CadWriteBack.AlertResult result)
        {
            var report = new WriteReport
            {
                Saved = result.Written,
                Failed = !result.Written,
                Message = result.Message
            };

            if (report.Saved) RefreshAlertCount();
            return report;
        }

        private static CallAlert ToAlert(DST1 row) => new CallAlert
        {
            Key = row.DST1_T_KEY,
            IsPending = row.DST1_ALERT_SW == 'P',
            EnteredAt = row.DST1_TRAN_DATE,
            EnteredBy = row.DST1_ENT_BY,
            Remarks = (row.DST1_REMARKS ?? string.Empty).Trim(),
            AcknowledgedBy = row.DST1_ACK_BY,
            AcknowledgedAt = row.DST1_ACK_DATE
        };
    }
}
