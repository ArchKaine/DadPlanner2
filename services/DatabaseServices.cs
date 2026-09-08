using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DadPlanner2.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace DadPlanner2.Services
{
    public class DatabaseService
    {
        private readonly string _dbDir;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private bool _isDirty;

        public DatabaseService()
        {
            _dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIMS");
            _dbPath = Path.Combine(_dbDir, "inventory.db");
            _connectionString = $"Data Source={_dbPath};Pooling=False;";

            QuestPDF.Settings.License = LicenseType.Community;
            InitializeDatabase();
        }

        public void MarkDirty() => _isDirty = true;

        public void ExecuteAutoBackup(string backupDirectory)
        {
            if (!_isDirty) return;

            try
            {
                Directory.CreateDirectory(backupDirectory);
                string backupPath = Path.Combine(
                    backupDirectory,
                    $"inventory-{DateTime.Now:yyyyMMdd-HHmmss-fff}.db");

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
            }
            catch (Exception ex)
            {
                ShowNotification("Backup Error", $"Could not create a database backup: {ex.Message}");
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

        public (string JsonPath, string CsvPath) ExportData(string exportDirectory)
        {
            Directory.CreateDirectory(exportDirectory);
            var logs = GetAllLogs().OrderBy(log => log.Timestamp).ToList();
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string jsonPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.json");
            string csvPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.csv");

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(logs, jsonOptions));

            using var writer = new StreamWriter(csvPath);
            writer.WriteLine("Id,Timestamp,Date,Mode,Volume,HeatFlag,Supplements,ClinicalVol,Concentration,Motility,ProgMotility,Morphology,PhLevel,HasPdf");
            foreach (var log in logs)
            {
                writer.WriteLine(string.Join(",",
                    log.Id,
                    log.Timestamp,
                    CsvEscape(log.DisplayDate),
                    CsvEscape(log.Mode),
                    CsvEscape(log.Volume),
                    log.HeatFlag,
                    CsvEscape(log.Supplements),
                    log.ClinicalVol.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    log.Concentration,
                    log.Motility,
                    log.ProgMotility,
                    log.Morphology,
                    log.PhLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    log.HasPdf));
            }

            return (jsonPath, csvPath);
        }

        private static string CsvEscape(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
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
                catch { }
            }

            if (File.Exists(_dbPath))
            {
                try { File.SetAttributes(_dbPath, FileAttributes.Normal); } catch { }
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
                    try { using var alterCmd = db.CreateCommand(); alterCmd.CommandText = stmt; alterCmd.ExecuteNonQuery(); } catch { }
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
            var allLogs = GetAllLogs();
            long ninetyDaysAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (90L * 24 * 3600);
            var logs = allLogs.Where(l => l.Timestamp >= ninetyDaysAgo).OrderBy(l => l.Timestamp).ToList();

            var releaseLogs = logs.Where(l => l.Volume != "None" && l.Volume != "N/A").ToList();
            
            double avgGap = 0;
            double minGap = 0;

            var gapData = new List<DateTimePoint>();

            if (releaseLogs.Count > 0)
            {
                double totalGap = 0;
                minGap = 999;
                for (int i = 0; i < releaseLogs.Count; i++)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(releaseLogs[i].Timestamp).ToLocalTime().DateTime;
                    double gap = i == 0 ? 72.0 : (releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0;
                    gapData.Add(new DateTimePoint(dt, gap));

                    if (i > 0)
                    {
                        totalGap += gap;
                        if (gap < minGap) minGap = gap;
                    }
                }
                if (releaseLogs.Count > 1) avgGap = totalGap / (releaseLogs.Count - 1);
            }
            
            var lineChart = new SKCartesianChart
            {
                Width = 900, Height = 250,
                Series = new ISeries[] { new LineSeries<DateTimePoint> { Values = gapData, Fill = new SolidColorPaint(new SKColor(0, 122, 204, 50)), Stroke = new SolidColorPaint(new SKColor(0, 122, 204)) { StrokeThickness = 2 }, GeometrySize = 6 } },
                XAxes = new[] { new Axis { Labeler = val => new DateTime((long)val).ToString("MMM dd"), LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Black), NamePaint = new SolidColorPaint(SKColors.Black) } },
                Background = SKColors.White
            };
            
            byte[] lineBytes;
            using (var img = lineChart.GetImage())
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) lineBytes = data.ToArray();

            int maint = logs.Count(l => l.Mode == "Maintenance");
            int play = logs.Count(l => l.Mode == "Playtime");
            int baby = logs.Count(l => l.Mode == "Baby-Making");
            int lab = logs.Count(l => l.Mode == "Clinical-Lab");

            var pieChart = new SKPieChart
            {
                Width = 450, Height = 300,
                Series = new ISeries[] {
                    new PieSeries<int> { Values = new[] { maint }, Name = "Maintenance", Fill = new SolidColorPaint(new SKColor(0, 122, 204)) },
                    new PieSeries<int> { Values = new[] { play }, Name = "Playtime", Fill = new SolidColorPaint(new SKColor(156, 39, 176)) },
                    new PieSeries<int> { Values = new[] { baby }, Name = "Baby-Making", Fill = new SolidColorPaint(new SKColor(76, 175, 80)) },
                    new PieSeries<int> { Values = new[] { lab }, Name = "Clinical", Fill = new SolidColorPaint(new SKColor(84, 110, 122)) }
                },
                Background = SKColors.White,
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                LegendTextPaint = new SolidColorPaint(SKColors.Black)
            };

            byte[] pieBytes;
            using (var img = pieChart.GetImage())
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) pieBytes = data.ToArray();

            int high = logs.Count(l => l.Volume == "High");
            int norm = logs.Count(l => l.Volume == "Normal");
            int low = logs.Count(l => l.Volume == "Low");
            int dry = logs.Count(l => l.Volume == "None" || l.Volume == "N/A");

            var barChart = new SKCartesianChart
            {
                Width = 450, Height = 300,
                Series = new ISeries[] {
                    new ColumnSeries<int> { 
                        Values = new[] { dry, low, norm, high }, 
                        Fill = new SolidColorPaint(new SKColor(0, 122, 204)),
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 12,
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
                        DataLabelsFormatter = p => p.Model > 0 ? p.Model.ToString() : ""
                    }
                },
                XAxes = new[] { new Axis { Labels = new[] { "Dry", "Low", "Normal", "High" }, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
                YAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Black), MinLimit = 0 } },
                Background = SKColors.White
            };

            byte[] barBytes;
            using (var img = barChart.GetImage())
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) barBytes = data.ToArray();

            string pdfPath = Path.Combine(_dbDir, "Baseline_Summary.pdf");
            string pdfFont = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Liberation Sans" : 
                             RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Helvetica" : Fonts.Arial;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(pdfFont));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PIMS BASELINE REPORT").SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);
                            col.Item().Text("Reproductive System Analytics").FontSize(14).FontColor(Colors.Grey.Darken1);
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"Date: {DateTime.Now:MMM dd, yyyy}").SemiBold();
                            col.Item().Text("Cycle: 90-Day Retrospective");
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Total Cycles Recorded").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text(logs.Count.ToString()).FontSize(16).SemiBold(); 
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Mean Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{avgGap:F1} Hrs").FontSize(16).SemiBold();
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Min Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text(minGap == 999 ? "--" : $"{minGap:F1} Hrs").FontSize(16).SemiBold();
                            });
                        });

                        col.Item().PaddingTop(10);

                        if (logs.Count == 0)
                        {
                            col.Item().Text("No records found in the last 90 days.").Italic();
                        }
                        else
                        {
                            col.Item().PaddingBottom(15).Column(c => {
                                c.Item().Text("Recovery Gap Timeline").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                c.Item().Image(lineBytes);
                            });

                            col.Item().PaddingBottom(15).Row(r => {
                                r.RelativeItem().PaddingRight(5).Column(c => {
                                    c.Item().Text("Event Distribution").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                    c.Item().Image(pieBytes);
                                });
                                r.RelativeItem().PaddingLeft(5).Column(c => {
                                    c.Item().Text("Yield Profile").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                    c.Item().Image(barBytes);
                                });
                            });

                            col.Item().PaddingBottom(5).Text("Raw Event Log").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.RelativeColumn(3); 
                                    columns.RelativeColumn(2); 
                                    columns.RelativeColumn(3); 
                                    columns.RelativeColumn(7); 
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Date").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Mode").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Vol").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Supplements").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Lab Results").SemiBold();
                                });

                                bool isAlternate = false;
                                foreach (var log in logs)
                                {
                                    var backgroundColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                    if (log.Mode == "Clinical-Lab") backgroundColor = Colors.Blue.Lighten4;

                                    var date = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime().ToString("MMM dd HH:mm");
                                    
                                    string suppStr = "";
                                    if (log.Supplements.Contains("\"zinc\":1")) suppStr += "💊 ";
                                    if (log.Supplements.Contains("\"maca\":1")) suppStr += "🌿 ";
                                    if (log.Supplements.Contains("\"vitD\":1")) suppStr += "☀️ ";
                                    if (log.Supplements.Contains("\"vitC\":1")) suppStr += "🍊 ";
                                    
                                    string heatStr = log.HeatFlag > 0 ? $"🔥 L{log.HeatFlag}" : "";
                                    string combinedSupps = (heatStr + " " + suppStr).Trim();
                                    if (string.IsNullOrEmpty(combinedSupps)) combinedSupps = "-";

                                    string labStr = "-";

                                    if (log.Concentration > 0 || log.Motility > 0 || log.Morphology > 0)
                                    {
                                        labStr = $"Vol: {log.ClinicalVol:F1}mL | C: {log.Concentration}M | Mot: {log.Motility}% (P:{log.ProgMotility}%) | Mor: {log.Morphology}% | pH: {log.PhLevel:F1}";
                                    }

                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(date).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Mode).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Volume).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(combinedSupps).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(labStr).FontSize(8).SemiBold();
                                    
                                    isAlternate = !isAlternate;
                                }
                            });
                        }
                    });
                    
                    page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("CONFIDENTIAL MEDICAL RECORD").FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text(x => 
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            });

            try 
            {
                document.GeneratePdf(pdfPath);
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
}
