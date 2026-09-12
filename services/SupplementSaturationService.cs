using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services
{
    public class SupplementProfile
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public double HalfLifeDays { get; set; }
        public int RecommendedCycleWeeksOn { get; set; }
        public int RecommendedCycleWeeksOff { get; set; }
        public string SynergyNotes { get; set; } = "";
        public string AntagonistNotes { get; set; } = "";
    }

    public class SupplementSaturationResult
    {
        // Core fields required by ReportDocumentService, DataExportService & SupplementAnalysisService
        public long TargetTimestamp { get; set; }
        public string SupplementKey { get; set; } = "";
        public int WindowDays { get; set; }
        public int ReleaseEventCount { get; set; }
        public int SupplementEventCount { get; set; }
        public double SupplementProportion { get; set; }
        public bool IsSaturated { get; set; }

        // Enhanced pharmacokinetic and cycling telemetry
        public double KineticSaturation { get; set; }
        public double FrequencyProportion => SupplementProportion;
        public int ConsecutiveWeeksSaturated { get; set; }
        public bool IsCyclingRecommended { get; set; }
        public string CyclingMessage { get; set; } = "";
        public string ClinicalNote { get; set; } = "";

        public SupplementSaturationResult() { }

        public SupplementSaturationResult(
            long targetTimestamp,
            string supplementKey,
            int windowDays,
            int releaseEventCount,
            int supplementEventCount,
            double supplementProportion,
            bool isSaturated)
        {
            TargetTimestamp = targetTimestamp;
            SupplementKey = supplementKey;
            WindowDays = windowDays;
            ReleaseEventCount = releaseEventCount;
            SupplementEventCount = supplementEventCount;
            SupplementProportion = supplementProportion;
            IsSaturated = isSaturated;
        }
    }

    public class SupplementSaturationService
    {
        private static readonly Dictionary<string, SupplementProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["zinc"] = new SupplementProfile
            {
                Key = "zinc",
                DisplayName = "Zinc",
                HalfLifeDays = 12.0,
                RecommendedCycleWeeksOn = 0,
                SynergyNotes = "Supports testosterone synthesis, seminal volume, and sperm chromatin stability.",
                AntagonistNotes = "Prolonged high-dose intake competitively blocks copper absorption. Ensure dietary balance."
            },
            ["maca"] = new SupplementProfile
            {
                Key = "maca",
                DisplayName = "Maca Root",
                HalfLifeDays = 0.85,
                RecommendedCycleWeeksOn = 8,
                RecommendedCycleWeeksOff = 1,
                SynergyNotes = "Adaptogenic modulation of the HPG axis; enhances libido and seminal yield.",
                AntagonistNotes = "Receptor sensitivity can diminish with uninterrupted long-term use. Periodic 1-week washouts restore response."
            },
            ["vitD"] = new SupplementProfile
            {
                Key = "vitD",
                DisplayName = "Vitamin D3",
                HalfLifeDays = 18.0,
                RecommendedCycleWeeksOn = 0,
                SynergyNotes = "Correlates directly with total and progressive sperm motility. Fat-soluble: take with healthy fats.",
                AntagonistNotes = "Requires adequate Magnesium and Vitamin K2 for proper calcium routing."
            },
            ["vitC"] = new SupplementProfile
            {
                Key = "vitC",
                DisplayName = "Vitamin C",
                HalfLifeDays = 1.0,
                RecommendedCycleWeeksOn = 0,
                SynergyNotes = "Potent seminal antioxidant; prevents sperm agglutination and shields DNA from reactive oxygen species.",
                AntagonistNotes = "Excessive doses (>2000mg) can cause GI distress and act as a pro-oxidant."
            }
        };

        public SupplementSaturationResult Calculate(
            IEnumerable<LogRecord> logs,
            long targetTimestamp,
            string suppKey,
            int daysBack)
        {
            string cleanKey = NormalizeKey(suppKey);
            Profiles.TryGetValue(cleanKey, out var profile);
            profile ??= new SupplementProfile { Key = cleanKey, DisplayName = suppKey, HalfLifeDays = 7.0 };

            long windowSeconds = (long)daysBack * 86400;
            long windowStartTs = targetTimestamp - windowSeconds;

            var windowLogs = logs
                .Where(l => l.Timestamp >= windowStartTs && l.Timestamp <= targetTimestamp)
                .OrderBy(l => l.Timestamp)
                .ToList();

            var releaseEvents = windowLogs
                .Where(l => l.Volume != "None" && l.Volume != "N/A")
                .ToList();

            int releaseCount = releaseEvents.Count;
            int suppCount = releaseEvents.Count(l => HasSupplement(l, cleanKey));
            double proportion = releaseCount == 0 ? 0.0 : (double)suppCount / releaseCount;

            bool isFreqSaturated = releaseCount >= 2 && proportion >= 0.5;
            double kineticLevel = CalculatePharmacokineticLevel(windowLogs, targetTimestamp, cleanKey, profile.HalfLifeDays, daysBack);

            bool finalIsSaturated = isFreqSaturated || kineticLevel >= 0.50;

            int weeksSaturated = CalculateConsecutiveWeeksSaturated(logs, targetTimestamp, cleanKey);
            bool cyclingRec = profile.RecommendedCycleWeeksOn > 0 && weeksSaturated >= profile.RecommendedCycleWeeksOn;
            string cyclingMsg = cyclingRec
                ? $"Tolerance Notice: Saturated for ~{weeksSaturated} consecutive weeks. A {profile.RecommendedCycleWeeksOff}-week washout is recommended to restore receptor sensitivity."
                : "";

            return new SupplementSaturationResult(
                targetTimestamp,
                cleanKey,
                daysBack,
                releaseCount,
                suppCount,
                proportion,
                finalIsSaturated)
            {
                KineticSaturation = Math.Round(kineticLevel, 2),
                ConsecutiveWeeksSaturated = weeksSaturated,
                IsCyclingRecommended = cyclingRec,
                CyclingMessage = cyclingMsg,
                ClinicalNote = profile.SynergyNotes
            };
        }

        private static double CalculatePharmacokineticLevel(
            List<LogRecord> logs,
            long targetTimestamp,
            string suppKey,
            double halfLifeDays,
            int daysBack)
        {
            if (logs.Count == 0) return 0.0;

            double k = Math.Log(2.0) / Math.Max(0.5, halfLifeDays);
            double currentLevel = 0.0;
            DateTime targetDate = DateTimeOffset.FromUnixTimeSeconds(targetTimestamp).ToLocalTime().Date;
            DateTime startDate = targetDate.AddDays(-daysBack);

            var dailyDoses = logs
                .Where(l => HasSupplement(l, suppKey))
                .GroupBy(l => DateTimeOffset.FromUnixTimeSeconds(l.Timestamp).ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => 1.0);

            for (DateTime day = startDate; day <= targetDate; day = day.AddDays(1))
            {
                currentLevel *= Math.Exp(-k);
                if (dailyDoses.ContainsKey(day))
                {
                    currentLevel += 1.0;
                }
            }

            double maxSteadyState = 1.0 / (1.0 - Math.Exp(-k));
            return Math.Clamp(currentLevel / maxSteadyState, 0.0, 1.0);
        }

        private static int CalculateConsecutiveWeeksSaturated(IEnumerable<LogRecord> logs, long targetTs, string suppKey)
        {
            int consecutiveWeeks = 0;
            long currentEnd = targetTs;

            for (int w = 0; w < 24; w++)
            {
                long weekStart = currentEnd - (7 * 86400);
                var weekLogs = logs.Where(l => l.Timestamp >= weekStart && l.Timestamp <= currentEnd).ToList();
                var releases = weekLogs.Where(l => l.Volume != "None" && l.Volume != "N/A").ToList();

                if (releases.Count == 0) break;
                int supps = releases.Count(l => HasSupplement(l, suppKey));

                if (supps / (double)releases.Count >= 0.5)
                {
                    consecutiveWeeks++;
                    currentEnd = weekStart;
                }
                else
                {
                    break;
                }
            }

            return consecutiveWeeks;
        }

        public static bool HasSupplement(LogRecord log, string key)
        {
            if (string.IsNullOrEmpty(log.Supplements)) return false;
            string needle = $"\"{NormalizeKey(key)}\":1";
            return log.Supplements.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeKey(string key) => key.ToLowerInvariant() switch
        {
            "zinc" or "zn" => "zinc",
            "maca" or "ma" => "maca",
            "vitd" or "d3" or "vitamind" => "vitD",
            "vitc" or "c" or "vitaminc" => "vitC",
            _ => key
        };
    }
}
