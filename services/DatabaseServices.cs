using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using DadPlanner2.Models;

namespace DadPlanner2.Services
{
    public class DatabaseService
    {
        private readonly string _dbDir;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly DataExportService _dataExport = new();
        private readonly SupplementAnalysisService _supplementAnalysis = new();
        private readonly SupplementSaturationService _supplementSaturation = new();
        private readonly ReportDataService _reportData = new();
        private readonly ReportDocumentService _reportDocument = new();
        private bool _isDirty;

        public DatabaseService(string? dataDirectory = null)
        {
            _dbDir = dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIMS");
            _dbPath = Path.Combine(_dbDir, "inventory.db");
            _connectionString = $"Data Source={_dbPath};Pooling=False;";

            InitializeDatabase();
        }

        public void MarkDirty() => _isDirty = true;

        public bool ExecuteAutoBackup(string backupDirectory)
        {
            if (!_isDirty) return false;

            try
            {
                Directory.CreateDirectory(backupDirectory);
                string backupPath = Path.Combine(
                    backupDirectory,
                    $"inventory-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db");

                using var db = new SqliteConnection(_connectionString);
                db.Open();

                using var backupCmd = db.CreateCommand();
                backupCmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}'";
                backupCmd.ExecuteNonQuery();

                var backups = new DirectoryInfo(backupDirectory)
                    .GetFiles("inventory-*.db")
                    .OrderByDescending(file => file.CreationTimeUtc)
                    .ToList();

                foreach (var oldBackup in backups.Skip(10))
                    oldBackup.Delete();

                _isDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                ShowNotification("Backup Error", $"Could not create a database backup: {ex.Message}");
                return false;
            }
        }

        public string? GetLatestBackup(string backupDirectory)
        {
            if (!Directory.Exists(backupDirectory)) return null;

            return new DirectoryInfo(backupDirectory)
                .GetFiles("inventory-*.db")
                .OrderByDescending(file => file.CreationTimeUtc)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }

        public void RestoreBackup(string backupPath)
        {
            if (!File.Exists(backupPath))
                throw new FileNotFoundException("The selected backup file was not found.", backupPath);

            using (var validationDb = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly;"))
            {
                validationDb.Open();
                using var validationCommand = validationDb.CreateCommand();
                validationCommand.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(validationCommand.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The selected backup failed SQLite integrity validation.");
            }

            string safetyBackupPath = $"{_dbPath}.pre-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db";
            if (File.Exists(_dbPath))
            {
                using var currentDb = new SqliteConnection(_connectionString);
                currentDb.Open();
                using var safetyBackupCommand = currentDb.CreateCommand();
                safetyBackupCommand.CommandText = $"VACUUM INTO '{safetyBackupPath.Replace("'", "''")}'";
                safetyBackupCommand.ExecuteNonQuery();
            }

            string temporaryPath = $"{_dbPath}.restore-{Guid.NewGuid():N}";
            File.Copy(backupPath, temporaryPath);

            try
            {
                File.Move(temporaryPath, _dbPath, true);
                _isDirty = false;
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }

        public DataExportResult ExportData(string exportDirectory)
        {
            var logs = GetAllLogs();
            var analysis = _supplementAnalysis.Analyze(
                logs,
                (timestamp, supplement, days) =>
                    _supplementSaturation.Calculate(logs, timestamp, supplement, days));
            return _dataExport.Export(logs, exportDirectory, analysis);
        }

        private void InitializeDatabase()
        {
            Directory.CreateDirectory(_dbDir);

            string legacyDb = "inventory.db";
            string altLegacyDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");

            if (!File.Exists(_dbPath))
            {
                try
                {
                    if (File.Exists(legacyDb)) File.Move(legacyDb, _dbPath);
                    else if (File.Exists(altLegacyDb)) File.Copy(altLegacyDb, _dbPath, true);
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException("The legacy database could not be moved into the application data directory.", ex);
                }
            }

            if (File.Exists(_dbPath))
            {
                try
                {
                    File.SetAttributes(_dbPath, FileAttributes.Normal);
                }
                catch (Exception ex)
                {
                    ShowNotification("Database Access Warning", $"Could not normalize database file attributes: {ex.Message}");
                }
            }

            try
            {
                using var db = new SqliteConnection(_connectionString);
                db.Open();

                using var walCmd = db.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode=WAL;";
                walCmd.ExecuteNonQuery();

                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                    CREATE TABLE IF NOT EXISTS Appointments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                    CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);
                    INSERT OR IGNORE INTO Settings (Key, Value) VALUES ('min_threshold', '24'), ('max_threshold', '72');
                ";
                cmd.ExecuteNonQuery();

                string[] alterStatements = {
                    "ALTER TABLE Logs ADD COLUMN Mode TEXT DEFAULT 'Maintenance'",
                    "ALTER TABLE Logs ADD COLUMN Volume TEXT DEFAULT 'Normal'",
                    "ALTER TABLE Logs ADD COLUMN HeatFlag INTEGER DEFAULT 0",
                    "ALTER TABLE Logs ADD COLUMN ZincFlag INTEGER DEFAULT 0",
                    "ALTER TABLE Logs ADD COLUMN MacaFlag INTEGER DEFAULT 0",
                    "ALTER TABLE Logs ADD COLUMN Concentration INTEGER",
                    "ALTER TABLE Logs ADD COLUMN Motility INTEGER",
                    "ALTER TABLE Logs ADD COLUMN Morphology INTEGER",
                    "ALTER TABLE Logs ADD COLUMN LabReportBlob BLOB",
                    "ALTER TABLE Logs ADD COLUMN LabReportFileName TEXT",
                    "ALTER TABLE Logs ADD COLUMN ClinicalVol REAL",
                    "ALTER TABLE Logs ADD COLUMN ProgMotility INTEGER",
                    "ALTER TABLE Logs ADD COLUMN PhLevel REAL",
                    "ALTER TABLE Logs ADD COLUMN Supplements TEXT DEFAULT '{}'"
                };

                foreach (var stmt in alterStatements)
                {
                    try
                    {
                        using var alterCmd = db.CreateCommand();
                        alterCmd.CommandText = stmt;
                        alterCmd.ExecuteNonQuery();
                    }
                    catch (SqliteException ex) when (DatabaseMigrationPolicy.IsAlreadyApplied(ex))
                    {
                        // SQLite has no portable ADD COLUMN IF NOT EXISTS syntax.
                    }
                }
            }

            catch (Exception ex)
            {
                ShowNotification("Init Error", ex.Message);
            }
        }

        public List<LogRecord> GetAllLogs()
        {
            var logs = new List<LogRecord>();
            using var db = new SqliteConnection(_connectionString);
            db.Open();

            using var cmdLogs = db.CreateCommand();
            cmdLogs.CommandText = @"
                SELECT Id, Timestamp, IFNULL(Mode, 'Maintenance'), IFNULL(Volume, 'Normal'), 
                IFNULL(HeatFlag, 0), IFNULL(Supplements, '{}'), IFNULL(Concentration, 0), 
                IFNULL(Motility, 0), IFNULL(Morphology, 0), LabReportFileName, 
                IFNULL(ClinicalVol, 0.0), IFNULL(ProgMotility, 0), IFNULL(PhLevel, 0.0) 
                FROM Logs ORDER BY Timestamp DESC";

            using var reader = cmdLogs.ExecuteReader();
            while (reader.Read())
            {
                logs.Add(new LogRecord
                {
                    Id = reader.GetInt64(0),
                    Timestamp = reader.GetInt64(1),
                    Mode = reader.GetString(2),
                    Volume = reader.GetString(3),
                    HeatFlag = Convert.ToInt32(reader.GetValue(4)),
                    Supplements = reader.GetString(5),
                    Concentration = Convert.ToInt32(reader.GetValue(6)),
                    Motility = Convert.ToInt32(reader.GetValue(7)),
                    Morphology = Convert.ToInt32(reader.GetValue(8)),
                    HasPdf = !reader.IsDBNull(9),
                    ClinicalVol = Convert.ToDouble(reader.GetValue(10)),
                    ProgMotility = Convert.ToInt32(reader.GetValue(11)),
                    PhLevel = Convert.ToDouble(reader.GetValue(12))
                });
            }
            return logs;
        }

        public void InsertLog(LogRecord log, string? fileName = null, byte[]? pdfBlob = null)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();

            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, Supplements, Concentration, Motility, Morphology, ClinicalVol, ProgMotility, PhLevel, LabReportFileName, LabReportBlob) 
                VALUES ($ts, $mode, $vol, $heat, $supps, $conc, $mot, $morph, $cvol, $pmot, $ph, $fname, $blob)";

            BindLogParameters(cmd, log, fileName, pdfBlob);
            cmd.ExecuteNonQuery();
        }

        public void UpdateLog(LogRecord log, string? fileName = null, byte[]? pdfBlob = null)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();

            using var cmd = db.CreateCommand();
            string updateBlob = pdfBlob != null ? ", LabReportFileName = $fname, LabReportBlob = $blob" : "";
            cmd.CommandText = $@"
                UPDATE Logs SET 
                    Timestamp = $ts, Mode = $mode, Volume = $vol, 
                    HeatFlag = $heat, Supplements = $supps, 
                    ClinicalVol = $cvol, Concentration = $conc, Motility = $mot, 
                    ProgMotility = $pmot, Morphology = $morph, PhLevel = $ph 
                    {updateBlob}
                WHERE Id = $id";

            BindLogParameters(cmd, log, fileName, pdfBlob);
            cmd.Parameters.AddWithValue("$id", log.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteLog(long id)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM Logs WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        private void BindLogParameters(SqliteCommand cmd, LogRecord log, string? fileName, byte[]? pdfBlob)
        {
            cmd.Parameters.AddWithValue("$ts", log.Timestamp);
            cmd.Parameters.AddWithValue("$mode", log.Mode);
            cmd.Parameters.AddWithValue("$vol", log.Volume);
            cmd.Parameters.AddWithValue("$heat", log.HeatFlag);
            cmd.Parameters.AddWithValue("$supps", log.Supplements);
            cmd.Parameters.AddWithValue("$conc", log.Concentration);
            cmd.Parameters.AddWithValue("$mot", log.Motility);
            cmd.Parameters.AddWithValue("$morph", log.Morphology);
            cmd.Parameters.AddWithValue("$cvol", log.ClinicalVol);
            cmd.Parameters.AddWithValue("$pmot", log.ProgMotility);
            cmd.Parameters.AddWithValue("$ph", log.PhLevel);
            cmd.Parameters.AddWithValue("$fname", fileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$blob", pdfBlob ?? (object)DBNull.Value);
        }

        public (double Min, double Max) GetThresholdSettings()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            return (GetSetting(db, "min_threshold", 24.0), GetSetting(db, "max_threshold", 72.0));
        }

        public void SaveThresholdSettings(double min, double max)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('min_threshold', $min), ('max_threshold', $max)";
            cmd.Parameters.AddWithValue("$min", min.ToString());
            cmd.Parameters.AddWithValue("$max", max.ToString());
            cmd.ExecuteNonQuery();
        }

        public void SaveSupplementsState(bool zn, bool ma, bool vitD, bool vitC)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('supp_zn', $zn), ('supp_ma', $ma), ('supp_vitD', $vitD), ('supp_vitC', $vitC)";
            cmd.Parameters.AddWithValue("$zn", zn.ToString());
            cmd.Parameters.AddWithValue("$ma", ma.ToString());
            cmd.Parameters.AddWithValue("$vitD", vitD.ToString());
            cmd.Parameters.AddWithValue("$vitC", vitC.ToString());
            cmd.ExecuteNonQuery();
        }

        public (bool zn, bool ma, bool vitD, bool vitC) GetSupplementsState()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            return (
                GetSettingStr(db, "supp_zn") == "True",
                GetSettingStr(db, "supp_ma") == "True",
                GetSettingStr(db, "supp_vitD") == "True",
                GetSettingStr(db, "supp_vitC") == "True"
            );
        }

        private string GetSettingStr(SqliteConnection db, string key)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? "False";
        }

        public long GetAppointment()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt64(result) : 0;
        }

        public void SetAppointment(long timestamp)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO Appointments (Timestamp) VALUES ($ts)";
            cmd.Parameters.AddWithValue("$ts", timestamp);
            cmd.ExecuteNonQuery();
        }

        public void ClearAppointment()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM Appointments";
            cmd.ExecuteNonQuery();
        }

        private double GetSetting(SqliteConnection db, string key, double defaultValue)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            var result = cmd.ExecuteScalar();
            if (result != null && double.TryParse(result.ToString(), out double val)) return val;
            return defaultValue;
        }

        public bool CheckIfTestModeActive()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Logs_Backup'";
            return cmd.ExecuteScalar() != null;
        }

        public void ToggleTestMode()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            
            if (CheckIfTestModeActive())
            {
                using var restoreCmd = db.CreateCommand();
                restoreCmd.CommandText = @"
                    DROP TABLE IF EXISTS Logs;
                    DROP TABLE IF EXISTS Settings;
                    DROP TABLE IF EXISTS Appointments;
                    ALTER TABLE Logs_Backup RENAME TO Logs;
                    ALTER TABLE Settings_Backup RENAME TO Settings;
                    ALTER TABLE Appointments_Backup RENAME TO Appointments;
                ";
                restoreCmd.ExecuteNonQuery();
            }
            else
            {
                using var backupCmd = db.CreateCommand();
                backupCmd.CommandText = @"
                    ALTER TABLE Logs RENAME TO Logs_Backup;
                    ALTER TABLE Settings RENAME TO Settings_Backup;
                    ALTER TABLE Appointments RENAME TO Appointments_Backup;
                    
                    CREATE TABLE Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER, Mode TEXT DEFAULT 'Maintenance', Volume TEXT DEFAULT 'Normal', HeatFlag INTEGER DEFAULT 0, Supplements TEXT DEFAULT '{}', Concentration INTEGER, Motility INTEGER, Morphology INTEGER, ClinicalVol REAL, ProgMotility INTEGER, PhLevel REAL, LabReportBlob BLOB, LabReportFileName TEXT);
                    
                    CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
                    INSERT INTO Settings SELECT * FROM Settings_Backup;
                    
                    CREATE TABLE Appointments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                ";
                backupCmd.ExecuteNonQuery();

                GenerateFakeData(db);
            }
        }

        private void GenerateFakeData(SqliteConnection db)
        {
            long currentTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var rand = new Random();
            string[] modes = { "Maintenance", "Playtime", "Baby-Making", "Clinical-Lab" };
            
            int testRecordCount = 150;
            int singleHeatEventIndex = rand.Next(0, testRecordCount);

            for (int i = 0; i < testRecordCount; i++)
            {
                int randomZinc = (i < 75) ? 1 : 0;
                int randomMaca = (i < 75) ? 1 : 0;
                int randomVitD = (i < 75) ? 1 : 0;
                int randomVitC = (i < 75) ? 1 : 0;
                
                string fakeSupps = $"{{\"zinc\":{randomZinc},\"maca\":{randomMaca},\"vitD\":{randomVitD},\"vitC\":{randomVitC}}}";
                
                int gapHours = rand.Next(35, 80);
                if (randomMaca == 1) gapHours -= rand.Next(10, 20); 
                
                currentTs -= (gapHours * 3600);
                
                string randomMode = modes[rand.Next(modes.Length)];
                if (rand.Next(100) > 5 && randomMode == "Clinical-Lab") randomMode = "Maintenance";

                string randomVol = "Normal";
                if (randomZinc == 1) 
                {
                    randomVol = rand.Next(100) > 15 ? "High" : "Normal"; 
                }
                else 
                {
                    int spread = rand.Next(100);
                    randomVol = spread < 40 ? "Low" : (spread < 80 ? "Normal" : "High");
                }
                
                if (rand.Next(100) > 90 && randomMode != "Baby-Making") randomVol = "None";
                
                int randomHeat = (i == singleHeatEventIndex) ? 1 : 0; 

                int conc = 0, mot = 0, morph = 0, pmot = 0;
                double cvol = 0.0, ph = 0.0;

                if (randomMode == "Clinical-Lab")
                {
                    cvol = rand.NextDouble() * 3 + 1.5; 
                    conc = rand.Next(15, 120);
                    mot = rand.Next(40, 85);
                    pmot = mot - rand.Next(5, 15);
                    morph = rand.Next(2, 8);
                    ph = rand.NextDouble() * 0.8 + 7.2;
                    randomVol = "High"; 
                }
                
                using var insertCmd = db.CreateCommand();
                insertCmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, Supplements, Concentration, Motility, Morphology, ClinicalVol, ProgMotility, PhLevel) VALUES ($ts, $mode, $vol, $heat, $supps, $conc, $mot, $morph, $cvol, $pmot, $ph)";
                insertCmd.Parameters.AddWithValue("$ts", currentTs);
                insertCmd.Parameters.AddWithValue("$mode", randomMode);
                insertCmd.Parameters.AddWithValue("$vol", randomVol);
                insertCmd.Parameters.AddWithValue("$heat", randomHeat);
                insertCmd.Parameters.AddWithValue("$supps", fakeSupps);
                insertCmd.Parameters.AddWithValue("$conc", conc);
                insertCmd.Parameters.AddWithValue("$mot", mot);
                insertCmd.Parameters.AddWithValue("$morph", morph);
                insertCmd.Parameters.AddWithValue("$cvol", cvol);
                insertCmd.Parameters.AddWithValue("$pmot", pmot);
                insertCmd.Parameters.AddWithValue("$ph", ph);
                insertCmd.ExecuteNonQuery();
            }
        }

        public void OpenLabReportPdf(long logId)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT LabReportBlob, LabReportFileName FROM Logs WHERE Id = $id AND LabReportBlob IS NOT NULL";
            cmd.Parameters.AddWithValue("$id", logId);
            using var reader = cmd.ExecuteReader();
            
            if (reader.Read())
            {
                byte[] pdfBytes = (byte[])reader["LabReportBlob"];
                string fileName = reader["LabReportFileName"].ToString() ?? $"LabReport_{logId}.pdf";
                string tempPath = Path.Combine(Path.GetTempPath(), fileName);
                File.WriteAllBytes(tempPath, pdfBytes);
                OpenFileCrossPlatform(tempPath);
            }
        }

        public void Generate90DayReport()
        {
            var logs = GetAllLogs();
            var reportData = _reportData.Create(logs, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var supplementAnalysis = _supplementAnalysis.Analyze(
                logs,
                (timestamp, supplement, days) =>
                    _supplementSaturation.Calculate(logs, timestamp, supplement, days));
            string pdfPath = Path.Combine(_dbDir, "Baseline_Summary.pdf");

            try
            {
                _reportDocument.Generate(reportData, pdfPath, supplementAnalysis);
                OpenFileCrossPlatform(pdfPath);
            }
            catch (IOException)
            {
                ShowNotification("File In Use", "Please close the currently open Baseline_Summary.pdf before generating a new one.");
            }
            catch (Exception ex)
            {
                ShowNotification("PDF Generation Error", ex.Message);
            }
        }

        private void OpenFileCrossPlatform(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", path);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", path);
            }
            catch (Exception ex)
            {
                ShowNotification("File Error", $"Could not open file automatically: {ex.Message}");
            }
        }

        public static void ShowNotification(string title, string message)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string psCommand = $"Add-Type -AssemblyName System.Windows.Forms; $n = New-Object System.Windows.Forms.NotifyIcon; $n.Icon = [System.Drawing.SystemIcons]::Information; $n.Visible = $true; $n.ShowBalloonTip(5000, '{title}', '{message}', 'Info'); Start-Sleep -Seconds 5; $n.Dispose()";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-WindowStyle Hidden -Command \"{psCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e 'display notification \"{message}\" with title \"{title}\"'",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notify-send",
                        Arguments = $"\"{title}\" \"{message}\"",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch { }
        }

    }

    internal static class DatabaseMigrationPolicy
    {
        public static bool IsAlreadyApplied(SqliteException exception)
        {
            return exception.SqliteErrorCode == 1 &&
                   exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
        }
    }
}
