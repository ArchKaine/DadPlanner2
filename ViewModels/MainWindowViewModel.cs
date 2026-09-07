using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DadPlanner2.Models;
using DadPlanner2.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DadPlanner2.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly DatabaseService _dbService;

        // Bridge for chart-to-table scrolling
        public Action<LogRecord>? RequestScrollToLog;
        [ObservableProperty] private LogRecord? _selectedLog;

        // Configuration
        [ObservableProperty] private double _minHours = 24.0;
        [ObservableProperty] private double _maxHours = 72.0;

        // UI Dialog States
        [ObservableProperty] private bool _isHelpOpen;
        [ObservableProperty] private bool _isSettingsOpen;
        [ObservableProperty] private bool _isManualLogOpen;
        [ObservableProperty] private bool _isEditLogOpen;
        [ObservableProperty] private bool _isAlertOpen;
        [ObservableProperty] private bool _isStealthMode;
        
        public double StealthBlurRadius => IsStealthMode ? 12.0 : 0.0;

        // Alert Modal Content
        [ObservableProperty] private string _alertTitle = "";
        [ObservableProperty] private string _alertMessage = "";
        [ObservableProperty] private bool _showAlertConfirm;
        private Action? _alertConfirmAction;

        // Banners
        [ObservableProperty] private bool _isTestModeActive;
        [ObservableProperty] private bool _isShadowActive;
        [ObservableProperty] private string _shadowMessage = "";
        [ObservableProperty] private bool _isBlackoutActive;
        [ObservableProperty] private string _blackoutMessage = "";
        [ObservableProperty] private string _blackoutColor = "#0d2a3a";
        [ObservableProperty] private string _blackoutBorder = "#0277bd";

        // HUD & Appointments
        [ObservableProperty] private string _hudCurrent = "--";
        [ObservableProperty] private string _hudRemaining = "--";
        [ObservableProperty] private string _hudAvg = "--";
        [ObservableProperty] private string _hudMax = "--";
        [ObservableProperty] private DateTime? _appointmentDate;

        // Main Input Form
        [ObservableProperty] private string _selectedMode = "Maintenance";
        [ObservableProperty] private string _selectedVolume = "Normal";
        [ObservableProperty] private int _selectedHeat = 0;
        [ObservableProperty] private bool _zincActive;
        [ObservableProperty] private bool _macaActive;
        [ObservableProperty] private bool _vitDActive;
        [ObservableProperty] private bool _vitCActive;
        
        public bool ShowClinicalFields => SelectedMode == "Clinical-Lab";
        [ObservableProperty] private double? _clinicalVol;
        [ObservableProperty] private int? _concentration;
        [ObservableProperty] private int? _motility;
        [ObservableProperty] private int? _progMotility;
        [ObservableProperty] private int? _morphology;
        [ObservableProperty] private double? _phLevel;

        // Manual Log Form
        [ObservableProperty] private DateTime? _manualDate = DateTime.Now;
        [ObservableProperty] private TimeSpan? _manualTime = DateTime.Now.TimeOfDay;
        [ObservableProperty] private string _manualMode = "Maintenance";
        [ObservableProperty] private string _manualVolume = "Normal";
        [ObservableProperty] private int _manualHeat = 0;
        [ObservableProperty] private bool _manualZinc;
        [ObservableProperty] private bool _manualMaca;
        [ObservableProperty] private bool _manualVitD;
        [ObservableProperty] private bool _manualVitC;
        
        public bool ShowManualClinicalFields => ManualMode == "Clinical-Lab";
        [ObservableProperty] private double? _manualClinicalVol;
        [ObservableProperty] private int? _manualConcentration;
        [ObservableProperty] private int? _manualMotility;
        [ObservableProperty] private int? _manualProgMotility;
        [ObservableProperty] private int? _manualMorphology;
        [ObservableProperty] private double? _manualPhLevel;
        
        [ObservableProperty] private string? _manualLabFileName;
        private byte[]? _manualLabFileData;

        // Edit Form
        [ObservableProperty] private long _editId;
        [ObservableProperty] private DateTime? _editDate;
        [ObservableProperty] private TimeSpan? _editTime;
        [ObservableProperty] private string _editMode = "Maintenance";
        [ObservableProperty] private string _editVolume = "Normal";
        [ObservableProperty] private int _editHeat = 0;
        [ObservableProperty] private bool _editZinc;
        [ObservableProperty] private bool _editMaca;
        [ObservableProperty] private bool _editVitD;
        [ObservableProperty] private bool _editVitC;
        
        public bool ShowEditClinicalFields => EditMode == "Clinical-Lab";
        [ObservableProperty] private double? _editClinicalVol;
        [ObservableProperty] private int? _editConcentration;
        [ObservableProperty] private int? _editMotility;
        [ObservableProperty] private int? _editProgMotility;
        [ObservableProperty] private int? _editMorphology;
        [ObservableProperty] private double? _editPhLevel;
        
        [ObservableProperty] private string? _editLabFileName;
        private byte[]? _editLabFileData;

        // Collections & Charts
        public ObservableCollection<LogRecord> Logs { get; } = new();
        public ObservableCollection<HeatmapDay> HeatmapDays { get; } = new();

        [ObservableProperty] private ISeries[] _chartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _modeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _volumeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _volumeXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _volumeYAxes = Array.Empty<Axis>();

        public MainWindowViewModel()
        {
            _dbService = new DatabaseService();
            IsTestModeActive = _dbService.CheckIfTestModeActive();
            SetupChartAxes();
            LoadData();
        }

        partial void OnSelectedModeChanged(string value) => OnPropertyChanged(nameof(ShowClinicalFields));
        partial void OnManualModeChanged(string value) => OnPropertyChanged(nameof(ShowManualClinicalFields));
        partial void OnEditModeChanged(string value) => OnPropertyChanged(nameof(ShowEditClinicalFields));
        partial void OnIsStealthModeChanged(bool value) => OnPropertyChanged(nameof(StealthBlurRadius));

        // Window & Modal Commands
        [RelayCommand] private void ToggleStealth() => IsStealthMode = !IsStealthMode;
        [RelayCommand] private void OpenHelp() => IsHelpOpen = true;
        [RelayCommand] private void CloseHelp() => IsHelpOpen = false;
        [RelayCommand] private void OpenSettings() => IsSettingsOpen = true;
        [RelayCommand] private void CloseSettings() => IsSettingsOpen = false;
        [RelayCommand] private void OpenManualLog() => IsManualLogOpen = true;
        [RelayCommand] private void CloseManualLog() => IsManualLogOpen = false;
        [RelayCommand] private void CloseEditLog() => IsEditLogOpen = false;
        [RelayCommand] private void CloseAlert() => IsAlertOpen = false;
        [RelayCommand] private void ConfirmAlert() { _alertConfirmAction?.Invoke(); IsAlertOpen = false; }
        [RelayCommand] private void ToggleTestMode() { _dbService.ToggleTestMode(); IsTestModeActive = _dbService.CheckIfTestModeActive(); LoadData(); CloseSettings(); }
        [RelayCommand] private void SaveSettings() { _dbService.SaveThresholdSettings(MinHours, MaxHours); LoadData(); CloseSettings(); }
        [RelayCommand] private void GeneratePdf() => _dbService.Generate90DayReport();
        [RelayCommand] private void OpenPdf(long id) => _dbService.OpenLabReportPdf(id);

        private void ShowAlert(string title, string message, Action? onConfirm = null)
        {
            AlertTitle = title;
            AlertMessage = message;
            _alertConfirmAction = onConfirm;
            ShowAlertConfirm = onConfirm != null;
            IsAlertOpen = true;
        }

        private void LoadData()
        {
            var thresholds = _dbService.GetThresholdSettings();
            MinHours = thresholds.Min;
            MaxHours = thresholds.Max;

            long apptTs = _dbService.GetAppointment();
            AppointmentDate = apptTs > 0 ? DateTimeOffset.FromUnixTimeSeconds(apptTs).ToLocalTime().DateTime : null;

            Logs.Clear();
            var records = _dbService.GetAllLogs();
            foreach (var record in records) Logs.Add(record);
            
            CheckThermalShadow();
            UpdateTelemetry();
            UpdateCharts();
            UpdateHeatmap();
            UpdateBlackoutBanner();
        }

        [RelayCommand]
        private void SetAppointment()
        {
            if (AppointmentDate.HasValue)
            {
                _dbService.SetAppointment(new DateTimeOffset(AppointmentDate.Value).ToUnixTimeSeconds());
                LoadData();
            }
        }

        [RelayCommand]
        private void ClearAppointment()
        {
            _dbService.ClearAppointment();
            AppointmentDate = null;
            LoadData();
        }

        private void UpdateBlackoutBanner()
        {
            long apptTs = _dbService.GetAppointment();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (apptTs > now)
            {
                double secondsUntil = apptTs - now;
                double days = Math.Round(secondsUntil / 86400.0, 1);
                
                if (secondsUntil <= (5 * 86400))
                {
                    BlackoutColor = "#ff9800";
                    BlackoutBorder = "#ff9800";
                    BlackoutMessage = $"CLINICAL BLACKOUT ACTIVE: Target T-Minus {days} Days";
                }
                else
                {
                    BlackoutColor = "#0d2a3a";
                    BlackoutBorder = "#0277bd";
                    BlackoutMessage = $"📅 Clinical Baseline Scheduled in {days} Days (Blackout begins at T-Minus 5 Days)";
                }
                IsBlackoutActive = true;
            }
            else
            {
                IsBlackoutActive = false;
            }
        }

        private bool GetSupplementSaturation(long targetTs, string suppKey, int daysBack)
        {
            long start = targetTs - (daysBack * 24 * 3600);
            var windowLogs = Logs.Where(l => l.Timestamp >= start && l.Timestamp <= targetTs).ToList();
            if (windowLogs.Count == 0) return false;
            
            int activeCount = windowLogs.Count(l => l.Supplements.Contains($"\"{suppKey}\":1"));
            return ((double)activeCount / windowLogs.Count) >= 0.5;
        }

        private string EstimateBabyMakingVolume(long timestamp)
        {
            var priorRelease = Logs.Where(l => l.Timestamp < timestamp && l.Volume != "None" && l.Volume != "N/A")
                                   .OrderByDescending(l => l.Timestamp).FirstOrDefault();
                                   
            double gapHours = MaxHours;
            if (priorRelease != null) gapHours = (timestamp - priorRelease.Timestamp) / 3600.0;
            
            string estVol = "Normal";
            if (gapHours < MinHours) estVol = "Low";
            else if (gapHours >= MaxHours) estVol = "High";
            
            if (GetSupplementSaturation(timestamp, "zinc", 21))
            {
                if (estVol == "Low") estVol = "Normal";
                else if (estVol == "Normal") estVol = "High";
            }
            return estVol;
        }

        [RelayCommand]
        private void LogEvent(string mode)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (mode == "Clinical-Lab" && IsShadowActive)
            {
                ShowAlert("Warning", "A Thermal Shadow is active, which compromises clinical accuracy. Are you sure you want to log a baseline test?", () => ExecuteLogEvent(mode, now));
                return;
            }
            ExecuteLogEvent(mode, now);
        }

        private void ExecuteLogEvent(string mode, long timestamp)
        {
            SelectedMode = mode;
            int z = ZincActive ? 1 : 0, m = MacaActive ? 1 : 0, d = VitDActive ? 1 : 0, c = VitCActive ? 1 : 0;
            string vol = SelectedVolume;
            
            if (mode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (mode == "Baby-Making") vol = EstimateBabyMakingVolume(timestamp);

            var newLog = new LogRecord {
                Timestamp = timestamp, 
                Mode = mode,
                Volume = vol, 
                HeatFlag = SelectedHeat,
                Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c}}}",
                ClinicalVol = ClinicalVol ?? 0.0, 
                Concentration = Concentration ?? 0,
                Motility = Motility ?? 0, 
                ProgMotility = ProgMotility ?? 0,
                Morphology = Morphology ?? 0, 
                PhLevel = PhLevel ?? 0.0
            };
            
            _dbService.InsertLog(newLog);
            
            ZincActive = MacaActive = VitDActive = VitCActive = false;
            SelectedHeat = 0; 
            SelectedVolume = "Normal";
            ClinicalVol = null; Concentration = null; Motility = null; 
            ProgMotility = null; Morphology = null; PhLevel = null;
            
            LoadData();
        }

        [RelayCommand]
        private void SubmitManualLog()
        {
            if (!ManualDate.HasValue || !ManualTime.HasValue) return;
            
            DateTime dt = ManualDate.Value.Date + ManualTime.Value;
            long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();
            
            int z = ManualZinc ? 1 : 0, m = ManualMaca ? 1 : 0, d = ManualVitD ? 1 : 0, c = ManualVitC ? 1 : 0;
            string vol = ManualVolume;
            
            if (ManualMode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (ManualMode == "Baby-Making") vol = EstimateBabyMakingVolume(ts);

            var newLog = new LogRecord {
                Timestamp = ts, 
                Mode = ManualMode,
                Volume = vol, 
                HeatFlag = ManualHeat,
                Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c}}}",
                ClinicalVol = ManualClinicalVol ?? 0.0, 
                Concentration = ManualConcentration ?? 0,
                Motility = ManualMotility ?? 0, 
                ProgMotility = ManualProgMotility ?? 0,
                Morphology = ManualMorphology ?? 0, 
                PhLevel = ManualPhLevel ?? 0.0
            };
            
            _dbService.InsertLog(newLog, ManualLabFileName, _manualLabFileData);
            
            ManualLabFileName = null;
            _manualLabFileData = null;
            
            CloseManualLog();
            LoadData();
        }

        [RelayCommand]
        private void OpenEditLog(long id)
        {
            var log = Logs.FirstOrDefault(l => l.Id == id);
            if (log == null) return;
            
            EditId = log.Id;
            var dtOffset = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime();
            EditDate = dtOffset.Date;
            EditTime = dtOffset.TimeOfDay;
            
            EditMode = log.Mode;
            EditVolume = log.Volume;
            EditHeat = log.HeatFlag;
            
            EditZinc = log.Supplements.Contains("\"zinc\":1");
            EditMaca = log.Supplements.Contains("\"maca\":1");
            EditVitD = log.Supplements.Contains("\"vitD\":1");
            EditVitC = log.Supplements.Contains("\"vitC\":1");
            
            EditClinicalVol = log.ClinicalVol > 0 ? log.ClinicalVol : null;
            EditConcentration = log.Concentration > 0 ? log.Concentration : null;
            EditMotility = log.Motility > 0 ? log.Motility : null;
            EditProgMotility = log.ProgMotility > 0 ? log.ProgMotility : null;
            EditMorphology = log.Morphology > 0 ? log.Morphology : null;
            EditPhLevel = log.PhLevel > 0 ? log.PhLevel : null;
            
            EditLabFileName = null;
            _editLabFileData = null;
            
            IsEditLogOpen = true;
        }

        [RelayCommand]
        private void SaveEditLog()
        {
            if (!EditDate.HasValue || !EditTime.HasValue) return;
            
            DateTime dt = EditDate.Value.Date + EditTime.Value;
            long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();
            
            int z = EditZinc ? 1 : 0, m = EditMaca ? 1 : 0, d = EditVitD ? 1 : 0, c = EditVitC ? 1 : 0;
            string vol = EditVolume;
            
            if (EditMode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (EditMode == "Baby-Making") vol = EstimateBabyMakingVolume(ts);

            var updatedLog = new LogRecord {
                Id = EditId,
                Timestamp = ts, 
                Mode = EditMode,
                Volume = vol, 
                HeatFlag = EditHeat,
                Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c}}}",
                ClinicalVol = EditClinicalVol ?? 0.0, 
                Concentration = EditConcentration ?? 0,
                Motility = EditMotility ?? 0, 
                ProgMotility = EditProgMotility ?? 0,
                Morphology = EditMorphology ?? 0, 
                PhLevel = EditPhLevel ?? 0.0
            };
            
            _dbService.UpdateLog(updatedLog, EditLabFileName, _editLabFileData);
            CloseEditLog();
            LoadData();
        }

        [RelayCommand] 
        private void DeleteLog(long id) 
        { 
            ShowAlert("Confirm Delete", "Are you sure you want to permanently purge this record?", () => {
                _dbService.DeleteLog(id); 
                LoadData();
            });
        }

        [RelayCommand]
        private async Task SelectManualPdf(Window window)
        {
            var file = await OpenPdfPicker(window);
            if (file != null)
            {
                ManualLabFileName = file.Value.Name;
                _manualLabFileData = file.Value.Data;
            }
        }

        [RelayCommand]
        private async Task SelectEditPdf(Window window)
        {
            var file = await OpenPdfPicker(window);
            if (file != null)
            {
                EditLabFileName = file.Value.Name;
                _editLabFileData = file.Value.Data;
            }
        }

        private async Task<(string Name, byte[] Data)?> OpenPdfPicker(Window window)
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Clinical Lab Report",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("PDF Documents") { Patterns = new[] { "*.pdf" } } }
            });

            if (files.Count > 0)
            {
                using var stream = await files[0].OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return (files[0].Name, ms.ToArray());
            }
            return null;
        }

        [RelayCommand]
        private void AutoCalibrate()
        {
            if (Logs.Count < 2)
            {
                ShowAlert("Error", "Not enough data. Keep logging!");
                return;
            }

            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            var uncompromisedLogs = new List<LogRecord>();
            
            foreach (var l in releaseLogs)
            {
                long shadowWindow = l.Timestamp - (74 * 24 * 3600);
                bool compromised = Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < l.Timestamp && x.HeatFlag >= 2);
                if (!compromised) uncompromisedLogs.Add(l);
            }

            if (uncompromisedLogs.Count < 2)
            {
                ShowAlert("Error", "Not enough uncompromised release data points to calibrate.");
                return;
            }

            var validGaps = new List<double>();
            var allGaps = new List<double>();

            for (int i = 0; i < uncompromisedLogs.Count - 1; i++)
            {
                double gap = (uncompromisedLogs[i + 1].Timestamp - uncompromisedLogs[i].Timestamp) / 3600.0;
                allGaps.Add(gap);
                
                if ((uncompromisedLogs[i + 1].Mode == "Maintenance" || uncompromisedLogs[i + 1].Mode == "Clinical-Lab") && 
                    (uncompromisedLogs[i + 1].Volume == "Normal" || uncompromisedLogs[i + 1].Volume == "High"))
                {
                    validGaps.Add(gap);
                }
            }

            if (validGaps.Count == 0)
            {
                ShowAlert("Error", "No successful uncompromised recoveries recorded yet.");
                return;
            }

            double roundedMin = Math.Round(validGaps.Average(), 1);
            double meanAll = allGaps.Average();
            double sumSquares = allGaps.Sum(val => Math.Pow(val - meanAll, 2));
            double stdDev = Math.Sqrt(sumSquares / allGaps.Count);
            
            double recommendedMax = Math.Round(meanAll + stdDev, 1);
            if (recommendedMax > 120.0) recommendedMax = 120.0;

            string msg = $"Calibration Complete.\n(Thermal Shadow events excluded)\n\n[FLOOR] Minimum recovery gap: {roundedMin} hours.\n[CEILING] Max endurance: {recommendedMax} hours.\n\nUpdate your threshold settings?";
            
            ShowAlert("Auto-Calibrate", msg, () => {
                MinHours = roundedMin;
                MaxHours = recommendedMax;
                SaveSettings();
            });
        }

        [RelayCommand]
        private void AnalyzeSupplements()
        {
            if (Logs.Count < 10)
            {
                ShowAlert("Error", "Need more data points to run statistical analysis. Keep logging!");
                return;
            }

            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            var validLogs = new List<LogRecord>();

            foreach (var l in releaseLogs)
            {
                long shadowWindow = l.Timestamp - (74 * 24 * 3600);
                bool compromised = Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < l.Timestamp && x.HeatFlag >= 2);
                if (!compromised) validLogs.Add(l);
            }

            if (validLogs.Count < 5)
            {
                ShowAlert("Error", "Insufficient uncompromised data points after filtering out Thermal Shadow periods.");
                return;
            }

            int zaTot = 0, zaSuc = 0, ziTot = 0, ziSuc = 0;
            int dTot = 0, dSuc = 0, diTot = 0, diSuc = 0;
            var mGaps = new List<double>(); var nmGaps = new List<double>();
            var cGaps = new List<double>(); var ncGaps = new List<double>();

            for (int i = 1; i < validLogs.Count; i++)
            {
                var cur = validLogs[i];
                var prev = validLogs[i - 1];
                double gap = (cur.Timestamp - prev.Timestamp) / 3600.0;

                bool zSat = GetSupplementSaturation(cur.Timestamp, "zinc", 21);
                bool mSat = GetSupplementSaturation(cur.Timestamp, "maca", 21);
                bool dSat = GetSupplementSaturation(cur.Timestamp, "vitD", 30);
                bool cSat = GetSupplementSaturation(cur.Timestamp, "vitC", 30);

                bool isSuccess = (cur.Volume == "Normal" || cur.Volume == "High");

                if (cur.Mode == "Maintenance" || cur.Mode == "Clinical-Lab")
                {
                    if (zSat) { zaTot++; if (isSuccess) zaSuc++; }
                    else { ziTot++; if (isSuccess) ziSuc++; }

                    if (dSat) { dTot++; if (isSuccess) dSuc++; }
                    else { diTot++; if (isSuccess) diSuc++; }
                }

                if (mSat) mGaps.Add(gap); else nmGaps.Add(gap);
                if (cSat) cGaps.Add(gap); else ncGaps.Add(gap);
            }

            string msg = "💊 BIOLOGICAL SATURATION ANALYSIS 🌿☀️🍊\n\n(Measuring efficacy based on cumulative buildup windows)\n(Thermal Shadow periods removed to prevent data corruption)\n\n";

            if (zaTot > 0 && ziTot > 0) {
                int zaPct = (int)Math.Round((zaSuc / (double)zaTot) * 100);
                int ziPct = (int)Math.Round((ziSuc / (double)ziTot) * 100);
                int diff = zaPct - ziPct;
                msg += $"[ZINC - YIELD RATE (21-Day Window)]\nUn-Saturated: {ziPct}% | Saturated: {zaPct}%\nResult: {(diff > 0 ? $"+{diff}% optimal yield probability." : "No measurable yield impact.")}\n\n";
            } else { msg += "[ZINC] Insufficient baseline data.\n\n"; }

            if (mGaps.Count > 0 && nmGaps.Count > 0) {
                double mAvg = mGaps.Average(); double nmAvg = nmGaps.Average(); double diff = nmAvg - mAvg;
                msg += $"[MACA ROOT - RECOVERY SPEED (21-Day Window)]\nUn-Saturated: {nmAvg:F1}h | Saturated: {mAvg:F1}h\nResult: {(diff > 0 ? $"Recovery accelerated by {diff:F1} hours." : "No measurable speed impact.")}\n\n";
            } else { msg += "[MACA] Insufficient baseline data.\n\n"; }

            if (dTot > 0 && diTot > 0) {
                int dPct = (int)Math.Round((dSuc / (double)dTot) * 100);
                int diPct = (int)Math.Round((diSuc / (double)diTot) * 100);
                int diff = dPct - diPct;
                msg += $"[VITAMIN D3 - YIELD RATE (30-Day Window)]\nUn-Saturated: {diPct}% | Saturated: {dPct}%\nResult: {(diff > 0 ? $"+{diff}% optimal yield probability." : "No measurable yield impact.")}\n\n";
            } else { msg += "[VITAMIN D3] Insufficient baseline data.\n\n"; }

            if (cGaps.Count > 0 && ncGaps.Count > 0) {
                double cAvg = cGaps.Average(); double ncAvg = ncGaps.Average(); double diff = ncAvg - cAvg;
                msg += $"[VITAMIN C - RECOVERY SPEED (30-Day Window)]\nUn-Saturated: {ncAvg:F1}h | Saturated: {cAvg:F1}h\nResult: {(diff > 0 ? $"Recovery accelerated by {diff:F1} hours." : "No measurable speed impact.")}\n";
            } else { msg += "[VITAMIN C] Insufficient baseline data.\n"; }

            ShowAlert("Analysis Complete", msg);
            CloseSettings();
        }

        private void CheckThermalShadow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long shadowWindow = now - (74 * 24 * 3600); 
            var severeEvents = Logs.Where(l => l.Timestamp >= shadowWindow && l.HeatFlag >= 2).OrderByDescending(l => l.Timestamp).ToList();
            
            if (severeEvents.Any())
            {
                var latestHeat = severeEvents.First();
                
                var subsequentLabs = Logs.Where(l => l.Mode == "Clinical-Lab" && l.Timestamp > latestHeat.Timestamp && l.Timestamp <= now).ToList();
                foreach(var lab in subsequentLabs)
                {
                    if (lab.Concentration >= 15 && lab.Motility >= 40)
                    {
                        IsShadowActive = false;
                        return;
                    }
                }

                var clearsAt = DateTimeOffset.FromUnixTimeSeconds(latestHeat.Timestamp + (74 * 24 * 3600)).ToLocalTime();
                IsShadowActive = true;
                ShadowMessage = $"⚠️ SYSTEM COMPROMISED: Level {latestHeat.HeatFlag} Thermal Shadow active. Clears: {clearsAt:MMM dd, yyyy}.";
            }
            else
            {
                IsShadowActive = false;
            }
        }

        private void UpdateTelemetry()
        {
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").ToList();
            if (!releaseLogs.Any()) return;

            double currentDelta = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - releaseLogs.First().Timestamp) / 3600.0;
            HudCurrent = $"{currentDelta:F1}h";

            double remaining = MaxHours - currentDelta;
            HudRemaining = remaining > 0 ? $"{remaining:F1}h" : "OVERDUE";

            if (releaseLogs.Count < 2) return;
            double totalGap = 0, maxGap = 0;
            for (int i = 0; i < releaseLogs.Count - 1; i++)
            {
                double gap = (releaseLogs[i].Timestamp - releaseLogs[i + 1].Timestamp) / 3600.0;
                totalGap += gap;
                if (gap > maxGap) maxGap = gap;
            }
            HudAvg = $"{(totalGap / (releaseLogs.Count - 1)):F1}h";
            HudMax = $"{maxGap:F1}h";
        }

        private void UpdateHeatmap()
        {
            HeatmapDays.Clear();
            var today = DateTime.Today;
            var logsByDate = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A")
                                 .GroupBy(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date)
                                 .ToDictionary(g => g.Key, g => g.ToList());

            var oldestDay = today.AddDays(-364);
            int padding = (int)oldestDay.DayOfWeek;
            for (int i = 0; i < padding; i++) HeatmapDays.Add(new HeatmapDay { Level = 0 });

            for (int i = 364; i >= 0; i--)
            {
                var targetDate = today.AddDays(-i);
                int level = 0;
                string tooltip = $"{targetDate:MMM dd}: No yield activity";

                if (logsByDate.TryGetValue(targetDate, out var dayLogs))
                {
                    if (dayLogs.Any(l => l.Volume == "High")) level = 3;
                    else if (dayLogs.Any(l => l.Volume == "Normal")) level = 2;
                    else if (dayLogs.Any(l => l.Volume == "Low")) level = 1;

                    string volLabel = level == 3 ? "High" : level == 2 ? "Normal" : "Low";
                    tooltip = $"{targetDate:MMM dd}: {dayLogs.Count} event(s) | Max Yield: {volLabel}";
                }
                HeatmapDays.Add(new HeatmapDay { DateStr = targetDate.ToShortDateString(), Level = level, Tooltip = tooltip });
            }
        }

        private void UpdateCharts()
        {
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            var gapData = new System.Collections.Generic.List<double>();
            for (int i = 0; i < releaseLogs.Count; i++) gapData.Add(i == 0 ? MaxHours : (releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0);

            var lineSeries = new LineSeries<double> { 
                Name = "", 
                Values = gapData, 
                Fill = new SolidColorPaint(new SKColor(85, 85, 85, 90)), 
                Stroke = new SolidColorPaint(new SKColor(85, 85, 85)) { StrokeThickness = 2 }, 
                GeometrySize = 6,
                YToolTipLabelFormatter = (point) => 
                {
                    var log = releaseLogs[point.Index];
                    string date = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime().ToString("MMM dd HH:mm");
                    
                    long shadowWindow = log.Timestamp - (74 * 24 * 3600);
                    string shadowText = Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < log.Timestamp && x.HeatFlag >= 2) 
                        ? " | 🔥 Shadow Active" : "";

                    var flags = new System.Collections.Generic.List<string>();
                    if (log.HeatFlag > 0) flags.Add($"⚠️ Temp L{log.HeatFlag}");
                    if (log.Supplements.Contains("\"zinc\":1")) flags.Add("💊 Zinc");
                    if (log.Supplements.Contains("\"maca\":1")) flags.Add("🌿 Maca");
                    if (log.Supplements.Contains("\"vitD\":1")) flags.Add("☀️ D3");
                    if (log.Supplements.Contains("\"vitC\":1")) flags.Add("🍊 C");
                    
                    string metrics = "";
                    if (log.Mode == "Clinical-Lab" && (log.ClinicalVol > 0 || log.Concentration > 0))
                        metrics = $"\n\nLab Metrics:\nVol: {log.ClinicalVol}mL | Conc: {log.Concentration}M\nTot Mot: {log.Motility}% | Prog Mot: {log.ProgMotility}%\nMorph: {log.Morphology}% | pH: {log.PhLevel}";

                    string flagStr = flags.Count > 0 ? $"\n\nFlags: {string.Join(" | ", flags)}" : "";
                    
                    return $"{date}{shadowText}\nGap: {point.Coordinate.PrimaryValue} hrs\nMode: {log.Mode}\nVolume: {log.Volume}{flagStr}{metrics}";
                }
            };

            ChartSeries = new ISeries[] { lineSeries };

            int maint = Logs.Count(l => l.Mode == "Maintenance"), play = Logs.Count(l => l.Mode == "Playtime"), baby = Logs.Count(l => l.Mode == "Baby-Making"), lab = Logs.Count(l => l.Mode == "Clinical-Lab");
            ModeSeries = new ISeries[] {
                new PieSeries<int> { Values = new[] { maint }, Name = "Maintenance", Fill = new SolidColorPaint(new SKColor(0, 122, 204)) },
                new PieSeries<int> { Values = new[] { play }, Name = "Playtime", Fill = new SolidColorPaint(new SKColor(156, 39, 176)) },
                new PieSeries<int> { Values = new[] { baby }, Name = "Baby-Making", Fill = new SolidColorPaint(new SKColor(76, 175, 80)) },
                new PieSeries<int> { Values = new[] { lab }, Name = "Clinical", Fill = new SolidColorPaint(new SKColor(84, 110, 122)) }
            };

            int high = Logs.Count(l => l.Volume == "High"), norm = Logs.Count(l => l.Volume == "Normal"), low = Logs.Count(l => l.Volume == "Low"), dry = Logs.Count(l => l.Volume == "None" || l.Volume == "N/A");
            VolumeSeries = new ISeries[] { new RowSeries<int> { Values = new[] { high, norm, low, dry }, Fill = new SolidColorPaint(new SKColor(0, 122, 204)) } };
            VolumeYAxes = new[] { new Axis { Labels = new[] { "High", "Norm", "Low", "Dry" }, LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
        }

        [RelayCommand]
        private void ChartClicked(object obj)
        {
            LiveChartsCore.Kernel.ChartPoint? point = null;

            if (obj is LiveChartsCore.Kernel.ChartPoint singlePoint)
                point = singlePoint;
            else if (obj is System.Collections.Generic.IEnumerable<LiveChartsCore.Kernel.ChartPoint> points)
                point = System.Linq.Enumerable.FirstOrDefault(points);

            if (point == null) return;
            
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            if (point.Index >= 0 && point.Index < releaseLogs.Count)
            {
                var log = releaseLogs[point.Index];
                SelectedLog = log;
                RequestScrollToLog?.Invoke(log);
            }
        }

        private void SetupChartAxes()
        {
            XAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
            YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
            VolumeXAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
        }
    }
}
