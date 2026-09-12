using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services
{
    public enum TelemetryPhase
    {
        None,
        Recharging,       // 0h to MinHours: Count/volume rebounding
        PeakWindow,       // MinHours to MaxHours: Optimal motility, minimal DNA fragmentation
        ExtendedStorage,  // MaxHours to 120h: High count, but ROS/aging starts creeping up
        ViabilityFading   // > 120h: Elevated DFI, senescence; clearance recommended
    }

    public class RecoveryMetrics
    {
        public bool HasRelease { get; set; }
        public double CurrentHours { get; set; }
        public double? AverageGapHours { get; set; }
        public double? MaximumGapHours { get; set; }
        public TelemetryPhase Phase { get; set; }
        public string PhaseLabel { get; set; } = "";
        public string PhaseColorHex { get; set; } = "#888888";
        public double ViabilityPercentage { get; set; } = 1.0;
        public double ProgressToPeak { get; set; }
    }

    public class ThermalShadowDetails
    {
        public bool IsActive { get; set; }
        public int HeatLevel { get; set; }
        public int DaysElapsed { get; set; }
        public int DaysRemaining { get; set; }
        public double ProgressPercent { get; set; }
        public string StageName { get; set; } = "";
        public string StageDescription { get; set; } = "";
        public DateTime ClearsAt { get; set; }
    }

    public class ClinicalCompliance
    {
        public bool HasAppointment { get; set; }
        public double HoursUntilAppointment { get; set; }
        public double ProjectedAbstinenceHoursAtAppointment { get; set; }
        public bool IsWithinWhoWindow { get; set; }
        public string ComplianceMessage { get; set; } = "";
        public string ComplianceColorHex { get; set; } = "#0277bd";
        public int BlackoutStage { get; set; } // 0 = Advance Notice, 1 = Extended Yield Gate, 2 = Optimal Turnover Window, 3 = Hard Lockout
        public string BlackoutStageName { get; set; } = "";
    }

    public class TelemetryAnalysisService
    {
        public const int ThermalShadowDays = 74;

        public RecoveryMetrics CalculateRecoveryMetrics(
            IEnumerable<LogRecord> logs,
            long currentTimestamp,
            double minHours = 24.0,
            double maxHours = 72.0)
        {
            var releaseLogs = logs
                .Where(l => l.Volume != "None" && l.Volume != "N/A")
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            if (releaseLogs.Count == 0)
            {
                return new RecoveryMetrics { HasRelease = false };
            }

            var latest = releaseLogs[0];
            double elapsedHours = Math.Max(0.0, (currentTimestamp - latest.Timestamp) / 3600.0);

            double? avgGap = null;
            double? maxGap = null;
            if (releaseLogs.Count >= 2)
            {
                var gaps = new List<double>();
                for (int i = 0; i < releaseLogs.Count - 1; i++)
                {
                    double gap = (releaseLogs[i].Timestamp - releaseLogs[i + 1].Timestamp) / 3600.0;
                    if (gap > 0) gaps.Add(gap);
                }
                if (gaps.Count > 0)
                {
                    avgGap = gaps.Average();
                    maxGap = gaps.Max();
                }
            }

            var metrics = new RecoveryMetrics
            {
                HasRelease = true,
                CurrentHours = elapsedHours,
                AverageGapHours = avgGap,
                MaximumGapHours = maxGap
            };

            const double fadeStartHours = 120.0;
            const double dynamicFloor = 0.55;

            if (elapsedHours < minHours)
            {
                metrics.Phase = TelemetryPhase.Recharging;
                metrics.PhaseLabel = "RECHARGING (REBOUND)";
                metrics.PhaseColorHex = "#007acc";
                metrics.ProgressToPeak = Math.Clamp(elapsedHours / Math.Max(1.0, minHours), 0.0, 1.0);
                metrics.ViabilityPercentage = 0.85 + (0.15 * metrics.ProgressToPeak);
            }
            else if (elapsedHours <= maxHours)
            {
                metrics.Phase = TelemetryPhase.PeakWindow;
                metrics.PhaseLabel = "OPTIMAL VIABILITY (PEAK)";
                metrics.PhaseColorHex = "#4caf50";
                metrics.ProgressToPeak = 1.0;
                metrics.ViabilityPercentage = 1.0;
            }
            else if (elapsedHours <= fadeStartHours)
            {
                metrics.Phase = TelemetryPhase.ExtendedStorage;
                metrics.PhaseLabel = "EXTENDED RESERVE";
                metrics.PhaseColorHex = "#ff9800";
                metrics.ProgressToPeak = 1.0;
                metrics.ViabilityPercentage = 1.0;
            }
            else
            {
                metrics.Phase = TelemetryPhase.ViabilityFading;
                metrics.PhaseLabel = "VIABILITY (FADING)";
                metrics.PhaseColorHex = "#e53935";
                metrics.ProgressToPeak = 1.0;

                double hoursOver = elapsedHours - fadeStartHours;
                double decay = (1.0 - dynamicFloor) / (1.0 + Math.Exp(-0.04 * (hoursOver - 36.0)));
                metrics.ViabilityPercentage = Math.Max(dynamicFloor, 1.0 - decay);
            }

            return metrics;
        }

        public ThermalShadowDetails GetThermalShadowDetails(IEnumerable<LogRecord> logs, long currentTimestamp)
        {
            var logList = logs.ToList();
            var heatLogs = logList
                .Where(l => l.HeatFlag >= 2)
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            if (heatLogs.Count == 0) return new ThermalShadowDetails { IsActive = false };

            var latestHeat = heatLogs[0];
            long windowSeconds = (long)ThermalShadowDays * 86400;
            long elapsedSeconds = currentTimestamp - latestHeat.Timestamp;

            if (elapsedSeconds < 0 || elapsedSeconds > windowSeconds)
            {
                return new ThermalShadowDetails { IsActive = false };
            }

            long heatTimestamp = latestHeat.Timestamp;
            bool hasPassingLab = logList.Any(l =>
                l.Mode == "Clinical-Lab" &&
                l.Timestamp > heatTimestamp &&
                l.Concentration >= 15 &&
                l.Motility >= 40);

            if (hasPassingLab)
            {
                return new ThermalShadowDetails { IsActive = false };
            }

            int daysElapsed = (int)(elapsedSeconds / 86400);
            int daysRemaining = ThermalShadowDays - daysElapsed;
            double progress = Math.Clamp((daysElapsed / (double)ThermalShadowDays) * 100.0, 0.0, 100.0);
            var clearsAt = DateTimeOffset.FromUnixTimeSeconds(latestHeat.Timestamp + windowSeconds).ToLocalTime().DateTime;

            string stageName;
            string stageDesc;

            if (daysElapsed <= 14)
            {
                stageName = "Stage 1: Epididymal Transit";
                stageDesc = "Motility & vitality temporarily impacted; damaged cells clearing transit.";
            }
            else if (daysElapsed <= 45)
            {
                stageName = "Stage 2: Spermiogenesis Transition";
                stageDesc = "Maturing spermatids affected; morphology variations peak in this window.";
            }
            else
            {
                stageName = "Stage 3: Meiotic Regeneration";
                stageDesc = "Early cell division recovering; fresh, undamaged cohort emerging.";
            }

            return new ThermalShadowDetails
            {
                IsActive = true,
                HeatLevel = latestHeat.HeatFlag,
                DaysElapsed = daysElapsed,
                DaysRemaining = daysRemaining,
                ProgressPercent = Math.Round(progress, 1),
                StageName = stageName,
                StageDescription = stageDesc,
                ClearsAt = clearsAt
            };
        }

        public bool HasActiveThermalShadow(IEnumerable<LogRecord> logs, long now, out LogRecord? latestHeat)
        {
            var logList = logs.ToList();
            var heatLogs = logList.Where(l => l.HeatFlag >= 2).OrderByDescending(l => l.Timestamp).ToList();
            if (heatLogs.Count > 0 && (now - heatLogs[0].Timestamp) <= (ThermalShadowDays * 86400))
            {
                var candidateHeat = heatLogs[0];
                bool hasPassingLab = logList.Any(l =>
                    l.Mode == "Clinical-Lab" &&
                    l.Timestamp > candidateHeat.Timestamp &&
                    l.Concentration >= 15 &&
                    l.Motility >= 40);

                if (hasPassingLab)
                {
                    latestHeat = null;
                    return false;
                }
                latestHeat = candidateHeat;
                return true;
            }
            latestHeat = null;
            return false;
        }

        public ClinicalCompliance CheckClinicalCompliance(long appointmentTimestamp, long lastReleaseTimestamp, long nowTimestamp)
        {
            if (appointmentTimestamp <= nowTimestamp) return new ClinicalCompliance { HasAppointment = false };

            double hoursUntilAppt = (appointmentTimestamp - nowTimestamp) / 3600.0;
            double projectedAbstinence = (appointmentTimestamp - lastReleaseTimestamp) / 3600.0;

            bool isIdeal = projectedAbstinence >= 48.0 && projectedAbstinence <= 72.0;
            bool isAcceptable = projectedAbstinence >= 48.0 && projectedAbstinence <= 168.0;

            int stage;
            string stageName;
            string color;
            string msg;

            if (hoursUntilAppt <= 48.0)
            {
                stage = 3;
                stageName = "Stage 3: Hard Lockout";
                color = "#e53935";

                if (projectedAbstinence < 48.0)
                {
                    msg = $"{stageName} ACTIVE. Warning: Only ~{projectedAbstinence:F0}h abstinence projected (<48h WHO minimum). Count may read artificially low.";
                }
                else
                {
                    msg = $"{stageName} ACTIVE. Zero release permitted. Projected abstinence: ~{projectedAbstinence:F0}h (WHO Gold Standard: 48–72h).";
                    if (isIdeal) color = "#4caf50";
                }
            }
            else if (hoursUntilAppt <= 72.0)
            {
                stage = 2;
                stageName = "Stage 2: Optimal Turnover Window";
                color = "#4caf50";
                msg = $"{stageName}. Recommended final clearance session to target ~48–72h Gold Standard on test day.";
            }
            else if (hoursUntilAppt <= 120.0)
            {
                stage = 1;
                stageName = "Stage 1: Extended Yield Gate";
                color = "#ff9800";
                msg = $"{stageName}. Voluntary lock path: yields higher total volume (~72–120h), but progressive motility will slowly plateau.";
            }
            else
            {
                stage = 0;
                stageName = "Advance Notice";
                color = "#0277bd";
                double days = Math.Round(hoursUntilAppt / 24.0, 1);
                msg = $"Scheduled in {days} days. Staged blackout begins at T-Minus 5 days.";
            }

            return new ClinicalCompliance
            {
                HasAppointment = true,
                HoursUntilAppointment = hoursUntilAppt,
                ProjectedAbstinenceHoursAtAppointment = projectedAbstinence,
                IsWithinWhoWindow = isIdeal || isAcceptable,
                BlackoutStage = stage,
                BlackoutStageName = stageName,
                ComplianceMessage = msg,
                ComplianceColorHex = color
            };
        }

        public double GetDynamicViabilityFloor(IEnumerable<LogRecord> logs, double fadeStartHours) => 0.55;

        public double CalculateFrequencyPerWeek(IEnumerable<LogRecord> logs, long now)
        {
            long thirtyDaysInSeconds = 2592000;
            long cutoff = now - thirtyDaysInSeconds;
            var recent = logs.Where(l => l.Timestamp >= cutoff && l.Volume != "None" && l.Volume != "N/A").ToList();
            int totalReleases = recent.Sum(l => l.ReleaseCount);
            return (totalReleases / 30.0) * 7.0;
        }

        public string EstimateVolume(
            IEnumerable<LogRecord> logs,
            long timestamp,
            double minHours,
            double maxHours,
            Func<long, string, int, bool> getSupplementSaturation)
        {
            var prev = logs.Where(l => l.Timestamp < timestamp && l.Volume != "None" && l.Volume != "N/A")
                   .OrderByDescending(l => l.Timestamp)
                   .FirstOrDefault();
            if (prev == null) return "Normal";

            double gap = (timestamp - prev.Timestamp) / 3600.0;
            if (gap < minHours)
            {
                bool zincSaturated = getSupplementSaturation(timestamp, "zinc", 30);
                return zincSaturated ? "Normal" : "Low";
            }
            if (gap >= maxHours) return "High";
            return "Normal";
        }
    }
}
