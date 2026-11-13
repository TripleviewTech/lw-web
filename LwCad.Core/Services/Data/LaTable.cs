using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using LaConfig;
using Microsoft.Data.SqlClient;

namespace LwCadSystemUX.Services.Data
{
    /// <summary>
    /// .NET 8 replacement for the generated LaTables / LwTables readers.
    ///
    /// The contract is the one the legacy <c>*_RW</c> classes expose - GetRow,
    /// GetRowVerify, InsertRow, UpdateRow, WriteRow, DeleteRow, Initialize, plus
    /// the RecordStatus / ErrorMsg pair - so code ported from the old apps reads
    /// the same. The difference is that the SQL is built once from the record's
    /// fields instead of being emitted column by column into a thousand-line
    /// file per table, and values are read with typed accessors rather than
    /// round-tripped through <c>reader[i].ToString()</c>.
    ///
    /// RecordStatus follows the legacy convention: 0 = row present, 1 = the
    /// statement failed, 9 = no row.
    /// </summary>
    public abstract class LaTable<TRecord> where TRecord : class, new()
    {
        private static readonly FieldInfo[] Columns =
            typeof(TRecord).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static readonly string ColumnList =
            string.Join(", ", Columns.Select(f => f.Name));

        protected LaTable() : this(null)
        {
        }

        protected LaTable(string connectString)
        {
            if (!string.IsNullOrEmpty(connectString))
            {
                LaConnectString = connectString;
                return;
            }

            try
            {
                LaConfig = new LaGetConfig();
                LaConnectString = LaConfig.GetConnectString();
            }
            catch (Exception ex)
            {
                ErrorMsg = ex.Message;
                LaLog.Error(TableName, "LaGetConfig error.", ex);
            }
        }

        /// <summary>Unqualified table name, e.g. "CCC1".</summary>
        protected abstract string TableName { get; }

        /// <summary>Columns of the primary key, in the order the generated reader used.</summary>
        protected abstract string[] KeyColumns { get; }

        /// <summary>
        /// An IDENTITY column the database fills on insert - STK1_S_KEY, DST1_T_KEY.
        /// The generated readers left it out of the INSERT and read it back with
        /// SCOPE_IDENTITY(); InsertRow does the same when this is set.
        /// </summary>
        protected virtual string IdentityColumn => null;

        public string LaConnectString { get; set; } = string.Empty;

        public LaGetConfig LaConfig { get; set; }

        public short RecordStatus { get; set; }

        public string ErrorMsg { get; set; } = string.Empty;

        // ------------------------------------------------------------- reads

        /// <summary>Loads the row matching the key fields already set on <paramref name="record"/>.</summary>
        public TRecord GetRow(TRecord record)
        {
            RecordStatus = 9;
            ErrorMsg = "Record not found.";

            var sql = "SELECT " + ColumnList + " FROM dbo." + TableName + " WHERE " + KeyPredicate();

            try
            {
                using var connection = Open();
                using var command = new SqlCommand(sql, connection);
                BindKey(command, record);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Fill(record, reader);
                    RecordStatus = 0;
                    ErrorMsg = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Fail("Get Row Error.", ex);
            }

            return record;
        }

        /// <summary>True when a row with that key exists, without loading it.</summary>
        public bool GetRowVerify(TRecord record)
        {
            RecordStatus = 9;
            ErrorMsg = "Record not found.";

            var sql = "SELECT 1 FROM dbo." + TableName + " WHERE " + KeyPredicate();

            try
            {
                using var connection = Open();
                using var command = new SqlCommand(sql, connection);
                BindKey(command, record);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    RecordStatus = 0;
                    ErrorMsg = string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Fail("Get Row Verify Error.", ex);
            }

            return false;
        }

        /// <summary>
        /// Reads many rows. <paramref name="where"/> is the text after WHERE and may
        /// use @parameters supplied in <paramref name="parameters"/>; pass null for all rows.
        /// The monitors need this - the generated readers had no list call and the
        /// screens hand-wrote the SQL instead.
        /// </summary>
        public List<TRecord> GetList(string where = null, object parameters = null,
                                     string orderBy = null, int top = 0)
        {
            RecordStatus = 9;
            ErrorMsg = "Record not found.";

            var sql = "SELECT " + (top > 0 ? "TOP (" + top + ") " : string.Empty) +
                      ColumnList + " FROM dbo." + TableName + " (NOLOCK)";
            if (!string.IsNullOrWhiteSpace(where)) sql += " WHERE " + where;
            if (!string.IsNullOrWhiteSpace(orderBy)) sql += " ORDER BY " + orderBy;

            var rows = new List<TRecord>();

            try
            {
                using var connection = Open();
                using var command = new SqlCommand(sql, connection);
                BindAnonymous(command, parameters);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var record = new TRecord();
                    Fill(record, reader);
                    rows.Add(record);
                }

                if (rows.Count > 0)
                {
                    RecordStatus = 0;
                    ErrorMsg = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Fail("Get List Error.", ex);
            }

            return rows;
        }

        // ------------------------------------------------------------ writes

        public void InsertRow(TRecord record)
        {
            if (IdentityColumn != null)
            {
                try
                {
                    using var connection = Open();
                    InsertRow(record, connection, null);
                }
                catch (Exception ex)
                {
                    Fail("Insert Row Error.", ex);
                }

                return;
            }

            Execute(InsertSql(), record, "Insert Row Error.");
        }

        public void UpdateRow(TRecord record)
        {
            Execute(UpdateSql(), record, "Update Row Error.");
        }

        /// <summary>
        /// Update that reports "no row" instead of succeeding silently. The legacy
        /// WriteRow relied on UpdateRowTry throwing to decide whether to insert.
        /// </summary>
        public void UpdateRowTry(TRecord record)
        {
            UpdateRow(record);
            if (RecordStatus == 0) return;

            throw new InvalidOperationException(
                string.IsNullOrEmpty(ErrorMsg) ? "Update affected no rows." : ErrorMsg);
        }

        /// <summary>Update if the row is there, insert if it is not.</summary>
        public void WriteRow(TRecord record)
        {
            UpdateRow(record);
            if (RecordStatus == 0) return;

            InsertRow(record);
        }

        public void DeleteRow(TRecord record)
        {
            var sql = "DELETE FROM dbo." + TableName + " WHERE " + KeyPredicate();
            Execute(sql, record, "Delete Row Error.", keyOnly: true);
        }

        /// <summary>Resets every field to the legacy empty value: ' ', 0, "" or null.</summary>
        public TRecord Initialize(TRecord record)
        {
            record ??= new TRecord();

            foreach (var field in Columns)
            {
                field.SetValue(record, EmptyValue(field.FieldType));
            }

            return record;
        }

        // -------------------------------------------------- inside a transaction
        //
        // The calls above each open their own connection and commit on their own,
        // which is how the generated readers worked. A dispatch or an assignment
        // writes several tables that must land together, so these overloads run on
        // a connection and transaction the caller owns (see CadTransaction). They
        // throw on failure instead of only setting RecordStatus, so the caller's
        // transaction rolls back rather than committing half an event.

        /// <summary>
        /// Reads the row and holds an update lock on it until the transaction ends -
        /// the SQL equivalent of the legacy lock-id handshake on RST1 and STK1.
        /// </summary>
        public TRecord GetRowForUpdate(TRecord record, CadTransaction tx)
        {
            RecordStatus = 9;
            ErrorMsg = "Record not found.";

            var sql = "SELECT " + ColumnList + " FROM dbo." + TableName +
                      " WITH (UPDLOCK, ROWLOCK) WHERE " + KeyPredicate();

            using var command = new SqlCommand(sql, tx.Connection, tx.Transaction);
            BindKey(command, record);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                Fill(record, reader);
                RecordStatus = 0;
                ErrorMsg = string.Empty;
            }

            return record;
        }

        /// <summary>Many rows, read inside the caller's transaction.</summary>
        public List<TRecord> GetList(string where, object parameters, CadTransaction tx, bool forUpdate = false)
        {
            var sql = "SELECT " + ColumnList + " FROM dbo." + TableName +
                      (forUpdate ? " WITH (UPDLOCK, ROWLOCK)" : string.Empty);
            if (!string.IsNullOrWhiteSpace(where)) sql += " WHERE " + where;

            var rows = new List<TRecord>();

            using var command = new SqlCommand(sql, tx.Connection, tx.Transaction);
            BindAnonymous(command, parameters);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var record = new TRecord();
                Fill(record, reader);
                rows.Add(record);
            }

            RecordStatus = (short)(rows.Count > 0 ? 0 : 9);
            ErrorMsg = rows.Count > 0 ? string.Empty : "Record not found.";
            return rows;
        }

        /// <summary>
        /// Inserts inside the caller's transaction. When the table has an identity
        /// column the new value is read back into the record.
        /// </summary>
        public void InsertRow(TRecord record, CadTransaction tx) =>
            InsertRow(record, tx.Connection, tx.Transaction);

        private void InsertRow(TRecord record, SqlConnection connection, SqlTransaction transaction)
        {
            RecordStatus = 1;

            FitToColumns(record, connection, transaction);

            using var command = new SqlCommand(InsertSql(), connection, transaction);
            BindAll(command, record, skipIdentity: true);

            if (IdentityColumn == null)
            {
                command.ExecuteNonQuery();
            }
            else
            {
                var identity = command.ExecuteScalar();
                var field = Field(IdentityColumn);
                var target = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                field.SetValue(record, Convert.ChangeType(identity, target));
            }

            RecordStatus = 0;
            ErrorMsg = string.Empty;
        }

        /// <summary>Full-row update inside the caller's transaction. False when no row matched.</summary>
        public bool UpdateRow(TRecord record, CadTransaction tx)
        {
            FitToColumns(record, tx.Connection, tx.Transaction);

            using var command = new SqlCommand(UpdateSql(), tx.Connection, tx.Transaction);
            BindAll(command, record);

            var found = command.ExecuteNonQuery() > 0;
            RecordStatus = (short)(found ? 0 : 9);
            ErrorMsg = found ? string.Empty : "Record not found.";
            return found;
        }

        public bool DeleteRow(TRecord record, CadTransaction tx)
        {
            var sql = "DELETE FROM dbo." + TableName + " WHERE " + KeyPredicate();

            using var command = new SqlCommand(sql, tx.Connection, tx.Transaction);
            BindKey(command, record);

            var found = command.ExecuteNonQuery() > 0;
            RecordStatus = (short)(found ? 0 : 9);
            ErrorMsg = found ? string.Empty : "Record not found.";
            return found;
        }

        // ----------------------------------------------------------- plumbing

        // Character widths per column, read once per table from the database.
        // The generated readers relied on fixed-width COBOL records; the LW table
        // definitions carry no lengths at all, and several columns added since
        // (STK1_UNITS, DSE1_PC_NAME) are not in the copybooks either. Truncating
        // to the real width keeps a long location or unit list from failing the
        // whole transaction with "string or binary data would be truncated".
        private static Dictionary<string, int> _widths;
        private static readonly object WidthLock = new object();

        private void FitToColumns(TRecord record, SqlConnection connection, SqlTransaction transaction)
        {
            var widths = _widths;
            if (widths == null)
            {
                lock (WidthLock)
                {
                    widths = _widths ?? (_widths = ReadWidths(connection, transaction));
                }
            }

            foreach (var field in Columns)
            {
                if (field.FieldType != typeof(string)) continue;
                if (!widths.TryGetValue(field.Name, out var width) || width <= 0) continue;

                if (field.GetValue(record) is string text && text.Length > width)
                {
                    field.SetValue(record, text.Substring(0, width));
                }
            }
        }

        private Dictionary<string, int> ReadWidths(SqlConnection connection, SqlTransaction transaction)
        {
            var widths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            const string sql =
                "SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @table AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL";

            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@table", TableName);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // -1 is varchar(max) / text, which needs no trimming.
                widths[reader.GetString(0)] = Convert.ToInt32(reader.GetValue(1));
            }

            return widths;
        }

        private string InsertSql()
        {
            var columns = Columns.Where(f => !IsIdentity(f.Name)).ToArray();

            var sql = "INSERT INTO dbo." + TableName +
                      " (" + string.Join(", ", columns.Select(f => f.Name)) + ") VALUES (" +
                      string.Join(", ", columns.Select(f => "@" + f.Name)) + ")";

            return IdentityColumn == null ? sql : sql + "; SELECT SCOPE_IDENTITY()";
        }

        private string UpdateSql()
        {
            var assignments = Columns
                .Where(f => !IsKey(f.Name) && !IsIdentity(f.Name))
                .Select(f => f.Name + " = @" + f.Name);

            return "UPDATE dbo." + TableName +
                   " SET " + string.Join(", ", assignments) +
                   " WHERE " + KeyPredicate();
        }

        private bool IsIdentity(string column) =>
            IdentityColumn != null && string.Equals(IdentityColumn, column, StringComparison.OrdinalIgnoreCase);

        private static FieldInfo Field(string column) =>
            Columns.First(f => string.Equals(f.Name, column, StringComparison.OrdinalIgnoreCase));

        private SqlConnection Open()
        {
            if (string.IsNullOrWhiteSpace(LaConnectString))
            {
                throw new InvalidOperationException("The ConnectString property has not been initialized.");
            }

            var connection = new SqlConnection(LaConnectString);
            connection.Open();
            return connection;
        }

        private void Execute(string sql, TRecord record, string context, bool keyOnly = false)
        {
            RecordStatus = 1;

            try
            {
                using var connection = Open();
                using var command = new SqlCommand(sql, connection);

                if (keyOnly) BindKey(command, record);
                else BindAll(command, record);

                var affected = command.ExecuteNonQuery();
                if (affected > 0)
                {
                    RecordStatus = 0;
                    ErrorMsg = string.Empty;
                }
                else
                {
                    RecordStatus = 9;
                    ErrorMsg = "Record not found.";
                }
            }
            catch (Exception ex)
            {
                Fail(context, ex);
            }
        }

        private void Fail(string context, Exception ex)
        {
            RecordStatus = 1;
            ErrorMsg = ex.Message;
            LaLog.Error(TableName, context, ex);
        }

        private string KeyPredicate() =>
            string.Join(" AND ", KeyColumns.Select(c => c + " = @" + c));

        private bool IsKey(string column) =>
            KeyColumns.Any(k => string.Equals(k, column, StringComparison.OrdinalIgnoreCase));

        private void BindKey(SqlCommand command, TRecord record)
        {
            foreach (var column in KeyColumns)
            {
                var field = Columns.First(f =>
                    string.Equals(f.Name, column, StringComparison.OrdinalIgnoreCase));
                AddParameter(command, field.Name, field.GetValue(record));
            }
        }

        private void BindAll(SqlCommand command, TRecord record, bool skipIdentity = false)
        {
            foreach (var field in Columns)
            {
                if (skipIdentity && IsIdentity(field.Name)) continue;
                AddParameter(command, field.Name, field.GetValue(record));
            }
        }

        private static void BindAnonymous(SqlCommand command, object parameters)
        {
            if (parameters == null) return;

            foreach (var property in parameters.GetType()
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                AddParameter(command, property.Name, property.GetValue(parameters));
            }
        }

        private static void AddParameter(SqlCommand command, string name, object value)
        {
            // char maps to a one-character string; the CAD tables store these as char(1).
            if (value is char c) value = c.ToString();

            command.Parameters.AddWithValue("@" + name, value ?? DBNull.Value);
        }

        private static void Fill(TRecord record, IDataRecord reader)
        {
            for (int i = 0; i < Columns.Length; i++)
            {
                Columns[i].SetValue(record, ReadValue(reader, i, Columns[i].FieldType));
            }
        }

        /// <summary>
        /// Reads one column. A NULL produces the same empty value the generated
        /// FieldMover helpers produced, so callers keep seeing ' ', 0 and "" rather
        /// than having to null-check every field.
        /// </summary>
        private static object ReadValue(IDataRecord reader, int ordinal, Type type)
        {
            if (reader.IsDBNull(ordinal)) return EmptyValue(type);

            if (type == typeof(string)) return reader.GetValue(ordinal).ToString();

            if (type == typeof(char))
            {
                var text = reader.GetValue(ordinal).ToString();
                return string.IsNullOrEmpty(text) ? ' ' : text[0];
            }

            if (type == typeof(DateTime?) || type == typeof(DateTime))
                return reader.GetDateTime(ordinal);

            if (type == typeof(byte[])) return (byte[])reader.GetValue(ordinal);

            // Numerics: go through Convert so a smallint column read into an int
            // field (or the reverse) behaves the way the old TryParse path did.
            var raw = reader.GetValue(ordinal);
            var target = Nullable.GetUnderlyingType(type) ?? type;
            return Convert.ChangeType(raw, target);
        }

        private static object EmptyValue(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            if (type == typeof(char)) return ' ';
            if (type == typeof(byte[])) return null;
            if (Nullable.GetUnderlyingType(type) != null) return null;
            return Activator.CreateInstance(type);
        }
    }
}
