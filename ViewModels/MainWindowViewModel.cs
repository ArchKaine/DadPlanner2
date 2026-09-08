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
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace DadPlanner2.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly DatabaseService _dbService;

        public Action<LogRecord>? RequestScrollToLog;
        [ObservableProperty] private LogRecord? _selectedLog;

        [ObservableProperty] private double _minHours = 24.0;
        [ObservableProperty] private double _maxHours = 72.0;

        private double _chartMinX;
        private double _chartMaxX;

        // Custom Overlay Tooltip Properties for ALL Charts
        [ObservableProperty] private bool _isTooltipVisible;
        [ObservableProperty] private double _tooltipX;
        [ObservableProperty] private double _tooltipY;
        
        [ObservableProperty] private ChartLogPoint? _hoveredTimelinePoint;
        [ObservableProperty] private PieHoverData? _hoveredPie;
        [ObservableProperty] private BarHoverData? _hoveredBar;

        [ObservableProperty] private bool _isHelpOpen;
        [ObservableProperty] private bool _isSettingsOpen;
        [ObservableProperty] private bool _isManualLogOpen;
        [ObservableProperty] private bool _isEditLogOpen;
        [ObservableProperty] private bool _isAlertOpen;
        [ObservableProperty] private bool _isStealthMode;
        
        public double StealthBlurRadius => IsStealthMode ? 12.0 : 0.0;

        [ObservableProperty] private string _alertTitle = "";
        [ObservableProperty] private string _alertMessage = "";
        [ObservableProperty] private bool _showAlertConfirm;
        private Action? _alertConfirmAction;

        [ObservableProperty] private bool _isTestModeActive;
        [ObservableProperty] private bool _isShadowActive;
        [ObservableProperty] private string _shadowMessage = "";
        [ObservableProperty] private bool _isBlackoutActive;
        [ObservableProperty] private string _blackoutMessage = "";
        [ObservableProperty] private string _blackoutColor = "#0d2a3a";
        [ObservableProperty] private string _blackoutBorder = "#0277bd";

        [ObservableProperty] private string _hudCurrent = "--";
        [ObservableProperty] private string _hudRemaining = "--";
        [ObservableProperty] private string _hudAvg = "--";
        [ObservableProperty] private string _hudMax = "--";
        [ObservableProperty] private string _hudFrequency = "--";
        
        [ObservableProperty] private DateTime? _appointmentDate;

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

        public ObservableCollection<LogRecord> Logs { get; } = new();
        public ObservableCollection<HeatmapDay> HeatmapDays { get; } = new();

        [ObservableProperty] private ISeries[] _chartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
        
        [ObservableProperty] private ISeries[] _modeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _volumeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _volumeXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _volumeYAxes = Array.Empty<Axis>();
        
        [ObservableProperty] private int _totalEventCount;
        [ObservableProperty] private string _legendMaint = "";
        [ObservableProperty] private string _legendPlay = "";
        [ObservableProperty] private string _legendBaby = "";
        [ObservableProperty] private string _legendLab = "";

        public MainWindowViewModel()
        {
            _dbService = new DatabaseService();
            IsTestModeActive = _dbService.CheckIfTestModeActive();
            SetupChartAxes();
            
            var thresholds = _dbService.GetThresholdSettings();
            MinHours = thresholds.Min;
            MaxHours = thresholds.Max;

            long appt = _dbService.GetAppointment();
            if (appt > 0) AppointmentDate = DateTimeOffset.FromUnixTimeSeconds(appt).ToLocalTime().DateTime;

            var savedSupps = _dbService.GetSupplementsState();
            ZincActive = savedSupps.zn;
            MacaActive = savedSupps.ma;
            VitDActive = savedSupps.vitD;
            VitCActive = savedSupps.vitC;
            
            LoadData();
        }

        partial void OnSelectedModeChanged(string value) => OnPropertyChanged(nameof(ShowClinicalFields));
        partial void OnManualModeChanged(string value) => OnPropertyChanged(nameof(ShowManualClinicalFields));
        partial void OnEditModeChanged(string value) => OnPropertyChanged(nameof(ShowEditClinicalFields));
        partial void OnIsStealthModeChanged(bool value) => OnPropertyChanged(nameof(StealthBlurRadius));

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

        [RelayCommand]
        private void ZoomChart(string range)
        {
            if (XAxes.Length == 0) return;
            var axis = XAxes[0];
            
            if (range == "ALL")
            {
                axis.MinLimit = _chartMinX;
                axis.MaxLimit = _chartMaxX;
            }
            else if (int.TryParse(range, out int days))
            {
                axis.MaxLimit = _chartMaxX;
                axis.MinLimit = _chartMaxX - (days * 86400.0);
            }
        }

        // --- CONTROL-BOUNDED QUADRANT MATH ---
        private void PositionTooltip(double winX, double winY, double ctrlX, double ctrlY, double ctrlW, double ctrlH, double estW, double estH)
        {
            double relX = winX - ctrlX;
            double relY = winY - ctrlY;

            // 1. Quadrant Logic: Push the tooltip inward toward the center of the control
            double targetX = (relX > (ctrlW / 2.0)) ? (winX - estW - 20) : (winX + 20);
            double targetY = (relY > (ctrlH / 2.0)) ? (winY - estH - 20) : (winY + 20);

            // 2. Hard Clamping: Absolute guarantee it cannot exceed the control's physical layout box
            if (targetX < ctrlX) targetX = ctrlX;
            if (targetX + estW > ctrlX + ctrlW) targetX = ctrlX + ctrlW - estW;

            if (targetY < ctrlY) targetY = ctrlY;
            if (targetY + estH > ctrlY + ctrlH) targetY = ctrlY + ctrlH - estH;

            TooltipX = targetX;
            TooltipY = targetY;
        }
        
        public void ProcessTimelineHover(double chartX, double winX, double winY, double plotX, double plotW, double ctrlX, double ctrlY, double ctrlW, double ctrlH)
        {
            if (ChartSeries == null || ChartSeries.Length == 0 || XAxes == null || XAxes.Length == 0 || plotW <= 0)
            {
                IsTooltipVisible = false; return;
            }

            double adjustedX = chartX - plotX;
            if (adjustedX < 0 || adjustedX > plotW) { IsTooltipVisible = false; return; }

            var axis = XAxes[0];
            double minTs = axis.MinLimit ?? _chartMinX;
            double maxTs = axis.MaxLimit ?? _chartMaxX;
            double ratio = adjustedX / plotW;
            double targetTs = minTs + (ratio * (maxTs - minTs));

            var allPoints = new List<ChartLogPoint>();
            foreach (var series in ChartSeries)
            {
                if (series.Values is IEnumerable<ChartLogPoint> pts) allPoints.AddRange(pts);
            }

            if (allPoints.Count == 0) { IsTooltipVisible = false; return; }

            var closest = allPoints.OrderBy(p => Math.Abs((p.X ?? 0) - targetTs)).FirstOrDefault();

            if (closest != null && Math.Abs((closest.X ?? 0) - targetTs) < (3 * 86400))
            {
                HoveredTimelinePoint = closest; HoveredPie = null; HoveredBar = null;
                PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 280, 210); // Generous estimates for timeline
                IsTooltipVisible = true;
            }
            else IsTooltipVisible = false;
        }

        public void ProcessPieHover(double chartX, double chartY, double winX, double winY, double plotX, double plotY, double plotW, double plotH, double ctrlX, double ctrlY, double ctrlW, double ctrlH)
        {
            if (plotW <= 0 || plotH <= 0) { IsTooltipVisible = false; return; }

            double adjustedX = chartX - plotX;
            double adjustedY = chartY - plotY;
            double cx = plotW / 2; double cy = plotH / 2;
            double dx = adjustedX - cx; double dy = adjustedY - cy;
            double distSq = dx * dx + dy * dy;
            
            double outerR = Math.Min(plotW, plotH) / 2;
            if (distSq < 3600 || distSq > (outerR * outerR)) { IsTooltipVisible = false; return; }

            double angle = Math.Atan2(dy, dx) * 180 / Math.PI; if (angle < 0) angle += 360; 

            int maint = Logs.Count(l => l.Mode == "Maintenance");
            int play = Logs.Count(l => l.Mode == "Playtime");
            int baby = Logs.Count(l => l.Mode == "Baby-Making");
            int lab = Logs.Count(l => l.Mode == "Clinical-Lab");
            int total = maint + play + baby + lab;
            if (total == 0) return;

            double a1 = (maint / (double)total) * 360; double a2 = a1 + (play / (double)total) * 360; double a3 = a2 + (baby / (double)total) * 360;
            string category = ""; int count = 0; string hex = "";
            
            if (angle <= a1) { category = "Maintenance"; count = maint; hex = "#007acc"; }
            else if (angle <= a2) { category = "Playtime"; count = play; hex = "#9c27b0"; }
            else if (angle <= a3) { category = "Baby-Making"; count = baby; hex = "#4caf50"; }
            else { category = "Clinical-Lab"; count = lab; hex = "#546e7a"; }

            if (count == 0) { IsTooltipVisible = false; return; }

            HoveredPie = new PieHoverData { Category = category, Count = count, ColorHex = hex, Percentage = Math.Round((count/(double)total)*100, 1) };
            HoveredTimelinePoint = null; HoveredBar = null;
            PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 240, 140);
            IsTooltipVisible = true;
        }

        public void ProcessBarHover(double chartY, double winX, double winY, double plotY, double plotH, double ctrlX, double ctrlY, double ctrlW, double ctrlH)
        {
            if (plotH <= 0) { IsTooltipVisible = false; return; }

            double adjustedY = chartY - plotY;
            if (adjustedY < 0 || adjustedY > plotH) { IsTooltipVisible = false; return; }

            double rowH = plotH / 4.0;
            int visualIdx = (int)(adjustedY / rowH);
            if (visualIdx < 0 || visualIdx > 3) { IsTooltipVisible = false; return; }

            int volHigh = Logs.Count(l => l.Volume == "High");
            int volNormal = Logs.Count(l => l.Volume == "Normal");
            int volLow = Logs.Count(l => l.Volume == "Low");
            int volDry = Logs.Count(l => l.Volume == "None" || l.Volume == "N/A");

            string category = ""; int count = 0; string hex = "";
            
            if (visualIdx == 0) { category = "High Yield"; count = volHigh; hex = "#4caf50"; }
            else if (visualIdx == 1) { category = "Normal Yield"; count = volNormal; hex = "#007acc"; }
            else if (visualIdx == 2) { category = "Low Yield"; count = volLow; hex = "#ff9800"; }
            else if (visualIdx == 3) { category = "Dry / None"; count = volDry; hex = "#757575"; }

            HoveredBar = new BarHoverData { Category = category, Count = count, ColorHex = hex };
            HoveredTimelinePoint = null; HoveredPie = null;
            PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 240, 110);
            IsTooltipVisible = true;
        }

        private void ShowAlert(string title, string message, Action? onConfirm = null)
        {
            AlertTitle = title; AlertMessage = message;
            _alertConfirmAction = onConfirm; ShowAlertConfirm = onConfirm != null;
            IsAlertOpen = true;
        }

        private void LoadData()
        {
            var thresholds = _dbService.GetThresholdSettings();
            MinHours = thresholds.Min; MaxHours = thresholds.Max;
            
            long apptTs = _dbService.GetAppointment();
            AppointmentDate = apptTs > 0 ? DateTimeOffset.FromUnixTimeSeconds(apptTs).ToLocalTime().DateTime : null;

            Logs.Clear();
            var records = _dbService.GetAllLogs();
            foreach (var record in records) Logs.Add(record);
            
            CheckThermalShadow(); UpdateTelemetry(); UpdateCharts(); UpdateHeatmap(); UpdateBlackoutBanner();
        }

        [RelayCommand] private void SetAppointment() { if (AppointmentDate.HasValue) { _dbService.SetAppointment(new DateTimeOffset(AppointmentDate.Value).ToUnixTimeSeconds()); LoadData(); } }
        [RelayCommand] private void ClearAppointment() { _dbService.ClearAppointment(); AppointmentDate = null; LoadData(); }

        private void UpdateBlackoutBanner()
        {
            long apptTs = _dbService.GetAppointment();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (apptTs > now)
            {
                double secondsUntil = apptTs - now; double days = Math.Round(secondsUntil / 86400.0, 1);
                if (secondsUntil <= (5 * 86400)) { BlackoutColor = "#ff9800"; BlackoutBorder = "#ff9800"; BlackoutMessage = $"CLINICAL BLACKOUT ACTIVE: Target T-Minus {days} Days"; }
                else { BlackoutColor = "#0d2a3a"; BlackoutBorder = "#0277bd"; BlackoutMessage = $"[!] Clinical Baseline Scheduled in {days} Days (Blackout begins at T-Minus 5 Days)"; }
                IsBlackoutActive = true;
            }
            else IsBlackoutActive = false;
        }

        private bool GetSupplementSaturation(long targetTs, string suppKey, int daysBack)
        {
            long start = targetTs - (daysBack * 24 * 3600);
            var windowLogs = Logs.Where(l => l.Timestamp >= start && l.Timestamp <= targetTs).ToList();
            if (windowLogs.Count == 0) return false;
            return ((double)windowLogs.Count(l => l.Supplements.Contains($"\"{suppKey}\":1")) / windowLogs.Count) >= 0.5;
        }

        private string EstimateBabyMakingVolume(long timestamp)
        {
            var priorRelease = Logs.Where(l => l.Timestamp < timestamp && l.Volume != "None" && l.Volume != "N/A").OrderByDescending(l => l.Timestamp).FirstOrDefault();
            double gapHours = priorRelease != null ? (timestamp - priorRelease.Timestamp) / 3600.0 : MaxHours;
            
            string estVol = "Normal";
            if (gapHours < MinHours) estVol = "Low"; else if (gapHours >= MaxHours) estVol = "High";
            
            if (GetSupplementSaturation(timestamp, "zinc", 21))
            {
                if (estVol == "Low") estVol = "Normal"; else if (estVol == "Normal") estVol = "High";
            }
            return estVol;
        }

        [RelayCommand]
        private void LogEvent(string mode)
        {
            string supps = $"{{\"zinc\":{(ZincActive ? 1 : 0)},\"maca\":{(MacaActive ? 1 : 0)},\"vitD\":{(VitDActive ? 1 : 0)},\"vitC\":{(VitCActive ? 1 : 0)}}}";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            string vol = SelectedVolume;
            if (mode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (mode == "Baby-Making") vol = EstimateBabyMakingVolume(timestamp);
            
            var log = new LogRecord { Timestamp = timestamp, Mode = mode, Volume = vol, HeatFlag = SelectedHeat, Supplements = supps, ClinicalVol = ClinicalVol ?? 0.0, Concentration = Concentration ?? 0, Motility = Motility ?? 0, ProgMotility = ProgMotility ?? 0, Morphology = Morphology ?? 0, PhLevel = PhLevel ?? 0.0 };

            _dbService.InsertLog(log); _dbService.SaveSupplementsState(ZincActive, MacaActive, VitDActive, VitCActive);
            LoadData(); ClearForm(); DatabaseService.ShowNotification("Event Logged", $"Successfully recorded {mode} event.");
        }

        [RelayCommand]
        private void SubmitManualLog()
        {
            if (!ManualDate.HasValue || !ManualTime.HasValue) return;
            DateTime dt = ManualDate.Value.Date + ManualTime.Value; long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();
            int z = ManualZinc ? 1 : 0, m = ManualMaca ? 1 : 0, d = ManualVitD ? 1 : 0, c = ManualVitC ? 1 : 0;
            string vol = ManualVolume;
            if (ManualMode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (ManualMode == "Baby-Making") vol = EstimateBabyMakingVolume(ts);

            var newLog = new LogRecord { Timestamp = ts, Mode = ManualMode, Volume = vol, HeatFlag = ManualHeat, Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c}}}", ClinicalVol = ManualClinicalVol ?? 0.0, Concentration = ManualConcentration ?? 0, Motility = ManualMotility ?? 0, ProgMotility = ManualProgMotility ?? 0, Morphology = ManualMorphology ?? 0, PhLevel = ManualPhLevel ?? 0.0 };
            
            _dbService.InsertLog(newLog, ManualLabFileName, _manualLabFileData);
            ManualLabFileName = null; _manualLabFileData = null; CloseManualLog(); LoadData();
        }

        [RelayCommand]
        private void OpenEditLog(long id)
        {
            var log = Logs.FirstOrDefault(l => l.Id == id);
            if (log == null) return;
            EditId = log.Id; var dtOffset = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime(); EditDate = dtOffset.Date; EditTime = dtOffset.TimeOfDay;
            EditMode = log.Mode; EditVolume = log.Volume; EditHeat = log.HeatFlag;
            EditZinc = log.Supplements.Contains("\"zinc\":1"); EditMaca = log.Supplements.Contains("\"maca\":1"); EditVitD = log.Supplements.Contains("\"vitD\":1"); EditVitC = log.Supplements.Contains("\"vitC\":1");
            EditClinicalVol = log.ClinicalVol > 0 ? log.ClinicalVol : null; EditConcentration = log.Concentration > 0 ? log.Concentration : null; EditMotility = log.Motility > 0 ? log.Motility : null; EditProgMotility = log.ProgMotility > 0 ? log.ProgMotility : null; EditMorphology = log.Morphology > 0 ? log.Morphology : null; EditPhLevel = log.PhLevel > 0 ? log.PhLevel : null;
            EditLabFileName = null; _editLabFileData = null; IsEditLogOpen = true;
        }

        [RelayCommand]
        private void SaveEditLog()
        {
            if (!EditDate.HasValue || !EditTime.HasValue) return;
            DateTime dt = EditDate.Value.Date + EditTime.Value; long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();
            int z = EditZinc ? 1 : 0, m = EditMaca ? 1 : 0, d = EditVitD ? 1 : 0, c = EditVitC ? 1 : 0;
            string vol = EditVolume;
            if (EditMode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (EditMode == "Baby-Making") vol = EstimateBabyMakingVolume(ts);

            var updatedLog = new LogRecord { Id = EditId, Timestamp = ts, Mode = EditMode, Volume = vol, HeatFlag = EditHeat, Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c}}}", ClinicalVol = EditClinicalVol ?? 0.0, Concentration = EditConcentration ?? 0, Motility = EditMotility ?? 0, ProgMotility = EditProgMotility ?? 0, Morphology = EditMorphology ?? 0, PhLevel = EditPhLevel ?? 0.0 };
            
            _dbService.UpdateLog(updatedLog, EditLabFileName, _editLabFileData);
            CloseEditLog(); LoadData();
        }

        [RelayCommand] private void DeleteLog(long id) { ShowAlert("Confirm Delete", "Are you sure you want to permanently purge this record?", () => { _dbService.DeleteLog(id); LoadData(); }); }

        [RelayCommand] private async Task SelectManualPdf(Window window) { var file = await OpenPdfPicker(window); if (file != null) { ManualLabFileName = file.Value.Name; _manualLabFileData = file.Value.Data; } }
        [RelayCommand] private async Task SelectEditPdf(Window window) { var file = await OpenPdfPicker(window); if (file != null) { EditLabFileName = file.Value.Name; _editLabFileData = file.Value.Data; } }

        private async Task<(string Name, byte[] Data)?> OpenPdfPicker(Window window)
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select Clinical Lab Report", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("PDF Documents") { Patterns = new[] { "*.pdf" } } } });
            if (files.Count > 0) { using var stream = await files[0].OpenReadAsync(); using var ms = new MemoryStream(); await stream.CopyToAsync(ms); return (files[0].Name, ms.ToArray()); } return null;
        }

        [RelayCommand]
        private void AutoCalibrate()
        {
            if (Logs.Count < 2) { ShowAlert("Error", "Not enough data. Keep logging!"); return; }

            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            var uncompromisedLogs = new List<LogRecord>();
            foreach (var l in releaseLogs) { long shadowWindow = l.Timestamp - (74 * 24 * 3600); if (!Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < l.Timestamp && x.HeatFlag >= 2)) uncompromisedLogs.Add(l); }
            if (uncompromisedLogs.Count < 2) { ShowAlert("Error", "Not enough uncompromised release data points to calibrate."); return; }

            var validGaps = new List<double>(); var allGaps = new List<double>();
            for (int i = 0; i < uncompromisedLogs.Count - 1; i++) { double gap = (uncompromisedLogs[i + 1].Timestamp - uncompromisedLogs[i].Timestamp) / 3600.0; allGaps.Add(gap); if ((uncompromisedLogs[i + 1].Mode == "Maintenance" || uncompromisedLogs[i + 1].Mode == "Clinical-Lab") && (uncompromisedLogs[i + 1].Volume == "Normal" || uncompromisedLogs[i + 1].Volume == "High")) validGaps.Add(gap); }
            if (validGaps.Count == 0) { ShowAlert("Error", "No successful uncompromised recoveries recorded yet."); return; }

            double roundedMin = Math.Round(validGaps.Average(), 1); double meanAll = allGaps.Average(); double stdDev = Math.Sqrt(allGaps.Sum(val => Math.Pow(val - meanAll, 2)) / allGaps.Count);
            double recommendedMax = Math.Round(meanAll + stdDev, 1); if (recommendedMax > 120.0) recommendedMax = 120.0;

            ShowAlert("Auto-Calibrate", $"Calibration Complete.\n(Thermal Shadow events excluded)\n\n[FLOOR] Minimum recovery gap: {roundedMin} hours.\n[CEILING] Max endurance: {recommendedMax} hours.\n\nUpdate your threshold settings?", () => { MinHours = roundedMin; MaxHours = recommendedMax; SaveSettings(); });
        }

        [RelayCommand]
        private void AnalyzeSupplements()
        {
            if (Logs.Count < 10) { ShowAlert("Error", "Need more data points to run statistical analysis. Keep logging!"); return; }

            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            var validLogs = new List<LogRecord>();
            foreach (var l in releaseLogs) { long shadowWindow = l.Timestamp - (74 * 24 * 3600); if (!Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < l.Timestamp && x.HeatFlag >= 2)) validLogs.Add(l); }
            if (validLogs.Count < 5) { ShowAlert("Error", "Insufficient uncompromised data points after filtering out Thermal Shadow periods."); return; }

            int zaTot = 0, zaSuc = 0, ziTot = 0, ziSuc = 0; int dTot = 0, dSuc = 0, diTot = 0, diSuc = 0;
            var mGaps = new List<double>(); var nmGaps = new List<double>(); var cGaps = new List<double>(); var ncGaps = new List<double>();

            for (int i = 1; i < validLogs.Count; i++)
            {
                var cur = validLogs[i]; double gap = (cur.Timestamp - validLogs[i - 1].Timestamp) / 3600.0;
                bool zSat = GetSupplementSaturation(cur.Timestamp, "zinc", 21); bool mSat = GetSupplementSaturation(cur.Timestamp, "maca", 21);
                bool dSat = GetSupplementSaturation(cur.Timestamp, "vitD", 30); bool cSat = GetSupplementSaturation(cur.Timestamp, "vitC", 30);
                bool isSuccess = (cur.Volume == "Normal" || cur.Volume == "High");

                if (cur.Mode == "Maintenance" || cur.Mode == "Clinical-Lab") { if (zSat) { zaTot++; if (isSuccess) zaSuc++; } else { ziTot++; if (isSuccess) ziSuc++; } if (dSat) { dTot++; if (isSuccess) dSuc++; } else { diTot++; if (isSuccess) diSuc++; } }
                if (mSat) mGaps.Add(gap); else nmGaps.Add(gap); if (cSat) cGaps.Add(gap); else ncGaps.Add(gap);
            }

            string msg = "--- BIOLOGICAL SATURATION ANALYSIS ---\n\n(Measuring efficacy based on cumulative buildup windows)\n(Thermal Shadow periods removed to prevent data corruption)\n\n";

            if (zaTot > 0 && ziTot > 0) { int zaPct = (int)Math.Round((zaSuc / (double)zaTot) * 100); int ziPct = (int)Math.Round((ziSuc / (double)ziTot) * 100); msg += $"[ZINC - YIELD RATE (21-Day Window)]\nUn-Saturated: {ziPct}% | Saturated: {zaPct}%\nResult: {(zaPct - ziPct > 0 ? $"+{zaPct - ziPct}% optimal yield probability." : "No measurable yield impact.")}\n\n"; } else { msg += "[ZINC] Insufficient baseline data.\n\n"; }
            if (mGaps.Count > 0 && nmGaps.Count > 0) { double mAvg = mGaps.Average(); double nmAvg = nmGaps.Average(); msg += $"[MACA ROOT - RECOVERY SPEED (21-Day Window)]\nUn-Saturated: {nmAvg:F1}h | Saturated: {mAvg:F1}h\nResult: {(nmAvg - mAvg > 0 ? $"Recovery accelerated by {nmAvg - mAvg:F1} hours." : "No measurable speed impact.")}\n\n"; } else { msg += "[MACA] Insufficient baseline data.\n\n"; }
            if (dTot > 0 && diTot > 0) { int dPct = (int)Math.Round((dSuc / (double)dTot) * 100); int diPct = (int)Math.Round((diSuc / (double)diTot) * 100); msg += $"[VITAMIN D3 - YIELD RATE (30-Day Window)]\nUn-Saturated: {diPct}% | Saturated: {dPct}%\nResult: {(dPct - diPct > 0 ? $"+{dPct - diPct}% optimal yield probability." : "No measurable yield impact.")}\n\n"; } else { msg += "[VITAMIN D3] Insufficient baseline data.\n\n"; }
            if (cGaps.Count > 0 && ncGaps.Count > 0) { double cAvg = cGaps.Average(); double ncAvg = ncGaps.Average(); msg += $"[VITAMIN C - RECOVERY SPEED (30-Day Window)]\nUn-Saturated: {ncAvg:F1}h | Saturated: {cAvg:F1}h\nResult: {(ncAvg - cAvg > 0 ? $"Recovery accelerated by {ncAvg - cAvg:F1} hours." : "No measurable speed impact.")}\n"; } else { msg += "[VITAMIN C] Insufficient baseline data.\n"; }

            ShowAlert("Analysis Complete", msg); CloseSettings();
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
                foreach(var lab in subsequentLabs) if (lab.Concentration >= 15 && lab.Motility >= 40) { IsShadowActive = false; return; }

                var clearsAt = DateTimeOffset.FromUnixTimeSeconds(latestHeat.Timestamp + (74 * 24 * 3600)).ToLocalTime();
                IsShadowActive = true; ShadowMessage = $"[!] SYSTEM COMPROMISED: Level {latestHeat.HeatFlag} Thermal Shadow active. Clears: {clearsAt:MMM dd, yyyy}.";
            }
            else IsShadowActive = false;
        }

        private void UpdateTelemetry()
        {
            // 1. Core Recovery Telemetry
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").ToList();
            if (releaseLogs.Any())
            {
                double currentDelta = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - releaseLogs.First().Timestamp) / 3600.0;
                HudCurrent = $"{currentDelta:F1}h"; 
                HudRemaining = (MaxHours - currentDelta) > 0 ? $"{(MaxHours - currentDelta):F1}h" : "OVERDUE";

                if (releaseLogs.Count >= 2)
                {
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
            }
            else
            {
                HudCurrent = "--"; HudRemaining = "--"; HudAvg = "--"; HudMax = "--";
            }

            // 2. Dynamic Rolling Frequency Telemetry (Up to 30 days)
            long thirtyDaysInSeconds = 2592000;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long cutoffTimestamp = now - thirtyDaysInSeconds;
            
            var recentEvents = Logs.Where(l => l.Timestamp >= cutoffTimestamp).ToList();
            
            if (recentEvents.Count == 0)
            {
                HudFrequency = "0.0/wk";
            }
            else
            {
                long firstEventEver = Logs.Min(l => l.Timestamp);
                long effectiveStart = Math.Max(cutoffTimestamp, firstEventEver);
                double daysActive = (now - effectiveStart) / 86400.0;
                
                if (daysActive < 1.0) daysActive = 1.0;

                double eventsPerWeek = (recentEvents.Count / daysActive) * 7.0;
                HudFrequency = $"{eventsPerWeek:F1}/wk";
            }
        }

        private void UpdateCharts()
        {
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            
            if (releaseLogs.Count == 0)
            {
                ChartSeries = Array.Empty<ISeries>(); _chartMinX = 0; _chartMaxX = 86400;
            }
            else
            {
                var gapData = new List<ChartLogPoint>(); var maintPts = new List<ChartLogPoint>(); var playPts = new List<ChartLogPoint>();
                var babyPts = new List<ChartLogPoint>(); var labPts = new List<ChartLogPoint>();

                for (int i = 0; i < releaseLogs.Count; i++) 
                {
                    double xVal = releaseLogs[i].Timestamp; double yVal = i == 0 ? MaxHours : (releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0;
                    var pt = new ChartLogPoint { X = xVal, Y = yVal, Log = releaseLogs[i] }; gapData.Add(pt);

                    switch (releaseLogs[i].Mode) { case "Maintenance": maintPts.Add(pt); break; case "Playtime": playPts.Add(pt); break; case "Baby-Making": babyPts.Add(pt); break; case "Clinical-Lab": labPts.Add(pt); break; }
                }

                if (gapData.Count > 0) { _chartMinX = gapData.First().X!.Value - 43200; _chartMaxX = gapData.Last().X!.Value + 43200; }

                var lineSeries = new LineSeries<ChartLogPoint> { Name = "", Values = gapData, LineSmoothness = 1, Fill = new LinearGradientPaint(new[] { new SKColor(85, 85, 85, 150), new SKColor(85, 85, 85, 10) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), Stroke = new SolidColorPaint(new SKColor(85, 85, 85)) { StrokeThickness = 2 }, GeometrySize = 0, GeometryFill = null, GeometryStroke = null, IsHoverable = false };
                var maintSeries = new ScatterSeries<ChartLogPoint> { Name = "", Values = maintPts, GeometrySize = 10, Fill = new SolidColorPaint(new SKColor(0, 122, 204)), Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } };
                var playSeries = new ScatterSeries<ChartLogPoint> { Name = "", Values = playPts, GeometrySize = 10, Fill = new SolidColorPaint(new SKColor(156, 39, 176)), Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } };
                var babySeries = new ScatterSeries<ChartLogPoint> { Name = "", Values = babyPts, GeometrySize = 10, Fill = new SolidColorPaint(new SKColor(76, 175, 80)), Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } };
                var labSeries = new ScatterSeries<ChartLogPoint> { Name = "", Values = labPts, GeometrySize = 10, Fill = new SolidColorPaint(new SKColor(84, 110, 122)), Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } };

                ChartSeries = new ISeries[] { lineSeries, maintSeries, playSeries, babySeries, labSeries };
                
                XAxes = new[] { new Axis { Labeler = value => { try { return DateTimeOffset.FromUnixTimeSeconds((long)value).ToLocalTime().ToString("MMM dd"); } catch { return string.Empty; } }, LabelsRotation = 15, LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 12, MinStep = 86400.0, MinLimit = _chartMinX, MaxLimit = _chartMaxX } };
                YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Gray), MinLimit = 0 } };
            }

            TotalEventCount = Logs.Count;
            int maint = Logs.Count(l => l.Mode == "Maintenance"); int play = Logs.Count(l => l.Mode == "Playtime"); int baby = Logs.Count(l => l.Mode == "Baby-Making"); int lab = Logs.Count(l => l.Mode == "Clinical-Lab");

            LegendMaint = $"Maintenance ({maint})"; LegendPlay = $"Playtime ({play})"; LegendBaby = $"Baby-Making ({baby})"; LegendLab = $"Clinical-Lab ({lab})";

            ModeSeries = new ISeries[] {
                new PieSeries<int> { Values = new[] { maint }, Name = "Maintenance", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(0, 122, 204)) },
                new PieSeries<int> { Values = new[] { play }, Name = "Playtime", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(156, 39, 176)) },
                new PieSeries<int> { Values = new[] { baby }, Name = "Baby-Making", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(76, 175, 80)) },
                new PieSeries<int> { Values = new[] { lab }, Name = "Clinical", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(84, 110, 122)) }
            };

            int volHigh = Logs.Count(l => l.Volume == "High"); 
            int volNormal = Logs.Count(l => l.Volume == "Normal"); 
            int volLow = Logs.Count(l => l.Volume == "Low"); 
            int volDry = Logs.Count(l => l.Volume == "None" || l.Volume == "N/A");

            VolumeSeries = new ISeries[] { new RowSeries<int> { Values = new[] { volDry, volLow, volNormal, volHigh }, Name = "Sessions", Stroke = null, DataLabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255)), DataLabelsSize = 12, DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End, } };
            
            ((RowSeries<int>)VolumeSeries[0]).PointMeasured += (point) =>
            {
                if (point.Visual == null) return;
                if (point.Index == 0) point.Visual.Fill = new SolidColorPaint(new SKColor(117, 117, 117)); // Dry
                if (point.Index == 1) point.Visual.Fill = new SolidColorPaint(new SKColor(255, 152, 0)); // Low
                if (point.Index == 2) point.Visual.Fill = new SolidColorPaint(new SKColor(0, 122, 204)); // Normal
                if (point.Index == 3) point.Visual.Fill = new SolidColorPaint(new SKColor(76, 175, 80)); // High
            };
            
            VolumeXAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 12, MinLimit = 0 } };
            VolumeYAxes = new[] { new Axis { Labels = new[] { "None", "Low", "Normal", "High" }, LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 12 } };
        }

        private void UpdateHeatmap()
        {
            HeatmapDays.Clear();
            var today = DateTime.Today;
            var logsByDate = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").GroupBy(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date).ToDictionary(g => g.Key, g => g.ToList());
            int padding = (int)today.AddDays(-364).DayOfWeek;
            
            for (int i = 0; i < padding; i++) HeatmapDays.Add(new HeatmapDay { Level = 0, ColorHex = "#252526" });

            for (int i = 364; i >= 0; i--)
            {
                var targetDate = today.AddDays(-i);
                var dayData = new HeatmapDay { DateText = targetDate.ToString("MMM dd, yyyy"), Level = 0, ColorHex = "#252526", MainInfoText = "No active yield events logged.", ModesText = "" };

                if (logsByDate.TryGetValue(targetDate, out var dayLogs))
                {
                    var dominantLog = dayLogs.OrderByDescending(l => l.Volume == "High" ? 3 : l.Volume == "Normal" ? 2 : 1).First();
                    dayData.Level = dominantLog.Volume == "High" ? 3 : dominantLog.Volume == "Normal" ? 2 : 1;
                    dayData.MainInfoText = $"Total Events: {dayLogs.Count}  |  Max Yield: {(dayData.Level == 3 ? "High" : dayData.Level == 2 ? "Normal" : "Low")}";
                    dayData.ModesText = "Modes Detected: " + string.Join(", ", dayLogs.Select(l => l.Mode).Distinct());

                    dayData.ColorHex = dominantLog.Mode switch { "Maintenance" => dayData.Level == 3 ? "#42a5f5" : dayData.Level == 2 ? "#007acc" : "#01437a", "Playtime" => dayData.Level == 3 ? "#ce93d8" : dayData.Level == 2 ? "#9c27b0" : "#4a148c", "Baby-Making" => dayData.Level == 3 ? "#81c784" : dayData.Level == 2 ? "#4caf50" : "#1b5e20", "Clinical-Lab"=> dayData.Level == 3 ? "#90a4ae" : dayData.Level == 2 ? "#546e7a" : "#263238", _ => "#007acc" };
                }
                HeatmapDays.Add(dayData);
            }
        }

        [RelayCommand] private void ChartClicked(object obj)
        {
            LiveChartsCore.Kernel.ChartPoint? point = null;
            if (obj is LiveChartsCore.Kernel.ChartPoint singlePoint) point = singlePoint; else if (obj is IEnumerable<LiveChartsCore.Kernel.ChartPoint> points) point = points.FirstOrDefault();
            if (point == null) return;
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
            if (point.Index >= 0 && point.Index < releaseLogs.Count) { var log = releaseLogs[point.Index]; SelectedLog = log; RequestScrollToLog?.Invoke(log); }
        }

        private void ClearForm() { SelectedVolume = "Normal"; SelectedHeat = 0; ClinicalVol = null; Concentration = null; Motility = null; ProgMotility = null; Morphology = null; PhLevel = null; }  
        private void SetupChartAxes() { XAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; VolumeXAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; }
    }

    public class ChartLogPoint : LiveChartsCore.Defaults.ObservablePoint
    {
        public LogRecord Log { get; set; } = null!;
        public string GapText => $"Gap: {(Y ?? 0):F1} Hrs";
        public string HeaderText => DateTimeOffset.FromUnixTimeSeconds(Log.Timestamp).ToLocalTime().ToString("MMM dd, yyyy @ HH:mm");
        public string FlagsText { get { var flags = new List<string>(); if (Log.HeatFlag > 0) flags.Add($"[HEAT L{Log.HeatFlag}]"); if (Log.Supplements.Contains("\"zinc\":1")) flags.Add("[Zn]"); if (Log.Supplements.Contains("\"maca\":1")) flags.Add("[Ma]"); if (Log.Supplements.Contains("\"vitD\":1")) flags.Add("[D3]"); if (Log.Supplements.Contains("\"vitC\":1")) flags.Add("[C]"); return string.Join(" ", flags); } }
        public bool HasFlags => FlagsText.Length > 0;
        public string LabText { get { var lines = new List<string>(); if (Log.Concentration > 0) lines.Add($"Conc: {Log.Concentration} M"); if (Log.Motility > 0) lines.Add($"Mot: {Log.Motility}%"); if (Log.ProgMotility > 0) lines.Add($"Prog: {Log.ProgMotility}%"); if (Log.Morphology > 0) lines.Add($"Morph: {Log.Morphology}%"); return string.Join(" | ", lines); } }
        public bool HasLab => Log.Mode == "Clinical-Lab" && LabText.Length > 0;
        public string ModeHex => Log.Mode switch { "Maintenance" => "#007acc", "Playtime" => "#9c27b0", "Baby-Making" => "#4caf50", "Clinical-Lab" => "#546e7a", _ => "#ccc" };
    }

    public class PieHoverData { public string Category { get; set; } = ""; public int Count { get; set; } public string ColorHex { get; set; } = ""; public double Percentage { get; set; } }
    public class BarHoverData { public string Category { get; set; } = ""; public int Count { get; set; } public string ColorHex { get; set; } = ""; }
    public class HeatmapDay { public int Level { get; set; } public string ColorHex { get; set; } = ""; public string DateText { get; set; } = ""; public string MainInfoText { get; set; } = ""; public string ModesText { get; set; } = ""; public bool HasData => Level > 0; }
}
