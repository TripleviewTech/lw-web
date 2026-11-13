using System;
using Microsoft.Data.SqlClient;

namespace LwCadSystemUX.Services.Data
{
    /// <summary>
    /// One connection and one transaction for a write that spans tables - a
    /// dispatch is DSE1, STK1 and DST1; an assignment is RST1, DSU1, DST1 and
    /// STK1. The legacy apps wrote each table on its own connection and guarded
    /// the rows with lock ids. Here the rows are held with UPDLOCK for the life of
    /// the transaction, and either every table changes or none does.
    ///
    /// Disposing without Commit rolls back.
    /// </summary>
    public sealed class CadTransaction : IDisposable
    {
        private bool _completed;

        public CadTransaction(string connectString)
        {
            if (string.IsNullOrWhiteSpace(connectString))
            {
                throw new InvalidOperationException("The ConnectString property has not been initialized.");
            }

            Connection = new SqlConnection(connectString);
            Connection.Open();
            Transaction = Connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        }

        public SqlConnection Connection { get; }

        public SqlTransaction Transaction { get; }

        /// <summary>A scalar read inside the transaction, for keys and counters.</summary>
        public object Scalar(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            var value = command.ExecuteScalar();
            return value == DBNull.Value ? null : value;
        }

        /// <summary>A statement inside the transaction; returns the rows affected.</summary>
        public int Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            return command.ExecuteNonQuery();
        }

        private SqlCommand Command(string sql, (string Name, object Value)[] parameters)
        {
            var command = new SqlCommand(sql, Connection, Transaction);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value is char c ? c.ToString() : value ?? DBNull.Value);
            }

            return command;
        }

        public void Commit()
        {
            Transaction.Commit();
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                try
                {
                    Transaction.Rollback();
                }
                catch (Exception ex)
                {
                    LaLog.Error("CadTransaction", "Rollback failed.", ex);
                }
            }

            Transaction.Dispose();
            Connection.Dispose();
        }
    }
}
