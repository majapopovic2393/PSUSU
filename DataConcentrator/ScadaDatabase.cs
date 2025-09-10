using System;
using System.Data.SQLite;
using System.IO;

namespace DataConcentrator
{
    public class ScadaDatabase
    {
        private readonly string dbPath = "scada.sqlite";

        public ScadaDatabase()
        {
            if (!File.Exists(dbPath))
                CreateDatabase();
            else
                EnsureTablesExist();
        }

        private void CreateDatabase()
        {
            SQLiteConnection.CreateFile(dbPath);
            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                CreateAllTables(conn);
            }
        }

        private void EnsureTablesExist()
        {
            using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                conn.Open();
                CreateAllTables(conn);
            }
        }

        private void CreateAllTables(SQLiteConnection conn)
        {
            var cmd = conn.CreateCommand();

            // Tagovi
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Tagovi (
                Id TEXT PRIMARY KEY,
                Type INTEGER,
                Description TEXT,
                IOAddress TEXT,
                LowLimit REAL,
                HighLimit REAL,
                Units TEXT,
                ScanTime INTEGER,
                OnOffScan INTEGER,
                InitialValue REAL
            );";
            cmd.ExecuteNonQuery();

            // Alarmi
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Alarmi (
                TagId TEXT,
                Type INTEGER,
                ""Limit"" REAL,
                Message TEXT,
                PRIMARY KEY (TagId, Type, ""Limit"")
            );";
            cmd.ExecuteNonQuery();

            // Aktivirani alarmi
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS AktiviraniAlarmi (
                AlarmId TEXT,
                TagName TEXT,
                Message TEXT,
                Time TEXT
            );";
            cmd.ExecuteNonQuery();
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection($"Data Source={dbPath};Version=3;");
        }
    }
}