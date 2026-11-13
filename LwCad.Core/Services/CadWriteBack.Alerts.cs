using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Services.Data;
using LwTableDse1;
using LwTableDst1;
using LwTableStk1;

namespace LwCadSystemUX.Services
{
    // CAD Alerts - LwCad301 (acknowledge) and LwCad302 (add).
    //
    // A call alert is a DST1 row: DST1_RCD_TYPE 'E', transaction type 5/7/999,
    // DST1_ALERT_SW 'P' while it waits and 'X' once acknowledged, with ACK_BY /
    // ACK_DATE stamped. The legacy consoles poll for 'P' rows on stacked and active
    // calls (CadMain Timers.CheckAlerts) and flash their alert indicator.
    public sealed partial class CadWriteBack
    {
        // FatPotComm.ActionType.Acknowledge.
        private const char FatAcknowledge = '5';

        /// <summary>The remark LwCad301 looks for before sending FatPot an acknowledge.</summary>
        public const string CadFusionRemark = "Call received from CADfusion";

        /// <summary>What an alert write did.</summary>
        public sealed class AlertResult
        {
            public bool Written;
            public int Count;
            public string Message;
        }

        /// <summary>
        /// LwCad301 TabA_Acknowledge, for each ticked alert: DST1_ALERT_SW 'X',
        /// DST1_ACK_DATE now, DST1_ACK_BY the employee. An alert someone else has
        /// acknowledged meanwhile is left alone, as the legacy leaves it
        /// (DST1_ACK_BY &gt; 0). An alert whose remarks carry "Call received from
        /// CADfusion" also queues a FatPot acknowledge for the event (FAT2 DSE1, 5).
        ///
        /// Unlike the legacy, which wrote each row on its own connection and
        /// swallowed failures, all the ticked alerts commit together or not at all.
        /// TSK1 301 afterwards, so every console's alert indicator catches up.
        /// </summary>
        public AlertResult AcknowledgeAlerts(char eventType, short eventNo1, int eventNo2, IEnumerable<long> alertKeys)
        {
            var keys = (alertKeys ?? Enumerable.Empty<long>()).Distinct().ToList();
            var result = new AlertResult();

            if (keys.Count == 0)
            {
                result.Message = "Tick the alerts to acknowledge first.";
                return result;
            }

            var already = 0;

            try
            {
                using (var tx = new CadTransaction(_connectString))
                {
                    var now = ServerNow(tx);

                    foreach (var key in keys)
                    {
                        var dst1 = _dst1.GetRowForUpdate(new DST1 { DST1_T_KEY = key }, tx);

                        if (_dst1.RecordStatus != 0 ||
                            dst1.DST1_EVENT_TYPE != eventType ||
                            dst1.DST1_EVENT_NO_1 != eventNo1 ||
                            dst1.DST1_EVENT_NO_2 != eventNo2)
                        {
                            throw new CadWriteException("Alert " + key + " is no longer on " +
                                                        FormatEvent(eventNo1, eventNo2) + ". Refresh and try again.");
                        }

                        if (dst1.DST1_ACK_BY > 0 || dst1.DST1_ALERT_SW != 'P')
                        {
                            already++;
                            continue;
                        }

                        dst1.DST1_ALERT_SW = 'X';
                        dst1.DST1_ACK_DATE = now;
                        dst1.DST1_ACK_BY = _session.EmpId;
                        _dst1.UpdateRow(dst1, tx);

                        if ((dst1.DST1_REMARKS ?? string.Empty).Contains(CadFusionRemark))
                        {
                            Fat2(tx, "DSE1", EventKeyText(eventType, eventNo1, eventNo2), FatAcknowledge);
                        }

                        result.Count++;
                    }

                    tx.Commit();
                }

                result.Written = true;
                result.Message = result.Count > 0
                    ? Plural(result.Count, "alert") + " acknowledged on " + FormatEvent(eventNo1, eventNo2) +
                      (already > 0 ? "; " + already + " had already been acknowledged" : string.Empty) + "."
                    : "Nothing to acknowledge on " + FormatEvent(eventNo1, eventNo2) + " - " +
                      (already == 1 ? "that alert had" : "those alerts had") + " already been acknowledged.";

                if (result.Count > 0) SendTasks((301, null));
            }
            catch (CadWriteException ex)
            {
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                LaLog.Error("CadWriteBack", "Alert acknowledge failed.", ex);
                result.Message = "The alerts were not acknowledged: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// LwCad302 TabA_Save: one pending alert for every event on the call's global
        /// number, so each agency working the call sees it - DST1_RCD_TYPE 'E',
        /// DST1_ALERT_SW 'P', type 5/7/999, entered by the employee. The legacy
        /// alert screens refuse a delayed call, and so does this. TSK1 301 afterwards.
        /// </summary>
        public AlertResult AddAlert(char eventType, short eventNo1, int eventNo2, string remarks)
        {
            var result = new AlertResult();
            remarks = (remarks ?? string.Empty).Trim();

            if (remarks.Length == 0)
            {
                result.Message = "Enter alert message to send.";
                return result;
            }

            try
            {
                using (var tx = new CadTransaction(_connectString))
                {
                    var eventArgs = new { t = eventType.ToString(), n1 = eventNo1, n2 = eventNo2 };

                    var stack = _stk1.GetList("STK1_EVENT_TYPE = @t AND STK1_EVENT_NO_1 = @n1 AND STK1_EVENT_NO_2 = @n2",
                                              eventArgs, tx);
                    if (stack.Any(s => s.STK1_STATUS == 'D'))
                    {
                        throw new CadWriteException("Alert function not supported by Delayed calls.");
                    }

                    var events = _dse1.GetList("DSE1_EVENT_TYPE = @t AND DSE1_EVENT_NO_1 = @n1 AND DSE1_EVENT_NO_2 = @n2",
                                               eventArgs, tx);
                    if (events.Count == 0)
                    {
                        throw new CadWriteException("Event " + FormatEvent(eventNo1, eventNo2) + " was not found.");
                    }

                    var source = events[0];

                    // Every agency's event under the one global number. An event with
                    // no global number (older rows) gets the alert on itself only.
                    if (source.DSE1_G_EVENT_2 > 0)
                    {
                        events = _dse1.GetList("DSE1_G_EVENT_1 = @g1 AND DSE1_G_EVENT_2 = @g2",
                                               new { g1 = source.DSE1_G_EVENT_1, g2 = source.DSE1_G_EVENT_2 }, tx);
                    }

                    var now = ServerNow(tx);

                    foreach (var dse1 in events)
                    {
                        var dst1 = _dst1.Initialize(new DST1());
                        dst1.DST1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
                        dst1.DST1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
                        dst1.DST1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
                        dst1.DST1_RCD_TYPE = 'E';
                        dst1.DST1_ALERT_SW = 'P';
                        dst1.DST1_TRAN_DATE = now;
                        dst1.DST1_DOW = Dow(now);
                        dst1.DST1_T_TYPE_S = 5;
                        dst1.DST1_T_TYPE_L = 7;
                        dst1.DST1_T_TYPE_C = 999;
                        dst1.DST1_ENT_BY = _session.EmpId;
                        dst1.DST1_REMARKS = remarks;

                        _dst1.InsertRow(dst1, tx);
                        result.Count++;
                    }

                    tx.Commit();
                }

                result.Written = true;
                result.Message = "Alert sent to " + FormatEvent(eventNo1, eventNo2) +
                                 (result.Count > 1 ? " and " + Plural(result.Count - 1, "linked event") + " on its global number" : string.Empty) +
                                 ".";

                SendTasks((301, null));
            }
            catch (CadWriteException ex)
            {
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                LaLog.Error("CadWriteBack", "Alert not added.", ex);
                result.Message = "The alert was not sent: " + ex.Message;
            }

            return result;
        }

        /// <summary>The FAT2 key of an event - the same text Dse1Key builds from a row.</summary>
        private static string EventKeyText(char type, short no1, int no2) =>
            type + no1.ToString() + no2.ToString().PadLeft(8, '0');

        private static string Plural(int count, string one, string many = null) =>
            count + " " + (count == 1 ? one : many ?? one + "s");
    }
}
