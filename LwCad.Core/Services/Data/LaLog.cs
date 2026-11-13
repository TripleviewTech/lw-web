using System;
using System.Collections.Generic;

namespace LwCadSystemUX.Services.Data
{
    /// <summary>One data-layer failure, as reported by <see cref="LaLog"/>.</summary>
    public class LaLogEntry
    {
        public DateTime When { get; set; } = DateTime.Now;
        public string Table { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public override string ToString() =>
            When.ToString("HH:mm:ss") + "  " + Table + "  " + Context + "  " + Message;
    }

    /// <summary>
    /// Stands in for the legacy LaLogError.LogError, which wrote failures to a
    /// database table. Writing an error report through the same connection that
    /// just failed is not much use, so this keeps the last entries in memory and
    /// raises an event the shell can surface; persisting them is the host's call.
    /// </summary>
    public static class LaLog
    {
        private const int Capacity = 200;

        private static readonly object Gate = new object();
        private static readonly Queue<LaLogEntry> Entries = new Queue<LaLogEntry>();

        public static event EventHandler<LaLogEntry> Logged;

        public static void Error(string table, string context, Exception ex)
        {
            Error(table, context, ex?.Message ?? string.Empty);
        }

        public static void Error(string table, string context, string message)
        {
            var entry = new LaLogEntry
            {
                Table = table ?? string.Empty,
                Context = context ?? string.Empty,
                Message = message ?? string.Empty
            };

            lock (Gate)
            {
                Entries.Enqueue(entry);
                while (Entries.Count > Capacity) Entries.Dequeue();
            }

            Logged?.Invoke(null, entry);
        }

        /// <summary>Most recent entries, newest last.</summary>
        public static LaLogEntry[] Recent()
        {
            lock (Gate)
            {
                return Entries.ToArray();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
            }
        }
    }
}
