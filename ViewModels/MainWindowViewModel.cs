using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DadPlanner2.Models;
using DadPlanner2.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DadPlanner2.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly TelemetryAnalysisService _telemetryAnalysis = new();
        private readonly LogValidationService _logValidation = new();
        private readonly SupplementAnalysisService _supplementAnalysis = new();
        private readonly SupplementSaturationService _supplementSaturation = new();
        private readonly ClinicalDeltaService _clinicalDelta = new();
        private readonly PharmacokineticService _pkService = new();
        private readonly UIRefreshService _uiRefresh = new();
        private int _tickCount = 0;

        public Action<LogRecord>? RequestScrollToLog;
        [ObservableProperty] private LogRecord? _selectedLog;

        [ObservableProperty] private double _minHours = 24.0;
        [ObservableProperty] private double _maxHours = 72.0;
        
        [ObservableProperty] private int _chartRangeDays = 90;

        private double _chartMinX;
        private double _chartMaxX;

        [ObservableProperty] private bool _isTooltipVisible;
        [ObservableProperty] private double _tooltipX;
        [ObservableProperty] private double _tooltipY;
        
        [ObservableProperty] private ChartLogPoint? _hoveredTimelinePoint;
        [ObservableProperty] private PieHoverData? _hoveredPie;
        [ObservableProperty] private BarHoverData? _hoveredBar;
        [ObservableProperty] private EnthusiasmHoverData? _hoveredEnthusiasm;
        [ObservableProperty] private PkHoverData? _hoveredPk;

        [ObservableProperty] private bool _isHelpOpen;
        [ObservableProperty] private bool _isSettingsOpen;
        [ObservableProperty] private bool _isManualLogOpen;
        [ObservableProperty] private bool _isEditLogOpen;
        [ObservableProperty] private bool _isAlertOpen;
        [ObservableProperty] private bool _isAnalysisOpen;
        [ObservableProperty] private bool _isDeltaOpen;
        [ObservableProperty] private bool _isBiometricsOpen;
        [ObservableProperty] private bool _isStealthMode;
        [ObservableProperty] private bool _isVolumeMode;
        
        public double StealthBlurRadius => IsStealthMode ? 12.0 : 0.0;

        [ObservableProperty] private string _alertTitle = "";
        [ObservableProperty] private string _alertMessage = "";
        [ObservableProperty] private string _analysisMessage = "";
        [ObservableProperty] private bool _showAlertConfirm;
        private Action? _alertConfirmAction;

        [ObservableProperty] private bool _isTestModeActive;
        [ObservableProperty] private bool _isShadowActive;
        [ObservableProperty] private string _shadowMessage = "";
        [ObservableProperty] private string _shadowStageTitle = "";
        [ObservableProperty] private string _shadowStageDesc = "";
        [ObservableProperty] private double _shadowProgressPercent = 0.0;

        [ObservableProperty] private bool _isBlackoutActive;
        [ObservableProperty] private string _blackoutMessage = "";
        [ObservableProperty] private string _blackoutColor = "#0d2a3a";
        [ObservableProperty] private string _blackoutBorder = "#0277bd";
        [ObservableProperty] private string _clinicalClearanceTarget = "";

        [ObservableProperty] private string _hudCurrent = "--";
        [ObservableProperty] private string _hudRemaining = "--";
        [ObservableProperty] private string _hudRemainingLabel = "LIMIT T-MINUS";
        [ObservableProperty] private string _hudPhaseLabel = "RECHARGING";
        [ObservableProperty] private string _hudPhaseColor = "#007acc";
        [ObservableProperty] private double _hudProgressValue = 0.0;
        [ObservableProperty] private string _hudAvg = "--";
        [ObservableProperty] private string _hudMax = "--";
        [ObservableProperty] private string _hudFrequency = "--";
        
        [ObservableProperty] private DateTime? _appointmentDate;
        [ObservableProperty] private TimeSpan? _appointmentTime;
        [ObservableProperty] private string _clinicalComplianceMessage = "";
        [ObservableProperty] private string _clinicalComplianceColor = "#0277bd";
        [ObservableProperty] private bool _enableAggressiveNotifications = true;
        [ObservableProperty] private bool _enableSenescenceAlert = true;
        [ObservableProperty] private bool _includeNotesInReport = false;
        [ObservableProperty] private bool _showWankAlert;

        [ObservableProperty] private double? _bioLeftL;
        [ObservableProperty] private double? _bioLeftW;
        [ObservableProperty] private double? _bioLeftH;
        [ObservableProperty] private double? _bioRightL;
        [ObservableProperty] private double? _bioRightW;
        [ObservableProperty] private double? _bioRightH;
        [ObservableProperty] private double _userTotalVolume = 30.0; 

        public double CalculatedTotalVolume => 
            Math.Round(((BioLeftL ?? 0) * (BioLeftW ?? 0) * (BioLeftH ?? 0) * 0.71) + 
                       ((BioRightL ?? 0) * (BioRightW ?? 0) * (BioRightH ?? 0) * 0.71), 1);

        partial void OnBioLeftLChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));
        partial void OnBioLeftWChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));
        partial void OnBioLeftHChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));
        partial void OnBioRightLChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));
        partial void OnBioRightWChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));
        partial void OnBioRightHChanged(double? value) => OnPropertyChanged(nameof(CalculatedTotalVolume));

        public string PdfButtonText => IncludeNotesInReport ? "[PDF] 90-Day Report (W/ Notes)" : "[PDF] 90-Day Report (Clinical)";

        private string _backupPath = "";
        public string BackupPath
        {
            get => _backupPath;
            set => SetProperty(ref _backupPath, value);
        }

        [ObservableProperty] private string _selectedMode = "Maintenance";
        [ObservableProperty] private string _selectedVolume = "Normal";
        [ObservableProperty] private int _selectedReleaseCount = 1;
        [ObservableProperty] private VolumeConfidence _selectedVolumeConfidence = VolumeConfidence.Observed;
        [ObservableProperty] private int _selectedHeat = 0;
        [ObservableProperty] private bool _zincActive;
        [ObservableProperty] private bool _macaActive;
        [ObservableProperty] private bool _vitDActive;
        [ObservableProperty] private bool _vitCActive;
        [ObservableProperty] private bool _tadalafilActive;
        [ObservableProperty] private int _tadalafilDose = 10;
        [ObservableProperty] private string _selectedNotes = "";
        
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
        [ObservableProperty] private int _manualReleaseCount = 1;
        [ObservableProperty] private VolumeConfidence _manualVolumeConfidence = VolumeConfidence.Observed;
        [ObservableProperty] private int _manualHeat = 0;
        [ObservableProperty] private bool _manualZinc;
        [ObservableProperty] private bool _manualMaca;
        [ObservableProperty] private bool _manualVitD;
        [ObservableProperty] private bool _manualVitC;
        [ObservableProperty] private bool _manualTadalafil;
        [ObservableProperty] private int _manualTadalafilDose = 10;
        [ObservableProperty] private string _manualNotes = "";
        
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
        [ObservableProperty] private int _editReleaseCount = 1;
        [ObservableProperty] private VolumeConfidence _editVolumeConfidence = VolumeConfidence.Unknown;
        [ObservableProperty] private int _editHeat = 0;
        [ObservableProperty] private bool _editZinc;
        [ObservableProperty] private bool _editMaca;
        [ObservableProperty] private bool _editVitD;
        [ObservableProperty] private bool _editVitC;
        [ObservableProperty] private bool _editTadalafil;
        [ObservableProperty] private int _editTadalafilDose = 10;
        [ObservableProperty] private string _editNotes = "";
        
        public bool ShowEditClinicalFields => EditMode == "Clinical-Lab";
        [ObservableProperty] private double? _editClinicalVol;
        [ObservableProperty] private int? _editConcentration;
        [ObservableProperty] private int? _editMotility;
        [ObservableProperty] private int? _editProgMotility;
        [ObservableProperty] private int? _editMorphology;
        [ObservableProperty] private double? _editPhLevel;
        [ObservableProperty] private string? _editLabFileName;
        private byte[]? _editLabFileData;
        
        [ObservableProperty] private string _editHistoryText = "No previous edits recorded.";
        public ObservableCollection<LogEditHistory> EditHistoryEntries { get; } = new();
        [ObservableProperty] private string _zincAnalysisSummary = "";
        [ObservableProperty] private string _macaAnalysisSummary = "";
        [ObservableProperty] private string _vitaminDAnalysisSummary = "";
        [ObservableProperty] private string _vitaminCAnalysisSummary = "";

        public ObservableCollection<LogRecord> Logs { get; } = new();
        public ObservableCollection<LogRecord> FilteredLogs { get; } = new();
        public ObservableCollection<HeatmapDay> HeatmapDays { get; } = new();
        
        [ObservableProperty] private string _filterMode = "All";
        [ObservableProperty] private string _filterVolume = "All";
        [ObservableProperty] private string _filterSupplement = "All";

        public ObservableCollection<LogRecord> ClinicalLogs { get; } = new();
        [ObservableProperty] private LogRecord? _selectedLabA;
        [ObservableProperty] private LogRecord? _selectedLabB;
        [ObservableProperty] private ClinicalDeltaResult? _deltaResult;

        [ObservableProperty] private ISeries[] _chartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
        [ObservableProperty] private RectangularSection[] _timelineSections = Array.Empty<RectangularSection>();
        
        [ObservableProperty] private ISeries[] _modeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _volumeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _volumeXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _volumeYAxes = Array.Empty<Axis>();
        
        [ObservableProperty] private ISeries[] _enthusiasmSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _enthusiasmXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _enthusiasmYAxes = Array.Empty<Axis>();
        [ObservableProperty] private double _enthusiasmTargetScore;

        [ObservableProperty] private ISeries[] _deltaChartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _deltaXAxes = new[] { new Axis() };
        [ObservableProperty] private Axis[] _deltaYAxes = new[] { new Axis() };

        [ObservableProperty] private ISeries[] _pkSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _pkXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _pkYAxes = Array.Empty<Axis>();

        [ObservableProperty] private string _diurnalPeakText = "Awaiting data...";
        
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
            
            BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DadPlanner2", "Backups");

            var thresholds = _dbService.GetThresholdSettings();
            MinHours = thresholds.Min;
            MaxHours = thresholds.Max;
            
            ChartRangeDays = _dbService.GetChartRangeDays();

            long appt = _dbService.GetAppointment();
            if (appt > 0)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(appt).ToLocalTime();
                AppointmentDate = dt.DateTime;
                AppointmentTime = dt.TimeOfDay;
            }

            var savedSupps = _dbService.GetSupplementsState();
            ZincActive = savedSupps.zn;
            MacaActive = savedSupps.ma;
            VitDActive = savedSupps.vitD;
            VitCActive = savedSupps.vitC;
            TadalafilActive = savedSupps.tadActive;
            TadalafilDose = savedSupps.tadDose;
            
            _enableSenescenceAlert = _dbService.GetSenescenceAlertSetting();
            _includeNotesInReport = _dbService.GetIncludeNotesInReportSetting();
            OnPropertyChanged(nameof(EnableSenescenceAlert));
            OnPropertyChanged(nameof(IncludeNotesInReport));
            OnPropertyChanged(nameof(PdfButtonText));

            LoadData();

            _uiRefresh.OnRefreshTick += () =>
            {
                UpdateTelemetry();
                UpdateBlackoutBanner();

                _tickCount++;
                if (_tickCount >= 60)
                {
                    _tickCount = 0;
                    if (FilteredLogs.Count > 0)
                    {
                        var selected = SelectedLog;
                        for (int i = 0; i < FilteredLogs.Count; i++) FilteredLogs[i] = FilteredLogs[i]; 
                        SelectedLog = selected;
                    }
                }
            };
            _uiRefresh.Start();
        }

        partial void OnIncludeNotesInReportChanged(bool value)
        {
            if (_dbService == null) return;
            _dbService.SaveIncludeNotesInReportSetting(value);
            _dbService.MarkDirty();
            OnPropertyChanged(nameof(PdfButtonText));
        }

        partial void OnEnableSenescenceAlertChanged(bool value)
        {
            if (_dbService == null) return;
            _dbService.SaveSenescenceAlertSetting(value);
            _dbService.MarkDirty();
            UpdateTelemetry(); 
        }

        partial void OnSelectedModeChanged(string value) => OnPropertyChanged(nameof(ShowClinicalFields));
        partial void OnManualModeChanged(string value) => OnPropertyChanged(nameof(ShowManualClinicalFields));
        partial void OnEditModeChanged(string value) => OnPropertyChanged(nameof(ShowEditClinicalFields));
        partial void OnIsStealthModeChanged(bool value) => OnPropertyChanged(nameof(StealthBlurRadius));
        partial void OnIsVolumeModeChanged(bool value) => UpdateHeatmap();
        partial void OnSelectedLabAChanged(LogRecord? value) => CalculateDelta();
        partial void OnSelectedLabBChanged(LogRecord? value) => CalculateDelta();
        
        partial void OnFilterModeChanged(string value) => ApplyFilters();
        partial void OnFilterVolumeChanged(string value) => ApplyFilters();
        partial void OnFilterSupplementChanged(string value) => ApplyFilters();

        [RelayCommand] private void ToggleStealth() => IsStealthMode = !IsStealthMode;
        [RelayCommand] private void OpenHelp() => IsHelpOpen = true;
        [RelayCommand] private void CloseHelp() => IsHelpOpen = false;
        [RelayCommand] private void OpenSettings() => IsSettingsOpen = true;
        [RelayCommand] private void CloseSettings() => IsSettingsOpen = false;
        [RelayCommand] private void OpenManualLog() => IsManualLogOpen = true;
        [RelayCommand] private void CloseManualLog() => IsManualLogOpen = false;
        [RelayCommand] private void CloseEditLog() => IsEditLogOpen = false;
        [RelayCommand] private void CloseAlert() => IsAlertOpen = false;
        [RelayCommand] private void CloseAnalysis() => IsAnalysisOpen = false;
        [RelayCommand] private void CloseDelta() => IsDeltaOpen = false;
        [RelayCommand] private void OpenBiometrics() { IsBiometricsOpen = true; CloseSettings(); }
        [RelayCommand] private void CloseBiometrics() => IsBiometricsOpen = false;
        [RelayCommand] private void ConfirmAlert() { _alertConfirmAction?.Invoke(); IsAlertOpen = false; }
        
        [RelayCommand] 
        private void ToggleTestMode() 
        { 
            _dbService.ToggleTestMode(); 
            IsTestModeActive = _dbService.CheckIfTestModeActive(); 
            _dbService.MarkDirty(); 
            LoadData(); 
            CloseSettings(); 
        }

        [RelayCommand] 
        private void SaveSettings() 
        { 
            string? thresholdError = _logValidation.ValidateThresholds(MinHours, MaxHours);
            if (thresholdError != null)
            {
                ShowAlert("Invalid Thresholds", thresholdError);
                return;
            }

            _dbService.SaveThresholdSettings(MinHours, MaxHours); 
            _dbService.MarkDirty();
            LoadData(); 
            CloseSettings(); 
        }
        
        [RelayCommand] 
        private void SaveBiometrics()
        {
            if (CalculatedTotalVolume > 0)
            {
                UserTotalVolume = CalculatedTotalVolume;
                _dbService.SaveBiometrics(BioLeftL, BioLeftW, BioLeftH, BioRightL, BioRightW, BioRightH, UserTotalVolume);
                _dbService.MarkDirty();
                ShowAlert("Biometrics Saved", $"Max endurance ceiling scaled to accommodate {UserTotalVolume:F1}mL total capacity.");
                CloseBiometrics();
                UpdateTelemetry(); 
            }
            else
            {
                ShowAlert("Invalid Data", "Please enter valid measurements.");
            }
        }

        [RelayCommand]
        private void ClearBiometrics()
        {
            BioLeftL = null; BioLeftW = null; BioLeftH = null;
            BioRightL = null; BioRightW = null; BioRightH = null;
            UserTotalVolume = 30.0;
            _dbService.ClearBiometrics();
            _dbService.MarkDirty();
            ShowAlert("Biometrics Cleared", "Custom capacity removed. Reverting to 30.0mL clinical baseline.");
            CloseBiometrics();
            UpdateTelemetry();
        }

        [RelayCommand] private void GeneratePdf() => _dbService.Generate90DayReport(UserTotalVolume);
        [RelayCommand] private void OpenPdf(long id) => _dbService.OpenLabReportPdf(id);

        [RelayCommand]
        private void OpenBackupFolder()
        {
            if (!Directory.Exists(BackupPath)) Directory.CreateDirectory(BackupPath);
            
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo { FileName = BackupPath, UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = BackupPath }); 
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start(new ProcessStartInfo { FileName = "open", Arguments = BackupPath });
            }
            catch (Exception ex)
            {
                ShowAlert("Folder Unavailable", $"Could not open the backup folder:\n{ex.Message}");
            }
        }

        [RelayCommand]
        private void RunManualBackup()
        {
            _dbService.MarkDirty();
            if (_dbService.ExecuteAutoBackup(BackupPath))
                ShowAlert("Backup Complete", $"Database successfully backed up to:\n{BackupPath}");
        }

        [RelayCommand]
        private void RestoreLatestBackup()
        {
            string? latestBackup = _dbService.GetLatestBackup(BackupPath);
            if (latestBackup == null)
            {
                ShowAlert("No Backup Found", "Create a backup before attempting to restore one.");
                return;
            }

            ShowAlert(
                "Restore Latest Backup",
                $"Restore the latest backup?\n\n{latestBackup}\n\nCurrent data will be replaced.",
                () =>
                {
                    try
                    {
                        _dbService.RestoreBackup(latestBackup);
                        IsTestModeActive = _dbService.CheckIfTestModeActive();
                        LoadData();
                        ShowAlert("Restore Complete", "The latest database backup has been restored.");
                    }
                    catch (Exception ex)
                    {
                        ShowAlert("Restore Failed", ex.Message);
                    }
                });
        }

        [RelayCommand]
        private void ExportData()
        {
            try
            {
                var exports = _dbService.ExportData(BackupPath);
                ShowAlert("Export Complete", $"Portable exports created:\n{exports.JsonPath}\n{exports.CsvPath}"
                    + $"\n{exports.HistoryJsonPath}\n{exports.HistoryCsvPath}"
                    + (exports.AnalysisJsonPath == null ? "" : $"\n{exports.AnalysisJsonPath}\n{exports.AnalysisCsvPath}"));
            }
            catch (Exception ex)
            {
                ShowAlert("Export Failed", ex.Message);
            }
        }

        public void HandleShutdown()
        {
            _uiRefresh.Stop();
            _uiRefresh.Dispose();
            _dbService.ExecuteAutoBackup(BackupPath);
        }

        [RelayCommand]
        private void ZoomChart(string range)
        {
            if (range == "ALL")
            {
                ChartRangeDays = 0;
            }
            else if (int.TryParse(range, out int days))
            {
                ChartRangeDays = days;
            }
            
            _dbService.SaveChartRangeDays(ChartRangeDays);
            _dbService.MarkDirty();
            ApplyChartRange();
            UpdateHeatmap();
        }

        private void ApplyChartRange()
        {
            if (XAxes.Length == 0) return;
            
            double minLimit = _chartMinX;
            double maxLimit = _chartMaxX;

            if (ChartRangeDays > 0)
            {
                // Subtract the requested days (in Ticks) from the maximum chart X value
                minLimit = _chartMaxX - TimeSpan.FromDays(ChartRangeDays).Ticks;
                if (minLimit < _chartMinX) minLimit = _chartMinX;
            }

            // Apply to Main Timeline
            XAxes[0].MinLimit = minLimit;
            XAxes[0].MaxLimit = maxLimit;
            OnPropertyChanged(nameof(XAxes)); // Force redraw

            // Apply to Enthusiasm Macro-Trend
            if (EnthusiasmXAxes.Length > 0)
            {
                EnthusiasmXAxes[0].MinLimit = minLimit;
                EnthusiasmXAxes[0].MaxLimit = maxLimit;
                OnPropertyChanged(nameof(EnthusiasmXAxes)); // Force redraw
            }

            // Apply to Pharmacokinetic Decay
            if (PkXAxes.Length > 0)
            {
                PkXAxes[0].MinLimit = minLimit;
                PkXAxes[0].MaxLimit = maxLimit;
                OnPropertyChanged(nameof(PkXAxes)); // Force redraw
            }
        }

        private void PositionTooltip(double winX, double winY, double ctrlX, double ctrlY, double ctrlW, double ctrlH, double estW, double estH)
        {
            double relX = winX - ctrlX;
            double relY = winY - ctrlY;

            double targetX = (relX > (ctrlW / 2.0)) ? (winX - estW - 20) : (winX + 20);
            double targetY = (relY > (ctrlH / 2.0)) ? (winY - estH - 20) : (winY + 20);

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

            if (closest != null && Math.Abs((closest.X ?? 0) - targetTs) < TimeSpan.FromDays(3).Ticks)
            {
                HoveredTimelinePoint = closest; HoveredPie = null; HoveredBar = null; HoveredEnthusiasm = null; HoveredPk = null;
                PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 280, 210); 
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
            HoveredTimelinePoint = null; HoveredBar = null; HoveredEnthusiasm = null; HoveredPk = null;
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
            int volLow = Logs.Count(l => l.Volume == "Low" && l.Mode != "Daily Dose");
            int volDry = Logs.Count(l => (l.Volume == "None" || l.Volume == "N/A") && l.Mode != "Daily Dose");

            string category = ""; int count = 0; string hex = "";
            
            if (visualIdx == 0) { category = "High Yield"; count = volHigh; hex = "#4caf50"; }
            else if (visualIdx == 1) { category = "Normal Yield"; count = volNormal; hex = "#007acc"; }
            else if (visualIdx == 2) { category = "Low Yield"; count = volLow; hex = "#ff9800"; }
            else if (visualIdx == 3) { category = "Dry / None"; count = volDry; hex = "#757575"; }

            HoveredBar = new BarHoverData { Category = category, Count = count, ColorHex = hex };
            HoveredTimelinePoint = null; HoveredPie = null; HoveredEnthusiasm = null; HoveredPk = null;
            PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 240, 110);
            IsTooltipVisible = true;
        }

        public void ProcessPkHover(double chartX, double winX, double winY, double plotX, double plotW, double ctrlX, double ctrlY, double ctrlW, double ctrlH)
        {
            if (PkSeries == null || PkSeries.Length == 0 || PkXAxes == null || PkXAxes.Length == 0 || plotW <= 0)
            {
                IsTooltipVisible = false; return;
            }

            double adjustedX = chartX - plotX;
            if (adjustedX < 0 || adjustedX > plotW) { IsTooltipVisible = false; return; }

            var axis = PkXAxes[0];
            double minTs = axis.MinLimit ?? _chartMinX;
            double maxTs = axis.MaxLimit ?? _chartMaxX;
            double ratio = adjustedX / plotW;
            double targetTs = minTs + (ratio * (maxTs - minTs));

            var zincPts = PkSeries.ElementAtOrDefault(0)?.Values as IEnumerable<ObservablePoint>;
            var macaPts = PkSeries.ElementAtOrDefault(1)?.Values as IEnumerable<ObservablePoint>;
            var vitDPts = PkSeries.ElementAtOrDefault(2)?.Values as IEnumerable<ObservablePoint>;
            var vitCPts = PkSeries.ElementAtOrDefault(3)?.Values as IEnumerable<ObservablePoint>;
            var tadPts  = PkSeries.ElementAtOrDefault(4)?.Values as IEnumerable<ObservablePoint>;

            if (zincPts == null) { IsTooltipVisible = false; return; }

            var closestZinc = zincPts.OrderBy(p => Math.Abs((p.X ?? 0) - targetTs)).FirstOrDefault();
            if (closestZinc == null || Math.Abs((closestZinc.X ?? 0) - targetTs) >= TimeSpan.FromDays(3).Ticks)
            {
                IsTooltipVisible = false;
                return;
            }

            double ts = closestZinc.X ?? 0;
            double zincVal = closestZinc.Y ?? 0;
            double macaVal = macaPts?.FirstOrDefault(p => Math.Abs((p.X ?? 0) - ts) < TimeSpan.FromHours(1).Ticks)?.Y ?? 0;
            double vitDVal = vitDPts?.FirstOrDefault(p => Math.Abs((p.X ?? 0) - ts) < TimeSpan.FromHours(1).Ticks)?.Y ?? 0;
            double vitCVal = vitCPts?.FirstOrDefault(p => Math.Abs((p.X ?? 0) - ts) < TimeSpan.FromHours(1).Ticks)?.Y ?? 0;
            double tadVal  = tadPts?.FirstOrDefault(p => Math.Abs((p.X ?? 0) - ts) < TimeSpan.FromHours(1).Ticks)?.Y ?? 0;

            HoveredPk = new PkHoverData
            {
                DateText = new DateTime((long)ts).ToString("MMM dd, yyyy"),
                Zinc = zincVal,
                Maca = macaVal,
                VitD = vitDVal,
                VitC = vitCVal,
                Tadalafil = tadVal
            };

            HoveredTimelinePoint = null; HoveredPie = null; HoveredBar = null; HoveredEnthusiasm = null;
            PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 220, 145);
            IsTooltipVisible = true;
        }

        private void ShowAlert(string title, string message, Action? onConfirm = null)
        {
            AlertTitle = title; AlertMessage = message;
            _alertConfirmAction = onConfirm; ShowAlertConfirm = onConfirm != null;
            IsAlertOpen = true;
        }

        private bool ValidateLogInput(
            string mode,
            double? clinicalVol,
            int? concentration,
            int? motility,
            int? progMotility,
            int? morphology,
            double? phLevel,
            long timestamp,
            long? existingId = null,
            int releaseCount = 1,
            VolumeConfidence volumeConfidence = VolumeConfidence.Unknown)
        {
            string? error = _logValidation.Validate(
                Logs.ToList(),
                mode,
                clinicalVol,
                concentration,
                motility,
                progMotility,
                morphology,
                phLevel,
                timestamp,
                existingId,
                releaseCount,
                volumeConfidence);

            if (error == null) return true;

            string title = error.StartsWith("A log", StringComparison.Ordinal)
                ? error.StartsWith("A log cannot", StringComparison.Ordinal) ? "Invalid Date" : "Duplicate Log"
                : "Invalid Log Data";
            ShowAlert(title, error);
            return false;
        }

        private void ApplyFilters()
        {
            var selected = SelectedLog;
            FilteredLogs.Clear();
            
            foreach (var log in Logs)
            {
                if (FilterMode != "All" && log.Mode != FilterMode) continue;
                if (FilterVolume != "All" && log.Volume != FilterVolume) continue;
                
                if (FilterSupplement != "All")
                {
                    bool hasMatch = FilterSupplement switch
                    {
                        "Zinc" => log.Supplements.Contains("\"zinc\":1"),
                        "Maca" => log.Supplements.Contains("\"maca\":1"),
                        "Vit D3" => log.Supplements.Contains("\"vitD\":1"),
                        "Vit C" => log.Supplements.Contains("\"vitC\":1"),
                        "Tadalafil" => ParseTadalafilDose(log.Supplements) > 0,
                        _ => true
                    };
                    if (!hasMatch) continue;
                }

                FilteredLogs.Add(log);
            }
            
            if (selected != null && FilteredLogs.Contains(selected))
            {
                SelectedLog = selected;
            }
        }

        private int ParseTadalafilDose(string supps)
        {
            if (string.IsNullOrEmpty(supps)) return 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(supps);
                if (doc.RootElement.TryGetProperty("tadalafil", out var tProp)) return tProp.GetInt32();
            }
            catch
            {
                if (supps.Contains("\"tadalafil\":1")) return 10; // Legacy default
            }
            return 0;
        }

        private void UpdateChartSections()
        {
            var sections = new List<RectangularSection>();
            long apptTs = _dbService.GetAppointment();
            
            if (apptTs > 0)
            {
                var apptDt = DateTimeOffset.FromUnixTimeSeconds(apptTs).ToLocalTime();
                long apptTicks = apptDt.Ticks;
                
                long ticks48 = apptDt.AddHours(-48).Ticks;
                long ticks72 = apptDt.AddHours(-72).Ticks;
                long ticks168 = apptDt.AddDays(-7).Ticks;

                // 1. WHO Acceptable Window (72h - 168h)
                sections.Add(new RectangularSection
                {
                    Xi = ticks168,
                    Xj = ticks72,
                    Fill = new SolidColorPaint(new SKColor(255, 152, 0, 15)),
                    Label = "ACCEPTABLE (2-7d)",
                    LabelPaint = new SolidColorPaint(new SKColor(255, 152, 0, 150)),
                    LabelSize = 11
                });

                // 2. WHO Optimal Window (48h - 72h)
                sections.Add(new RectangularSection
                {
                    Xi = ticks72,
                    Xj = ticks48,
                    Fill = new SolidColorPaint(new SKColor(76, 175, 80, 40)),
                    Label = "TARGET CLEARANCE (48-72h)",
                    LabelPaint = new SolidColorPaint(new SKColor(76, 175, 80, 200)),
                    LabelSize = 12
                });

                // 3. Sub-48h Warning Window (0 - 48h)
                sections.Add(new RectangularSection
                {
                    Xi = ticks48,
                    Xj = apptTicks,
                    Fill = new SolidColorPaint(new SKColor(211, 47, 47, 30)),
                    Label = "SUB-48H (AVOID)",
                    LabelPaint = new SolidColorPaint(new SKColor(211, 47, 47, 200)),
                    LabelSize = 11
                });

                // 4. Exact Appointment Line
                sections.Add(new RectangularSection
                {
                    Xi = apptTicks,
                    Xj = apptTicks, 
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0)) 
                    { 
                        StrokeThickness = 2, 
                        PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 6, 6 }) 
                    },
                    Label = $"APPT: {apptDt:MMM dd, h:mm tt}",
                    LabelPaint = new SolidColorPaint(new SKColor(245, 124, 0)),
                    LabelSize = 13
                });
            }

            TimelineSections = sections.ToArray();
        }

        private void LoadData()
        {
            var thresholds = _dbService.GetThresholdSettings();
            MinHours = thresholds.Min; MaxHours = thresholds.Max;
            
            long apptTs = _dbService.GetAppointment();
            if (apptTs > 0)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(apptTs).ToLocalTime();
                AppointmentDate = dt.DateTime;
                AppointmentTime = dt.TimeOfDay;
            }
            else
            {
                AppointmentDate = null;
                AppointmentTime = null;
            }

            var savedSupps = _dbService.GetSupplementsState();
            ZincActive = savedSupps.zn;
            MacaActive = savedSupps.ma;
            VitDActive = savedSupps.vitD;
            VitCActive = savedSupps.vitC;
            TadalafilActive = savedSupps.tadActive;
            TadalafilDose = savedSupps.tadDose;

            // Load Biometrics
            var bio = _dbService.GetBiometrics();
            BioLeftL = bio.LeftL; BioLeftW = bio.LeftW; BioLeftH = bio.LeftH;
            BioRightL = bio.RightL; BioRightW = bio.RightW; BioRightH = bio.RightH;
            UserTotalVolume = bio.TotalVolume > 0 ? bio.TotalVolume : 30.0;

            Logs.Clear();
            var records = _dbService.GetAllLogs();
            foreach (var record in records) Logs.Add(record);
            
            ApplyFilters();
            
            CheckThermalShadow(); 
            UpdateTelemetry(); 
            UpdateCharts(); 
            UpdateChartSections(); 
            ApplyChartRange();
            UpdateHeatmap(); 
            UpdateBlackoutBanner();
        }

        [RelayCommand] private void SetAppointment() 
        { 
            if (AppointmentDate.HasValue && AppointmentTime.HasValue) 
            { 
                DateTime target = AppointmentDate.Value.Date + AppointmentTime.Value;
                _dbService.SetAppointment(new DateTimeOffset(target).ToUnixTimeSeconds()); 
                _dbService.MarkDirty(); 
                LoadData(); 
            } 
        }
        
        [RelayCommand] private void ClearAppointment() 
        { 
            _dbService.ClearAppointment(); 
            AppointmentDate = null; 
            AppointmentTime = null;
            _dbService.MarkDirty(); 
            LoadData(); 
        }

        private void UpdateBlackoutBanner()
        {
            long apptTs = _dbService.GetAppointment();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (apptTs > now)
            {
                var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A" && l.Mode != "Daily Dose").OrderByDescending(l => l.Timestamp).ToList();
                long lastReleaseTs = releaseLogs.Count > 0 ? releaseLogs[0].Timestamp : now;
                var compliance = _telemetryAnalysis.CheckClinicalCompliance(apptTs, lastReleaseTs, now);

                BlackoutMessage = compliance.ComplianceMessage;

                var apptDate = DateTimeOffset.FromUnixTimeSeconds(apptTs).ToLocalTime();
                var windowStart = apptDate.AddHours(-72);
                var windowEnd = apptDate.AddHours(-48);
                
                ClinicalClearanceTarget = $"TARGET CLEARANCE: {windowStart:MMM dd, h:mm tt} — {windowEnd:MMM dd, h:mm tt}";

                ClinicalComplianceMessage = compliance.BlackoutStage switch
                {
                    3 => "SUB-48H WARNING",
                    2 => "WHO: OPTIMAL",
                    1 => "WHO: ACCEPTABLE",
                    _ => "CLINICAL LOCK"
                };
                
                ClinicalComplianceColor = compliance.ComplianceColorHex;

                BlackoutColor = compliance.BlackoutStage switch
                {
                    3 => "#4a0e0e", 
                    2 => "#0f3813", 
                    1 => "#3e2723", 
                    _ => "#0d2a3a"  
                };

                BlackoutBorder = compliance.ComplianceColorHex;
                IsBlackoutActive = true;
            }
            else
            {
                IsBlackoutActive = false;
                ClinicalComplianceMessage = "";
                ClinicalClearanceTarget = "";
            }
        }

        private bool GetSupplementSaturation(long targetTs, string suppKey, int daysBack)
        {
            return _supplementSaturation
                .Calculate(Logs.ToList(), targetTs, suppKey, daysBack)
                .IsSaturated;
        }

        private string EstimateBabyMakingVolume(long timestamp)
        {
            return _telemetryAnalysis.EstimateVolume(
                Logs.ToList(),
                timestamp,
                MinHours,
                MaxHours,
                GetSupplementSaturation);
        }

        [RelayCommand]
        private void LogDailySupplements()
        {
            int tDose = TadalafilActive ? TadalafilDose : 0;
            string supps = $"{{\"zinc\":{(ZincActive ? 1 : 0)},\"maca\":{(MacaActive ? 1 : 0)},\"vitD\":{(VitDActive ? 1 : 0)},\"vitC\":{(VitCActive ? 1 : 0)},\"tadalafil\":{tDose}}}";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var log = new LogRecord 
            { 
                Timestamp = timestamp, 
                Mode = "Daily Dose", 
                Volume = "None", 
                ReleaseCount = 0, 
                VolumeConfidence = VolumeConfidence.Unknown, 
                HeatFlag = SelectedHeat,
                Supplements = supps, 
                ClinicalVol = 0, Concentration = 0, Motility = 0, ProgMotility = 0, Morphology = 0, PhLevel = 0, 
                Notes = SelectedNotes 
            };

            _dbService.InsertLog(log); 
            _dbService.SaveSupplementsState(ZincActive, MacaActive, VitDActive, VitCActive, TadalafilActive, TadalafilDose);
            _dbService.MarkDirty();

            LoadData(); 
            SelectedNotes = ""; 
            DatabaseService.ShowNotification("Supplements Tracked", "Daily dose recorded without resetting recovery telemetry.");
        }

        [RelayCommand]
        private void LogEvent(string mode)
        {
            int tDose = TadalafilActive ? TadalafilDose : 0;
            string supps = $"{{\"zinc\":{(ZincActive ? 1 : 0)},\"maca\":{(MacaActive ? 1 : 0)},\"vitD\":{(VitDActive ? 1 : 0)},\"vitC\":{(VitCActive ? 1 : 0)},\"tadalafil\":{tDose}}}";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (!ValidateLogInput(mode, ClinicalVol, Concentration, Motility, ProgMotility, Morphology, PhLevel, timestamp, null, SelectedReleaseCount, SelectedVolumeConfidence))
            {
                return;
            }
            
            string vol = SelectedVolume;
            if (mode == "Clinical-Lab" && (vol == "None" || vol == "N/A")) vol = "Normal";
            if (mode == "Baby-Making") vol = EstimateBabyMakingVolume(timestamp);
            var confidence = mode == "Baby-Making" ? VolumeConfidence.Estimated : SelectedVolumeConfidence;
            
            var log = new LogRecord { Timestamp = timestamp, Mode = mode, Volume = vol, ReleaseCount = SelectedReleaseCount, VolumeConfidence = confidence, HeatFlag = SelectedHeat, Supplements = supps, ClinicalVol = ClinicalVol ?? 0.0, Concentration = Concentration ?? 0, Motility = Motility ?? 0, ProgMotility = ProgMotility ?? 0, Morphology = Morphology ?? 0, PhLevel = PhLevel ?? 0.0, Notes = SelectedNotes };

            _dbService.InsertLog(log); 
            _dbService.SaveSupplementsState(ZincActive, MacaActive, VitDActive, VitCActive, TadalafilActive, TadalafilDose);
            _dbService.MarkDirty();

            LoadData(); ClearForm(); DatabaseService.ShowNotification("Event Logged", $"Successfully recorded {mode} event.");
        }

        [RelayCommand]
        private void SubmitManualLog()
        {
            if (!ManualDate.HasValue || !ManualTime.HasValue) return;
            DateTime dt = ManualDate.Value.Date + ManualTime.Value; 
            long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();

            if (ManualMode == "Daily Dose") 
            { 
                ManualVolume = "None"; 
                ManualReleaseCount = 0; 
            }
            else if (ManualMode == "Clinical-Lab" && (ManualVolume == "None" || ManualVolume == "N/A")) 
            {
                ManualVolume = "Normal";
            }
            else if (ManualMode == "Baby-Making") 
            {
                ManualVolume = EstimateBabyMakingVolume(ts);
            }

            if (!ValidateLogInput(ManualMode, ManualClinicalVol, ManualConcentration, ManualMotility, ManualProgMotility, ManualMorphology, ManualPhLevel, ts, null, ManualReleaseCount, ManualVolumeConfidence))
            {
                return;
            }

            int z = ManualZinc ? 1 : 0, m = ManualMaca ? 1 : 0, d = ManualVitD ? 1 : 0, c = ManualVitC ? 1 : 0, tDose = ManualTadalafil ? ManualTadalafilDose : 0;
            var confidence = ManualMode == "Baby-Making" ? VolumeConfidence.Estimated : ManualVolumeConfidence;

            var newLog = new LogRecord { Timestamp = ts, Mode = ManualMode, Volume = ManualVolume, ReleaseCount = ManualReleaseCount, VolumeConfidence = confidence, HeatFlag = ManualHeat, Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c},\"tadalafil\":{tDose}}}", ClinicalVol = ManualClinicalVol ?? 0.0, Concentration = ManualConcentration ?? 0, Motility = ManualMotility ?? 0, ProgMotility = ManualProgMotility ?? 0, Morphology = ManualMorphology ?? 0, PhLevel = ManualPhLevel ?? 0.0, Notes = ManualNotes };
            
            _dbService.InsertLog(newLog, ManualLabFileName, _manualLabFileData);
            _dbService.MarkDirty();

            ManualLabFileName = null; _manualLabFileData = null; CloseManualLog(); LoadData();
        }

        [RelayCommand]
        private void OpenEditLog(long id)
        {
            var log = Logs.FirstOrDefault(l => l.Id == id);
            if (log == null) return;
            EditId = log.Id; var dtOffset = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime(); EditDate = dtOffset.Date; EditTime = dtOffset.TimeOfDay;
            EditMode = log.Mode; EditVolume = log.Volume; EditReleaseCount = log.ReleaseCount; EditVolumeConfidence = log.VolumeConfidence; EditHeat = log.HeatFlag;
            EditZinc = log.Supplements.Contains("\"zinc\":1"); 
            EditMaca = log.Supplements.Contains("\"maca\":1"); 
            EditVitD = log.Supplements.Contains("\"vitD\":1"); 
            EditVitC = log.Supplements.Contains("\"vitC\":1");
            
            int parsedTadDose = ParseTadalafilDose(log.Supplements);
            EditTadalafil = parsedTadDose > 0;
            EditTadalafilDose = parsedTadDose > 0 ? parsedTadDose : 10;
            
            EditClinicalVol = log.ClinicalVol > 0 ? log.ClinicalVol : null; EditConcentration = log.Concentration > 0 ? log.Concentration : null; EditMotility = log.Motility > 0 ? log.Motility : null; EditProgMotility = log.ProgMotility > 0 ? log.ProgMotility : null; EditMorphology = log.Morphology > 0 ? log.Morphology : null; EditPhLevel = log.PhLevel > 0 ? log.PhLevel : null;
            EditNotes = log.Notes ?? "";
            var history = _dbService.GetLogEditHistory(log.Id);
            EditHistoryEntries.Clear();
            foreach (var entry in history) EditHistoryEntries.Add(entry);
            EditHistoryText = history.Count == 0
                ? "No previous edits recorded."
                : string.Join(Environment.NewLine, history.Select(entry => entry.DisplayText));
            EditLabFileName = null; _editLabFileData = null; IsEditLogOpen = true;
        }

        [RelayCommand]
        private void RestoreEditHistory(long historyId)
        {
            var restored = _dbService.RestoreLogFromHistory(historyId);
            if (restored == null)
            {
                ShowAlert("Restore Unavailable", "The selected edit history entry no longer has a restorable snapshot.");
                return;
            }

            _dbService.UpdateLog(restored);
            _dbService.MarkDirty();
            CloseEditLog();
            LoadData();
        }

        [RelayCommand]
        private void SaveEditLog()
        {
            if (!EditDate.HasValue || !EditTime.HasValue) return;
            DateTime dt = EditDate.Value.Date + EditTime.Value; 
            long ts = new DateTimeOffset(dt).ToUnixTimeSeconds();

            if (EditMode == "Daily Dose") 
            { 
                EditVolume = "None"; 
                EditReleaseCount = 0; 
            }
            else if (EditMode == "Clinical-Lab" && (EditVolume == "None" || EditVolume == "N/A")) 
            {
                EditVolume = "Normal";
            }

            if (!ValidateLogInput(EditMode, EditClinicalVol, EditConcentration, EditMotility, EditProgMotility, EditMorphology, EditPhLevel, ts, EditId, EditReleaseCount, EditVolumeConfidence))
            {
                return;
            }

            int z = EditZinc ? 1 : 0, m = EditMaca ? 1 : 0, d = EditVitD ? 1 : 0, c = EditVitC ? 1 : 0, tDose = EditTadalafil ? EditTadalafilDose : 0;
            var confidence = EditMode == "Baby-Making" ? VolumeConfidence.Estimated : EditVolumeConfidence;

            var updatedLog = new LogRecord { Id = EditId, Timestamp = ts, Mode = EditMode, Volume = EditVolume, ReleaseCount = EditReleaseCount, VolumeConfidence = confidence, HeatFlag = EditHeat, Supplements = $"{{\"zinc\":{z},\"maca\":{m},\"vitD\":{d},\"vitC\":{c},\"tadalafil\":{tDose}}}", ClinicalVol = EditClinicalVol ?? 0.0, Concentration = EditConcentration ?? 0, Motility = EditMotility ?? 0, ProgMotility = EditProgMotility ?? 0, Morphology = EditMorphology ?? 0, PhLevel = EditPhLevel ?? 0.0, Notes = EditNotes };
            
            _dbService.UpdateLog(updatedLog, EditLabFileName, _editLabFileData);
            _dbService.MarkDirty();

            CloseEditLog(); LoadData();
        }

        [RelayCommand] private void DeleteLog(long id) { ShowAlert("Confirm Delete", "Are you sure you want to permanently purge this record?", () => { _dbService.DeleteLog(id); _dbService.MarkDirty(); LoadData(); }); }

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

            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A" && l.Mode != "Daily Dose").OrderBy(l => l.Timestamp).ToList();
            var uncompromisedLogs = new List<LogRecord>();
            foreach (var l in releaseLogs) { long shadowWindow = l.Timestamp - (74 * 24 * 3600); if (!Logs.Any(x => x.Timestamp >= shadowWindow && x.Timestamp < l.Timestamp && x.HeatFlag >= 2)) uncompromisedLogs.Add(l); }
            if (uncompromisedLogs.Count < 2) { ShowAlert("Error", "Not enough uncompromised release data points to calibrate."); return; }

            var validGaps = new List<double>(); var allGaps = new List<double>();
            for (int i = 0; i < uncompromisedLogs.Count - 1; i++) { double gap = (uncompromisedLogs[i + 1].Timestamp - uncompromisedLogs[i].Timestamp) / 3600.0; allGaps.Add(gap); if ((uncompromisedLogs[i + 1].Mode == "Maintenance" || uncompromisedLogs[i + 1].Mode == "Clinical-Lab") && (uncompromisedLogs[i + 1].Volume == "Normal" || uncompromisedLogs[i + 1].Volume == "High")) validGaps.Add(gap); }
            if (validGaps.Count == 0) { ShowAlert("Error", "No successful uncompromised recoveries recorded yet."); return; }

            double roundedMin = Math.Round(validGaps.Average(), 1); 
            double meanAll = allGaps.Average(); 
            double stdDev = Math.Sqrt(allGaps.Sum(val => Math.Pow(val - meanAll, 2)) / allGaps.Count);
            double recommendedMax = Math.Round(meanAll + stdDev, 1); 
            if (recommendedMax > 120.0) recommendedMax = 120.0;

            // --- LAB-DRIVEN CALIBRATION ENGINE ---
            var latestLab = Logs.Where(l => l.Mode == "Clinical-Lab" && l.Concentration > 0).OrderByDescending(l => l.Timestamp).FirstOrDefault();
            string labFeedback = "";
            
            if (latestLab != null)
            {
                // WHO Criteria for sub-optimal: < 15M/mL conc, < 40% tot motility, < 32% prog motility
                if (latestLab.Concentration < 15 || latestLab.ProgMotility < 32 || latestLab.Motility < 40)
                {
                    roundedMin = Math.Round(roundedMin * 1.15, 1); // +15% penalty extension
                    labFeedback = "\n\n[LAB CALIBRATION] Recent sub-optimal clinical metrics (WHO criteria) applied a +15% extension to the minimum recovery floor.";
                }
                // Optimal turnover criteria
                else if (latestLab.Concentration >= 40 && latestLab.ProgMotility >= 50)
                {
                    roundedMin = Math.Round(roundedMin * 0.90, 1); // -10% bonus reduction
                    labFeedback = "\n\n[LAB CALIBRATION] Recent pristine clinical metrics applied a -10% reduction to the minimum recovery floor.";
                }
            }
            // -------------------------------------

            ShowAlert("Auto-Calibrate", $"Calibration Complete.\n(Thermal Shadow events excluded)\n\n[FLOOR] Minimum recovery gap: {roundedMin} hours.{labFeedback}\n[CEILING] Max endurance: {recommendedMax} hours.\n\nUpdate your threshold settings?", () => { MinHours = roundedMin; MaxHours = recommendedMax; SaveSettings(); });
        }

        [RelayCommand]
        private void AnalyzeSupplements()
        {
            var validLogs = Logs.Where(l => l.Mode != "Daily Dose").ToList();
            if (validLogs.Count < 10) { ShowAlert("Error", "Need more data points to run statistical analysis. Keep logging!"); return; }

            var analysis = _supplementAnalysis.Analyze(
                validLogs,
                (timestamp, supplement, days) => _supplementSaturation.Calculate(Logs.ToList(), timestamp, supplement, days));
            if (analysis.InsufficientData)
            {
                ShowAlert("Analysis Needs More Data", "Too few uncompromised release events remain after thermal-shadow filtering.");
                return;
            }

            ZincAnalysisSummary = FormatAnalysisCard("Zinc", analysis.Zinc);
            MacaAnalysisSummary = FormatAnalysisCard("Maca", analysis.Maca);
            VitaminDAnalysisSummary = FormatAnalysisCard("Vitamin D3", analysis.VitaminD);
            VitaminCAnalysisSummary = FormatAnalysisCard("Vitamin C", analysis.VitaminC);
            AnalysisMessage = FormatSupplementAnalysis(analysis);
            IsAnalysisOpen = true;
            CloseSettings();
        }

        [RelayCommand]
        private void OpenDelta()
        {
            ClinicalLogs.Clear();
            var labs = Logs.Where(l => l.Mode == "Clinical-Lab").OrderByDescending(l => l.Timestamp).ToList();
            foreach (var lab in labs) ClinicalLogs.Add(lab);
            
            if (ClinicalLogs.Count >= 2)
            {
                SelectedLabA = ClinicalLogs[1]; 
                SelectedLabB = ClinicalLogs[0]; 
                CalculateDelta();
            }
            else
            {
                SelectedLabA = ClinicalLogs.FirstOrDefault();
                SelectedLabB = null;
                DeltaResult = null;
                DeltaChartSeries = Array.Empty<ISeries>();
            }
            
            IsDeltaOpen = true;
            CloseSettings(); 
        }

        [RelayCommand]
        private void CalculateDelta()
        {
            if (SelectedLabA == null || SelectedLabB == null || SelectedLabA.Id == SelectedLabB.Id)
            {
                DeltaResult = null;
                DeltaChartSeries = Array.Empty<ISeries>();
                return;
            }
            DeltaResult = _clinicalDelta.AnalyzeDelta(Logs.ToList(), SelectedLabA.Id, SelectedLabB.Id);
            UpdateDeltaChart();
        }

        private void UpdateDeltaChart()
        {
            if (DeltaResult == null) return;

            var labA = DeltaResult.LabA;
            var labB = DeltaResult.LabB;

            double CalcPct(double a, double b) => a > 0 ? ((b - a) / a) * 100.0 : 0;

            var pctVol = CalcPct(labA.ClinicalVol, labB.ClinicalVol);
            var pctConc = CalcPct(labA.Concentration, labB.Concentration);
            var pctMot = CalcPct(labA.Motility, labB.Motility);
            var pctProg = CalcPct(labA.ProgMotility, labB.ProgMotility);
            var pctMorph = CalcPct(labA.Morphology, labB.Morphology);

            var values = new[] { pctVol, pctConc, pctMot, pctProg, pctMorph };

            var columnSeries = new ColumnSeries<double>
            {
                Values = values,
                Name = "% Change",
                DataLabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255)),
                DataLabelsSize = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                DataLabelsFormatter = point => $"{point.Model:+0.0;-0.0;0}%",
                Stroke = null
            };

            columnSeries.PointMeasured += (point) =>
            {
                if (point.Visual == null) return;
                point.Visual.Fill = point.Model >= 0 
                    ? new SolidColorPaint(new SKColor(76, 175, 80))  
                    : new SolidColorPaint(new SKColor(211, 47, 47)); 
            };

            DeltaChartSeries = new ISeries[] { columnSeries };

            DeltaXAxes = new[]
            {
                new Axis
                {
                    Labels = new[] { "Volume", "Conc.", "Tot Mot", "Prog Mot", "Morph" },
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 11
                }
            };

            DeltaYAxes = new[]
            {
                new Axis
                {
                    Labeler = value => $"{value}%",
                    LabelsPaint = new SolidColorPaint(new SKColor(136, 136, 136)),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(51, 51, 51))
                }
            };
        }

        private void UpdatePharmacokineticChart()
        {
            if (Logs.Count == 0)
            {
                PkSeries = Array.Empty<ISeries>();
                return;
            }

            double startTs = _chartMinX;
            double endTs = _chartMaxX;

            if (startTs <= 0 || endTs <= startTs)
            {
                startTs = DateTimeOffset.FromUnixTimeSeconds(Logs.Min(l => l.Timestamp) - 86400).ToLocalTime().Ticks;
                endTs = DateTimeOffset.FromUnixTimeSeconds(Logs.Max(l => l.Timestamp) + 86400).ToLocalTime().Ticks;
            }

            var minLocal = new DateTime((long)startTs);
            var maxLocal = new DateTime((long)endTs);
            
            long startUnix = new DateTimeOffset(minLocal, TimeZoneInfo.Local.GetUtcOffset(minLocal)).ToUnixTimeSeconds();
            long endUnix = new DateTimeOffset(maxLocal, TimeZoneInfo.Local.GetUtcOffset(maxLocal)).ToUnixTimeSeconds();

            var curves = _pkService.CalculateCurves(Logs.ToList(), startUnix, endUnix);

            if (curves.Count == 0 || curves.Values.All(c => c.Count == 0))
            {
                PkSeries = Array.Empty<ISeries>();
                return;
            }

            foreach (var curve in curves.Values)
            {
                foreach(var pt in curve)
                {
                    if (pt.X.HasValue)
                        pt.X = DateTimeOffset.FromUnixTimeSeconds((long)pt.X.Value).ToLocalTime().Ticks;
                }
            }

            PkSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = curves["Zinc"],
                    Name = "Zinc Saturation",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(0, 122, 204)) { StrokeThickness = 2 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(0, 122, 204)),
                    GeometryStroke = new SolidColorPaint(new SKColor(0, 122, 204)),
                    LineSmoothness = 0.8
                },
                new LineSeries<ObservablePoint>
                {
                    Values = curves["Maca"],
                    Name = "Maca Kinetics",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(156, 39, 176)) { StrokeThickness = 2 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(156, 39, 176)),
                    GeometryStroke = new SolidColorPaint(new SKColor(156, 39, 176)),
                    LineSmoothness = 0.8
                },
                new LineSeries<ObservablePoint>
                {
                    Values = curves["VitD"],
                    Name = "Vitamin D3",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(76, 175, 80)) { StrokeThickness = 2 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(76, 175, 80)),
                    GeometryStroke = new SolidColorPaint(new SKColor(76, 175, 80)),
                    LineSmoothness = 0.8
                },
                new LineSeries<ObservablePoint>
                {
                    Values = curves["VitC"],
                    Name = "Vitamin C",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0)) { StrokeThickness = 2 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(245, 124, 0)),
                    GeometryStroke = new SolidColorPaint(new SKColor(245, 124, 0)),
                    LineSmoothness = 0.8
                },
                new LineSeries<ObservablePoint>
                {
                    Values = curves.ContainsKey("Tadalafil") ? curves["Tadalafil"] : new List<ObservablePoint>(),
                    Name = "Tadalafil",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(229, 57, 53)) { StrokeThickness = 2 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(229, 57, 53)),
                    GeometryStroke = new SolidColorPaint(new SKColor(229, 57, 53)),
                    LineSmoothness = 0.8
                }
            };

            PkXAxes = new[] { 
                new Axis { 
                    Labeler = value => { 
                        try { return new DateTime((long)value).ToString("MMM"); } 
                        catch { return string.Empty; } 
                    }, 
                    LabelsPaint = new SolidColorPaint(new SKColor(136, 136, 136)), 
                    TextSize = 11,
                    MinStep = TimeSpan.FromDays(30).Ticks,
                    MinLimit = startTs, 
                    MaxLimit = endTs,
                    SeparatorsPaint = null, 
                    TicksPaint = null 
                } 
            };

            PkYAxes = new[] { 
                new Axis { 
                    LabelsPaint = null, 
                    MinLimit = 0,
                    SeparatorsPaint = null,
                    TicksPaint = null 
                } 
            };
        }

        private void UpdateDiurnalInsight()
        {
            var releaseLogs = Logs.Where(l => l.Mode != "Daily Dose").ToList();
            if (releaseLogs.Count < 5)
            {
                DiurnalPeakText = "Awaiting data to calculate peak biological window.";
                return;
            }

            var hourBuckets = new double[24];
            foreach (var log in releaseLogs)
            {
                if (log.Volume == "Normal" || log.Volume == "High" || log.Mode == "Clinical-Lab")
                {
                    int hour = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime().Hour;
                    double score = (log.Volume == "High" || log.Mode == "Clinical-Lab") ? 1.5 : 1.0;
                    hourBuckets[hour] += score;
                }
            }

            double maxWindow = 0;
            int bestStartHour = 0;
            for (int i = 0; i < 24; i++)
            {
                double windowSum = hourBuckets[i] + hourBuckets[(i + 1) % 24] + hourBuckets[(i + 2) % 24];
                if (windowSum > maxWindow)
                {
                    maxWindow = windowSum;
                    bestStartHour = i;
                }
            }

            if (maxWindow > 0)
            {
                int endHour = (bestStartHour + 3) % 24;
                DiurnalPeakText = $"* Historical data indicates peak yield window is {bestStartHour:D2}:00 - {endHour:D2}:00.";
            }
            else
            {
                DiurnalPeakText = "";
            }
        }

        private static string FormatAnalysisCard(string label, SupplementComparison comparison)
        {
            string groups = $"Saturated {comparison.SaturatedCount} | Unsaturated {comparison.UnsaturatedCount}";
            string result = comparison.SaturatedAverageGap.HasValue && comparison.UnsaturatedAverageGap.HasValue
                ? $"Gap delta: {comparison.UnsaturatedAverageGap.Value - comparison.SaturatedAverageGap.Value:+0.0;-0.0;0.0}h"
                : comparison.SaturatedSuccesses.HasValue && comparison.UnsaturatedSuccesses.HasValue
                    ? $"Yield delta: {(comparison.SaturatedSuccesses.Value / (double)Math.Max(1, comparison.SaturatedCount) - comparison.UnsaturatedSuccesses.Value / (double)Math.Max(1, comparison.UnsaturatedCount)):+0.0%;-0.0%;0.0%}"
                    : "Not comparable yet";
            return $"{label}  |  {comparison.WindowDays}-day window\n{groups}\n{result}\nConfidence: O {comparison.SaturatedConfidenceCounts.Observed}/{comparison.UnsaturatedConfidenceCounts.Observed}  E {comparison.SaturatedConfidenceCounts.Estimated}/{comparison.UnsaturatedConfidenceCounts.Estimated}  U {comparison.SaturatedConfidenceCounts.Unknown}/{comparison.UnsaturatedConfidenceCounts.Unknown}";
        }

        private static string FormatSupplementAnalysis(SupplementAnalysisResult analysis)
        {
            return "--- OBSERVED SUPPLEMENT ASSOCIATIONS ---\n\n"
                + "Personal observational comparisons only; this is not clinical evidence.\n"
                + "Thermal-shadow periods are excluded.\n\n"
                + "Saturation rule: at least 50% of release events in the stated lookback window contain the supplement.\n\n"
                + "Percentages include all observations, including sessions with estimated or unknown volume confidence.\n\n"
                + FormatYieldComparison("ZINC", analysis.Zinc)
                + FormatGapComparison("MACA ROOT", analysis.Maca)
                + FormatYieldComparison("VITAMIN D3", analysis.VitaminD)
                + FormatGapComparison("VITAMIN C", analysis.VitaminC);
        }

        private static string FormatYieldComparison(string label, SupplementComparison comparison)
        {
            string saturatedRate = comparison.SaturatedCount == 0
                ? "n/a"
                : $"{comparison.SaturatedSuccesses!.Value / (double)comparison.SaturatedCount * 100:F1}%";
            string unsaturatedRate = comparison.UnsaturatedCount == 0
                ? "n/a"
                : $"{comparison.UnsaturatedSuccesses!.Value / (double)comparison.UnsaturatedCount * 100:F1}%";
            string association = comparison.HasBothGroups
                ? $"Observed association: {(comparison.SaturatedSuccesses!.Value / (double)comparison.SaturatedCount * 100) - (comparison.UnsaturatedSuccesses!.Value / (double)comparison.UnsaturatedCount * 100):+0.0;-0.0;0.0} percentage points."
                : "Observed association: not comparable until both groups have data.";
            string caution = comparison.SaturatedCount < 5 || comparison.UnsaturatedCount < 5
                ? "Caution: small group size; treat this comparison as exploratory."
                : "";
            string audit = FormatSaturationAudit(comparison);
            return $"[{label} - {comparison.WindowDays}-DAY WINDOW]\n"
                + audit
                + $"Saturated: {comparison.SaturatedSuccesses?.ToString() ?? "n/a"}/{comparison.SaturatedCount} successful ({saturatedRate})\n"
                + $"  Confidence: {FormatConfidenceCounts(comparison.SaturatedConfidenceCounts)}\n"
                + $"  Successes by confidence: {FormatConfidenceCounts(comparison.SaturatedSuccessesByConfidence)}\n"
                + $"Unsaturated: {comparison.UnsaturatedSuccesses?.ToString() ?? "n/a"}/{comparison.UnsaturatedCount} successful ({unsaturatedRate})\n"
                + $"  Confidence: {FormatConfidenceCounts(comparison.UnsaturatedConfidenceCounts)}\n"
                + $"  Successes by confidence: {FormatConfidenceCounts(comparison.UnsaturatedSuccessesByConfidence)}\n"
                + $"{association}\n"
                + (caution.Length > 0 ? $"{caution}\n" : "")
                + "\n";
        }

        private static string FormatGapComparison(string label, SupplementComparison comparison)
        {
            string association = comparison.SaturatedAverageGap.HasValue && comparison.UnsaturatedAverageGap.HasValue
                ? $"Observed association: {comparison.UnsaturatedAverageGap.Value - comparison.SaturatedAverageGap.Value:+0.0;-0.0;0.0} hours."
                : "Observed association: not comparable until both groups have data.";
            string caution = comparison.SaturatedCount < 5 || comparison.UnsaturatedCount < 5
                ? "Caution: small group size; treat this comparison as exploratory."
                : "";
            string audit = FormatSaturationAudit(comparison);
            return $"[{label} - {comparison.WindowDays}-DAY WINDOW]\n"
                + audit
                + $"Saturated: {(comparison.SaturatedAverageGap.HasValue ? $"{comparison.SaturatedAverageGap:F1}h" : "n/a")} average gap ({comparison.SaturatedCount} gaps)\n"
                + $"  Confidence: {FormatConfidenceCounts(comparison.SaturatedConfidenceCounts)}\n"
                + $"Unsaturated: {(comparison.UnsaturatedAverageGap.HasValue ? $"{comparison.UnsaturatedAverageGap:F1}h" : "n/a")} average gap ({comparison.UnsaturatedCount} gaps)\n"
                + $"  Confidence: {FormatConfidenceCounts(comparison.UnsaturatedConfidenceCounts)}\n"
                + $"{association}\n"
                + (caution.Length > 0 ? $"{caution}\n" : "")
                + "\n";
        }

        private static string FormatConfidenceCounts(ConfidenceCounts counts) =>
            $"Observed {counts.Observed}, Estimated {counts.Estimated}, Unknown {counts.Unknown} (all {counts.Total})";

        private static string FormatSaturationAudit(SupplementComparison comparison)
        {
            if (comparison.SaturationAudits.Count == 0)
                return "Saturation audit: no comparison events available.\n";

            var windowDays = comparison.SaturationAudits[0].WindowDays;
            int releaseEvents = comparison.SaturationAudits.Sum(audit => audit.ReleaseEventCount);
            int supplementEvents = comparison.SaturationAudits.Sum(audit => audit.SupplementEventCount);
            double proportion = releaseEvents == 0 ? 0 : supplementEvents / (double)releaseEvents;
            return $"Saturation audit ({windowDays}-day windows): "
                + $"{supplementEvents}/{releaseEvents} release events across exact target windows "
                + $"({proportion:P1}); {comparison.SaturationAudits.Count} targets\n";
        }

        private void CheckThermalShadow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var shadow = _telemetryAnalysis.GetThermalShadowDetails(Logs.ToList(), now);
            if (shadow.IsActive)
            {
                IsShadowActive = true;
                ShadowProgressPercent = shadow.ProgressPercent;
                ShadowStageTitle = shadow.StageName;
                ShadowStageDesc = shadow.StageDescription;
                ShadowMessage = $"[!] SYSTEM COMPROMISED: Level {shadow.HeatLevel} Thermal Shadow active (Day {shadow.DaysElapsed}/74, {shadow.DaysRemaining}d remaining). Clears: {shadow.ClearsAt:MMM dd, yyyy}.";
            }
            else
            {
                IsShadowActive = false;
                ShadowProgressPercent = 0.0;
                ShadowStageTitle = "";
                ShadowStageDesc = "";
            }
        }
        
        [RelayCommand]
        private void DismissWankAlert()
        {
            EnableAggressiveNotifications = false;
            ShowWankAlert = false;
        }
        
        private void UpdateTelemetry()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            var releaseLogs = Logs.Where(l => l.Mode != "Daily Dose").ToList();
            
            var metrics = _telemetryAnalysis.CalculateRecoveryMetrics(
                releaseLogs,
                now,
                MinHours,
                MaxHours,
                UserTotalVolume); 
            
            if (metrics.HasRelease)
            {
                var current = TimeSpan.FromHours(metrics.CurrentHours);
                HudCurrent = $"{(int)current.TotalHours}h {current.Minutes:D2}m";
                
                if (metrics.Phase == TelemetryPhase.ViabilityFading)
                {
                    HudRemaining = $"{Math.Floor(metrics.ViabilityPercentage * 100)}%"; 
                    HudRemainingLabel = "VIABILITY (FADING)";
                }
                else if (metrics.Phase == TelemetryPhase.PeakWindow)
                {
                    HudRemaining = "100%"; 
                    HudRemainingLabel = "VIABILITY (PEAK)";
                }
                else if (metrics.Phase == TelemetryPhase.ExtendedStorage)
                {
                    HudRemaining = "100%"; 
                    HudRemainingLabel = "EXTENDED RESERVE";
                }
                else
                {
                    var remainingHours = Math.Max(0, MaxHours - metrics.CurrentHours);
                    var remaining = TimeSpan.FromHours(remainingHours);
                    HudRemaining = $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m";
                    HudRemainingLabel = "LIMIT T-MINUS";
                }

                HudPhaseLabel = metrics.PhaseLabel;
                HudPhaseColor = metrics.PhaseColorHex;
                HudProgressValue = metrics.Phase == TelemetryPhase.Recharging ? metrics.ProgressToPeak : 1.0;
                
                HudAvg = metrics.AverageGapHours.HasValue ? $"{metrics.AverageGapHours:F1}h" : "--";
                HudMax = metrics.MaximumGapHours.HasValue ? $"{metrics.MaximumGapHours:F1}h" : "--";

                double criticalFadeHours = 144.0;
                if (metrics.CurrentHours < criticalFadeHours)
                {
                    EnableAggressiveNotifications = true;
                }

                if (EnableSenescenceAlert && EnableAggressiveNotifications && metrics.CurrentHours > criticalFadeHours)
                {
                    ShowWankAlert = true;
                }
                else
                {
                    ShowWankAlert = false;
                }
            }
            else
            {
                HudCurrent = "--"; HudRemaining = "--"; HudRemainingLabel = "LIMIT T-MINUS"; HudAvg = "--"; HudMax = "--";
                HudPhaseLabel = "NO LOGS"; HudPhaseColor = "#888888"; HudProgressValue = 0.0;
                ShowWankAlert = false;
            }

            long thirtyDaysInSeconds = 2592000;
            long cutoffTimestamp = now - thirtyDaysInSeconds;
            
            var recentEvents = releaseLogs.Where(l => l.Timestamp >= cutoffTimestamp).ToList();

            if (recentEvents.Count == 0)
            {
                HudFrequency = "0.0/wk";
            }
            else
            {
                double eventsPerWeek = _telemetryAnalysis.CalculateFrequencyPerWeek(releaseLogs, now);
                HudFrequency = $"{eventsPerWeek:F1}/wk";
            }
        }

        private void UpdateCharts()
        {
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A" && l.Mode != "Daily Dose").OrderBy(l => l.Timestamp).ToList();
            
            if (releaseLogs.Count == 0)
            {
                ChartSeries = Array.Empty<ISeries>(); 
                _chartMinX = DateTime.Today.Ticks; 
                _chartMaxX = DateTime.Today.AddDays(1).Ticks;
            }
            else
            {
                var gapData = new List<ChartLogPoint>(); 
                var maintPts = new List<ChartLogPoint>(); 
                var playPts = new List<ChartLogPoint>();
                var babyPts = new List<ChartLogPoint>(); 
                var labPts = new List<ChartLogPoint>();

                for (int i = 0; i < releaseLogs.Count; i++) 
                {
                    double xVal = DateTimeOffset.FromUnixTimeSeconds(releaseLogs[i].Timestamp).ToLocalTime().Ticks;
                    bool isBaseline = i == 0;
                    double yVal = isBaseline ? MaxHours : (releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0;
                    var pt = new ChartLogPoint { X = xVal, Y = yVal, IsBaseline = isBaseline, Log = releaseLogs[i] }; gapData.Add(pt);

                    switch (releaseLogs[i].Mode) 
                    { 
                        case "Maintenance": maintPts.Add(pt); break; 
                        case "Playtime": playPts.Add(pt); break; 
                        case "Baby-Making": babyPts.Add(pt); break; 
                        case "Clinical-Lab": labPts.Add(pt); break; 
                    }
                }

                if (gapData.Count > 0) 
                { 
                    _chartMinX = gapData.First().X!.Value - TimeSpan.FromHours(12).Ticks; 
                    _chartMaxX = gapData.Last().X!.Value + TimeSpan.FromHours(12).Ticks; 
                }

                // --- FORECASTING ENGINE ---
                var predictionPts = new List<ChartLogPoint>();
                var predictionLineData = new List<ChartLogPoint>();
                
                var prediction = _telemetryAnalysis.PredictNextEvent(releaseLogs);

                if (prediction.IsValid && gapData.Count > 0)
                {
                    var lastActual = gapData.Last();
                    predictionLineData.Add(lastActual); // Connect dashed line from the last real dot

                    // Predict next 2 events using the adaptive gap
                    long predictedUnix1 = lastActual.Log.Timestamp + (long)(prediction.MeanGapHours * 3600.0);
                    long predictedUnix2 = predictedUnix1 + (long)(prediction.MeanGapHours * 3600.0);

                    // Create dummy log records for the tooltip binding
                    var p1Log = new LogRecord { Mode = "Predicted", Volume = "Adaptive Forecast", Timestamp = predictedUnix1, Supplements = "{}" };
                    var p2Log = new LogRecord { Mode = "Predicted", Volume = "Adaptive Forecast", Timestamp = predictedUnix2, Supplements = "{}" };

                    var p1 = new ChartLogPoint { X = DateTimeOffset.FromUnixTimeSeconds(predictedUnix1).ToLocalTime().Ticks, Y = prediction.MeanGapHours, IsPrediction = true, StdDev = prediction.StdDevHours, Log = p1Log };
                    var p2 = new ChartLogPoint { X = DateTimeOffset.FromUnixTimeSeconds(predictedUnix2).ToLocalTime().Ticks, Y = prediction.MeanGapHours, IsPrediction = true, StdDev = prediction.StdDevHours, Log = p2Log };

                    predictionPts.Add(p1); predictionPts.Add(p2);
                    predictionLineData.Add(p1); predictionLineData.Add(p2);

                    // Extend chart view further out to fit the predicted points
                    _chartMaxX = p2.X!.Value + TimeSpan.FromHours(12).Ticks; 
                }
                // ---------------------------

                var lineSeries = new LineSeries<ChartLogPoint> 
                { 
                    Name = "", 
                    Values = gapData, 
                    LineSmoothness = 1, 
                    Fill = new LinearGradientPaint(new[] { new SKColor(85, 85, 85, 150), new SKColor(85, 85, 85, 10) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)), 
                    Stroke = new SolidColorPaint(new SKColor(85, 85, 85)) { StrokeThickness = 2 }, 
                    GeometrySize = 0, 
                    GeometryFill = null, 
                    GeometryStroke = null, 
                    IsHoverable = false 
                };

                var predictedLineSeries = new LineSeries<ChartLogPoint>
                {
                    Name = "Trend",
                    Values = predictionLineData,
                    LineSmoothness = 0,
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0)) { StrokeThickness = 2, PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 6, 6 }) },
                    GeometrySize = 0,
                    IsHoverable = false
                };
                
                var predictedScatterSeries = new ScatterSeries<ChartLogPoint>
                {
                    Name = "Predicted",
                    Values = predictionPts,
                    GeometrySize = 10,
                    Fill = new SolidColorPaint(new SKColor(18, 18, 18)),
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0)) { StrokeThickness = 2 }
                };
                
                var maintSeries = new ScatterSeries<ChartLogPoint> 
                { 
                    Name = "", 
                    Values = maintPts, 
                    GeometrySize = 10, 
                    Fill = new SolidColorPaint(new SKColor(0, 122, 204)), 
                    Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } 
                };
                
                var playSeries = new ScatterSeries<ChartLogPoint> 
                { 
                    Name = "", 
                    Values = playPts, 
                    GeometrySize = 10, 
                    Fill = new SolidColorPaint(new SKColor(156, 39, 176)), 
                    Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } 
                };
                
                var babySeries = new ScatterSeries<ChartLogPoint> 
                { 
                    Name = "", 
                    Values = babyPts, 
                    GeometrySize = 10, 
                    Fill = new SolidColorPaint(new SKColor(76, 175, 80)), 
                    Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } 
                };
                
                var labSeries = new ScatterSeries<ChartLogPoint> 
                { 
                    Name = "", 
                    Values = labPts, 
                    GeometrySize = 10, 
                    Fill = new SolidColorPaint(new SKColor(84, 110, 122)), 
                    Stroke = new SolidColorPaint(new SKColor(30,30,30)) { StrokeThickness = 2 } 
                };

                ChartSeries = new ISeries[] { lineSeries, predictedLineSeries, maintSeries, playSeries, babySeries, labSeries, predictedScatterSeries };
                
                XAxes = new[] { 
                    new Axis 
                    { 
                        Labeler = value => 
                        { 
                            try { return new DateTime((long)value).ToString("MMM dd\nHH:mm"); } 
                            catch { return string.Empty; } 
                        }, 
                        LabelsRotation = 15, 
                        LabelsPaint = new SolidColorPaint(SKColors.Gray), 
                        TextSize = 11, 
                        MinStep = TimeSpan.FromHours(12).Ticks, 
                        MinLimit = _chartMinX, 
                        MaxLimit = _chartMaxX 
                    } 
                };
                
                YAxes = new[] { 
                    new Axis 
                    { 
                        Name = "Gap (Hrs)", 
                        LabelsPaint = new SolidColorPaint(SKColors.Gray), 
                        MinLimit = 0 
                    } 
                };
            }

            TotalEventCount = Logs.Count(l => l.Mode != "Daily Dose");
            int maint = Logs.Count(l => l.Mode == "Maintenance"); 
            int play = Logs.Count(l => l.Mode == "Playtime"); 
            int baby = Logs.Count(l => l.Mode == "Baby-Making"); 
            int lab = Logs.Count(l => l.Mode == "Clinical-Lab");

            LegendMaint = $"Maintenance ({maint})"; 
            LegendPlay = $"Playtime ({play})"; 
            LegendBaby = $"Baby-Making ({baby})"; 
            LegendLab = $"Clinical-Lab ({lab})";

            ModeSeries = new ISeries[] {
                new PieSeries<int> { Values = new[] { maint }, Name = "Maintenance", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(0, 122, 204)) },
                new PieSeries<int> { Values = new[] { play }, Name = "Playtime", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(156, 39, 176)) },
                new PieSeries<int> { Values = new[] { baby }, Name = "Baby-Making", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(76, 175, 80)) },
                new PieSeries<int> { Values = new[] { lab }, Name = "Clinical", InnerRadius = 60, HoverPushout = 0, Stroke = new SolidColorPaint(new SKColor(26,26,26)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(84, 110, 122)) }
            };

            int volHigh = Logs.Count(l => l.Volume == "High"); 
            int volNormal = Logs.Count(l => l.Volume == "Normal"); 
            int volLow = Logs.Count(l => l.Volume == "Low" && l.Mode != "Daily Dose"); 
            int volDry = Logs.Count(l => (l.Volume == "None" || l.Volume == "N/A") && l.Mode != "Daily Dose");

            VolumeSeries = new ISeries[] { 
                new RowSeries<int> 
                { 
                    Values = new[] { volDry, volLow, volNormal, volHigh }, 
                    Name = "Sessions", 
                    Stroke = null, 
                    DataLabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255)), 
                    DataLabelsSize = 12, 
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End 
                } 
            };
            
            ((RowSeries<int>)VolumeSeries[0]).PointMeasured += (point) =>
            {
                if (point.Visual == null) return;
                if (point.Index == 0) point.Visual.Fill = new SolidColorPaint(new SKColor(117, 117, 117)); // Dry
                if (point.Index == 1) point.Visual.Fill = new SolidColorPaint(new SKColor(255, 152, 0)); // Low
                if (point.Index == 2) point.Visual.Fill = new SolidColorPaint(new SKColor(0, 122, 204)); // Normal
                if (point.Index == 3) point.Visual.Fill = new SolidColorPaint(new SKColor(76, 175, 80)); // High
            };
            
            VolumeXAxes = new[] { 
                new Axis 
                { 
                    LabelsPaint = new SolidColorPaint(SKColors.Gray), 
                    TextSize = 12, 
                    MinLimit = 0 
                } 
            };
            
            VolumeYAxes = new[] { 
                new Axis 
                { 
                    Labels = new[] { "None", "Low", "Normal", "High" }, 
                    LabelsPaint = new SolidColorPaint(SKColors.Gray), 
                    TextSize = 12 
                } 
            };
            
            UpdateEnthusiasmChart();
            UpdatePharmacokineticChart();
            UpdateDiurnalInsight();
        }

        private void UpdateEnthusiasmChart()
        {
            var releaseLogs = Logs.Where(l => l.Mode != "Daily Dose").OrderBy(l => l.Timestamp).ToList();
            var today = DateTime.Today;
            
            var logsByDate = releaseLogs
                .GroupBy(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Sum(l => 
                {
                    double fluidScore = l.Volume == "High" ? 1.0 : l.Volume == "Normal" ? 0.6 : l.Volume == "Low" ? 0.3 : 0.0;
                    double neuroScore = l.ReleaseCount * 0.4;
                    return fluidScore + neuroScore;
                }));

            var prediction = _telemetryAnalysis.PredictNextEvent(releaseLogs);
            if (prediction.IsValid && releaseLogs.Count > 0)
            {
                long lastTs = releaseLogs.Last().Timestamp;
                long predictedTs1 = lastTs + (long)(prediction.MeanGapHours * 3600.0);
                long predictedTs2 = predictedTs1 + (long)(prediction.MeanGapHours * 3600.0);
                
                var pDate1 = DateTimeOffset.FromUnixTimeSeconds(predictedTs1).ToLocalTime().Date;
                var pDate2 = DateTimeOffset.FromUnixTimeSeconds(predictedTs2).ToLocalTime().Date;

                if (logsByDate.ContainsKey(pDate1)) logsByDate[pDate1] += 1.0; else logsByDate[pDate1] = 1.0;
                if (logsByDate.ContainsKey(pDate2)) logsByDate[pDate2] += 1.0; else logsByDate[pDate2] = 1.0;
            }

            var historicalPoints = new List<ObservablePoint>();
            var predictedPoints = new List<ObservablePoint>();
            var baselinePoints = new List<ObservablePoint>();
            
            int windowSize = 14; 
            double optimalScore = 336.0 / Math.Max(1.0, MinHours);
            EnthusiasmTargetScore = optimalScore;
            
            DateTime maxChartDate = new DateTime((long)_chartMaxX).Date;
            if (maxChartDate < today) maxChartDate = today;

            int totalDays = (maxChartDate - today.AddDays(-364)).Days;
            
            for (int i = 0; i <= totalDays; i++)
            {
                var targetDate = today.AddDays(-364 + i);
                double rollingSum = 0;
                
                for (int j = 0; j < windowSize; j++)
                {
                    if (logsByDate.TryGetValue(targetDate.AddDays(-j), out double score))
                        rollingSum += score;
                }
                
                long targetTs = targetDate.Ticks;
                var point = new ObservablePoint(targetTs, rollingSum);
                
                baselinePoints.Add(new ObservablePoint(targetTs, optimalScore));

                if (targetDate <= today) 
                {
                    historicalPoints.Add(point);
                }
                if (targetDate >= today) 
                {
                    predictedPoints.Add(point);
                }
            }

            EnthusiasmSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = baselinePoints,
                    Name = "Optimal Baseline",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(100, 100, 100)) 
                    { 
                        StrokeThickness = 2,
                        PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 6, 6 }) 
                    },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(100, 100, 100)),
                    GeometryStroke = new SolidColorPaint(new SKColor(100, 100, 100)),
                    LineSmoothness = 1.0,
                    IsHoverable = false 
                },
                new LineSeries<ObservablePoint>
                {
                    Values = historicalPoints,
                    Name = "14-Day Enthusiasm",
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(245, 124, 0, 180), new SKColor(245, 124, 0, 0) }, 
                        new SKPoint(0.5f, 0), 
                        new SKPoint(0.5f, 1)),
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0)) { StrokeThickness = 3 },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(245, 124, 0)),
                    GeometryStroke = new SolidColorPaint(new SKColor(245, 124, 0)),
                    LineSmoothness = 1.0 
                },
                new LineSeries<ObservablePoint>
                {
                    Values = predictedPoints,
                    Name = "Projected Enthusiasm",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(245, 124, 0, 120)) 
                    { 
                        StrokeThickness = 3,
                        PathEffect = new LiveChartsCore.SkiaSharpView.Painting.Effects.DashEffect(new float[] { 6, 6 }) 
                    },
                    GeometrySize = 4,
                    GeometryFill = new SolidColorPaint(new SKColor(245, 124, 0, 120)),
                    GeometryStroke = new SolidColorPaint(new SKColor(245, 124, 0, 120)),
                    LineSmoothness = 1.0 
                }
            };

            EnthusiasmXAxes = new[] { 
                new Axis { 
                    Labeler = value => 
                    { 
                        try { return new DateTime((long)value).ToString("MMM"); } 
                        catch { return string.Empty; } 
                    }, 
                    LabelsPaint = new SolidColorPaint(new SKColor(136, 136, 136)), 
                    TextSize = 11,
                    MinStep = TimeSpan.FromDays(30).Ticks,
                    MinLimit = _chartMinX, 
                    MaxLimit = _chartMaxX,
                    SeparatorsPaint = null, 
                    TicksPaint = null 
                } 
            };
            
            EnthusiasmYAxes = new[] { 
                new Axis { 
                    LabelsPaint = null, 
                    MinLimit = 0,
                    SeparatorsPaint = null,
                    TicksPaint = null 
                } 
            };
        }

        public void ProcessEnthusiasmHover(double chartX, double winX, double winY, double plotX, double plotW, double ctrlX, double ctrlY, double ctrlW, double ctrlH)
        {
            if (EnthusiasmSeries == null || EnthusiasmSeries.Length == 0 || EnthusiasmXAxes == null || EnthusiasmXAxes.Length == 0 || plotW <= 0)
            {
                IsTooltipVisible = false; return;
            }

            double adjustedX = chartX - plotX;
            if (adjustedX < 0 || adjustedX > plotW) { IsTooltipVisible = false; return; }

            var axis = EnthusiasmXAxes[0];
            double minTs = axis.MinLimit ?? _chartMinX;
            double maxTs = axis.MaxLimit ?? _chartMaxX;
            double ratio = adjustedX / plotW;
            double targetTs = minTs + (ratio * (maxTs - minTs));

            var allPoints = new List<ObservablePoint>();
            var actualSeries = EnthusiasmSeries.FirstOrDefault(s => s.Name == "14-Day Enthusiasm");
            var projectedSeries = EnthusiasmSeries.FirstOrDefault(s => s.Name == "Projected Enthusiasm");
            
            if (actualSeries != null && actualSeries.Values is IEnumerable<ObservablePoint> pts) 
            {
                allPoints.AddRange(pts);
            }
            if (projectedSeries != null && projectedSeries.Values is IEnumerable<ObservablePoint> projPts) 
            {
                foreach (var pt in projPts)
                {
                    if (!allPoints.Any(p => p.X == pt.X))
                        allPoints.Add(pt);
                }
            }

            if (allPoints.Count == 0) { IsTooltipVisible = false; return; }

            var closest = allPoints.OrderBy(p => Math.Abs((p.X ?? 0) - targetTs)).FirstOrDefault();

            if (closest != null && Math.Abs((closest.X ?? 0) - targetTs) < TimeSpan.FromDays(3).Ticks)
            {
                HoveredEnthusiasm = new EnthusiasmHoverData 
                { 
                    DateText = new DateTime((long)(closest.X ?? 0)).ToString("MMM dd, yyyy"), 
                    Score = closest.Y ?? 0 
                };
                HoveredTimelinePoint = null; HoveredPie = null; HoveredBar = null; HoveredPk = null;
                PositionTooltip(winX, winY, ctrlX, ctrlY, ctrlW, ctrlH, 200, 90); 
                IsTooltipVisible = true;
            }
            else IsTooltipVisible = false;
        }

        [RelayCommand]
        private void HeatmapDayClicked(HeatmapDay day)
        {
            if (day == null || string.IsNullOrEmpty(day.DateText)) return;
            if (!DateTime.TryParse(day.DateText, out var targetDate)) return;

            var dayLogs = Logs
                .Where(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date == targetDate.Date)
                .OrderBy(l => l.Timestamp)
                .ToList();

            if (dayLogs.Count > 0)
            {
                var targetLog = dayLogs[0];
                SelectedLog = targetLog;
                RequestScrollToLog?.Invoke(targetLog);
            }
            else
            {
                ShowAlert("No Records", $"No sessions or events recorded on {targetDate:MMM dd, yyyy}.");
            }
        }

        private void UpdateHeatmap()
        {
            HeatmapDays.Clear();
            var today = DateTime.Today;
            
            var logsByDate = Logs.GroupBy(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date)
                                 .ToDictionary(g => g.Key, g => g.ToList());
                                 
            int globalMaxReleases = 4; 
            if (logsByDate.Any())
            {
                int actualMax = logsByDate.Values.Max(day => day.Sum(l => l.ReleaseCount));
                if (actualMax > globalMaxReleases) 
                    globalMaxReleases = actualMax;
            }

            int heatmapSpanDays = ChartRangeDays > 0 ? ChartRangeDays : 365;

            int padding = (int)today.AddDays(-(heatmapSpanDays - 1)).DayOfWeek;
            
            for (int i = 0; i < padding; i++) 
                HeatmapDays.Add(new HeatmapDay { Level = 0, ColorHex = "#252526" });

            for (int i = heatmapSpanDays - 1; i >= 0; i--)
            {
                var targetDate = today.AddDays(-i);
                var dayData = new HeatmapDay { 
                    DateText = targetDate.ToString("MMM dd, yyyy"), 
                    Level = 0, 
                    ColorHex = "#252526", 
                    MainInfoText = "No active events logged.", 
                    ModesText = "" 
                };

                if (logsByDate.TryGetValue(targetDate, out var dayLogs) && dayLogs.Count > 0)
                {
                    if (IsVolumeMode)
                    {
                        var yieldLogs = dayLogs.Where(l => l.Volume != "None" && l.Volume != "N/A" && l.Mode != "Daily Dose").ToList();
                        if (yieldLogs.Count > 0)
                        {
                            var dominantLog = yieldLogs.OrderByDescending(l => l.Volume == "High" ? 3 : l.Volume == "Normal" ? 2 : 1).First();
                            
                            int yieldScore = dominantLog.Volume == "High" ? 3 : dominantLog.Volume == "Normal" ? 2 : 1;
                            dayData.Level = yieldScore;
                            dayData.MainInfoText = $"Max Yield: {dominantLog.Volume}";
                            dayData.ModesText = "Modes Detected: " + string.Join(", ", yieldLogs.Select(l => l.Mode).Distinct());
                            dayData.ColorHex = GetHeatmapColor(dominantLog.Mode, yieldScore, 3);
                        }
                        else if (dayLogs.Any(l => l.Mode == "Daily Dose"))
                        {
                            dayData.Level = 1;
                            dayData.MainInfoText = "Supplements Only";
                            dayData.ModesText = "Daily Dose Tracked";
                            dayData.ColorHex = GetHeatmapColor("Daily Dose", 1, 1);
                        }
                    }
                    else
                    {
                        int dailyReleaseTotal = dayLogs.Sum(l => l.ReleaseCount);
                        
                        dayData.Level = dailyReleaseTotal > 0 ? dailyReleaseTotal : 1; 
                        dayData.MainInfoText = dailyReleaseTotal > 0 ? $"Total Releases: {dailyReleaseTotal} (in {dayLogs.Count} session{(dayLogs.Count == 1 ? "" : "s")})" : "Supplements Only";
                        dayData.ModesText = "Modes Detected: " + string.Join(", ", dayLogs.Select(l => l.Mode).Distinct());
                        
                        var dominantLog = dayLogs.GroupBy(l => l.Mode).OrderByDescending(g => g.Count()).First().First();
                        dayData.ColorHex = GetHeatmapColor(dominantLog.Mode, dayData.Level, globalMaxReleases);
                    }
                }
                HeatmapDays.Add(dayData);
            }
        }

        private string GetHeatmapColor(string mode, int count, int maxCount)
        {
            if (count <= 0) return "#252526";

            (double R, double G, double B) minCol = mode switch {
                "Maintenance"  => (1, 67, 122),     
                "Playtime"     => (74, 20, 140),    
                "Baby-Making"  => (27, 94, 32),     
                "Clinical-Lab" => (38, 50, 56),
                "Daily Dose"   => (60, 75, 85),     
                _              => (1, 67, 122)
            };

            (double R, double G, double B) maxCol = mode switch {
                "Maintenance"  => (144, 202, 249),  
                "Playtime"     => (243, 229, 245),  
                "Baby-Making"  => (200, 230, 201),  
                "Clinical-Lab" => (207, 216, 220),  
                "Daily Dose"   => (144, 164, 174),
                _              => (144, 202, 249)
            };

            double intensity = maxCount <= 1 ? 1.0 : (double)(count - 1) / (maxCount - 1);
            intensity = Math.Clamp(intensity, 0.0, 1.0);

            int r = (int)Math.Round(minCol.R + (maxCol.R - minCol.R) * intensity);
            int g = (int)Math.Round(minCol.G + (maxCol.G - minCol.G) * intensity);
            int b = (int)Math.Round(minCol.B + (maxCol.B - minCol.B) * intensity);

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        [RelayCommand] private void ChartClicked(object obj)
        {
            LiveChartsCore.Kernel.ChartPoint? point = null;
            if (obj is LiveChartsCore.Kernel.ChartPoint singlePoint) point = singlePoint; else if (obj is IEnumerable<LiveChartsCore.Kernel.ChartPoint> points) point = points.FirstOrDefault();
            if (point == null) return;
            var releaseLogs = Logs.Where(l => l.Volume != "None" && l.Volume != "N/A" && l.Mode != "Daily Dose").OrderBy(l => l.Timestamp).ToList();
            if (point.Index >= 0 && point.Index < releaseLogs.Count) { var log = releaseLogs[point.Index]; SelectedLog = log; RequestScrollToLog?.Invoke(log); }
        }

        private void ClearForm() { SelectedVolume = "Normal"; SelectedReleaseCount = 1; SelectedVolumeConfidence = VolumeConfidence.Observed; SelectedHeat = 0; ClinicalVol = null; Concentration = null; Motility = null; ProgMotility = null; Morphology = null; PhLevel = null; SelectedNotes = ""; }  
        private void SetupChartAxes() { XAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; VolumeXAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray) } }; }
    }

    public class ChartLogPoint : ObservablePoint
    {
        public LogRecord Log { get; set; } = null!;
        public bool IsBaseline { get; set; }
        public bool IsPrediction { get; set; }
        public double? StdDev { get; set; }
        
        public string GapText => IsPrediction 
            ? $"Projected gap: {(Y ?? 0):F1} Hrs (± {StdDev:F1}h)" 
            : IsBaseline 
                ? $"Baseline threshold: {(Y ?? 0):F1} Hrs (no prior gap)" 
                : $"Measured gap: {(Y ?? 0):F1} Hrs";
                
        public string HeaderText => IsPrediction
            ? $"PREDICTED WINDOW: {new DateTime((long)(X ?? 0)):MMM dd, yyyy @ HH:mm}"
            : new DateTime((long)(X ?? 0)).ToString("MMM dd, yyyy @ HH:mm");
        
        public string FlagsText 
        { 
            get 
            { 
                if (IsPrediction) return "[STATISTICAL PROJECTION]";
                var flags = new List<string>(); 
                if (Log.HeatFlag > 0) flags.Add($"[HEAT L{Log.HeatFlag}]"); 
                if (Log.Supplements.Contains("\"zinc\":1")) flags.Add("[Zn]"); 
                if (Log.Supplements.Contains("\"maca\":1")) flags.Add("[Ma]"); 
                if (Log.Supplements.Contains("\"vitD\":1")) flags.Add("[D3]"); 
                if (Log.Supplements.Contains("\"vitC\":1")) flags.Add("[C]"); 
                if (Log.Supplements.Contains("\"tadalafil\"") && !Log.Supplements.Contains("\"tadalafil\":0")) flags.Add("[Tad]"); 
                return string.Join(" ", flags); 
            } 
        }
        
        public bool HasFlags => FlagsText.Length > 0;
        
        public string LabText 
        { 
            get 
            { 
                if (IsPrediction) return "";
                var lines = new List<string>(); 
                if (Log.Concentration > 0) lines.Add($"Conc: {Log.Concentration} M"); 
                if (Log.Motility > 0) lines.Add($"Mot: {Log.Motility}%"); 
                if (Log.ProgMotility > 0) lines.Add($"Prog: {Log.ProgMotility}%"); 
                if (Log.Morphology > 0) lines.Add($"Morph: {Log.Morphology}%"); 
                return string.Join(" | ", lines); 
            } 
        }
        
        public bool HasLab => Log.Mode == "Clinical-Lab" && LabText.Length > 0;
        public string ModeHex => Log.Mode switch { 
            "Maintenance" => "#007acc", 
            "Playtime" => "#9c27b0", 
            "Baby-Making" => "#4caf50", 
            "Clinical-Lab" => "#546e7a", 
            "Daily Dose" => "#607d8b", 
            "Predicted" => "#f57c00",
            _ => "#ccc" 
        };
    }

    public class PieHoverData { public string Category { get; set; } = ""; public int Count { get; set; } public string ColorHex { get; set; } = ""; public double Percentage { get; set; } }
    public class BarHoverData { public string Category { get; set; } = ""; public int Count { get; set; } public string ColorHex { get; set; } = ""; }
    public class EnthusiasmHoverData { public string DateText { get; set; } = ""; public double Score { get; set; } }
    public class PkHoverData { public string DateText { get; set; } = ""; public double Zinc { get; set; } public double Maca { get; set; } public double VitD { get; set; } public double VitC { get; set; } public double Tadalafil { get; set; } }
}
