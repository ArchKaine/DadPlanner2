using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services
{
    public class ClinicalDeltaResult
    {
        public LogRecord LabA { get; set; } = null!;
        public LogRecord LabB { get; set; } = null!;

        // Lab Deltas
        public double VolDelta => (LabB.ClinicalVol) - (LabA.ClinicalVol);
        public int ConcDelta => (LabB.Concentration) - (LabA.Concentration);
        public int MotilityDelta => (LabB.Motility) - (LabA.Motility);
        public int ProgMotilityDelta => (LabB.ProgMotility) - (LabA.ProgMotility);
        public int MorphDelta => (LabB.Morphology) - (LabA.Morphology);
        public double PhDelta => (LabB.PhLevel) - (LabA.PhLevel);

        // 74-Day Lookback Behavioral Metrics
        public double AvgGapA { get; set; }
        public double AvgGapB { get; set; }
        public double GapDelta => AvgGapB - AvgGapA;

        public int ThermalEventsA { get; set; }
        public int ThermalEventsB { get; set; }

        public double ZincSatA { get; set; }
        public double ZincSatB { get; set; }
    }

    public class ClinicalDeltaService
    {
        public ClinicalDeltaResult? AnalyzeDelta(IEnumerable<LogRecord> logs, long labIdA, long labIdB)
        {
            var logList = logs.OrderBy(l => l.Timestamp).ToList();
            var labA = logList.FirstOrDefault(l => l.Id == labIdA && l.Mode == "Clinical-Lab");
            var labB = logList.FirstOrDefault(l => l.Id == labIdB && l.Mode == "Clinical-Lab");

            if (labA == null || labB == null) return null;

            // Ensure LabA is the older lab for proper chronological delta (B - A)
            if (labA.Timestamp > labB.Timestamp)
            {
                (labA, labB) = (labB, labA);
            }

            var result = new ClinicalDeltaResult
            {
                LabA = labA,
                LabB = labB
            };

            // Calculate 74-Day Behavioral Lookback for Lab A
            CalculateLookbackMetrics(logList, labA.Timestamp, out double avgGapA, out int heatA, out double zincA);
            result.AvgGapA = avgGapA;
            result.ThermalEventsA = heatA;
            result.ZincSatA = zincA;

            // Calculate 74-Day Behavioral Lookback for Lab B
            CalculateLookbackMetrics(logList, labB.Timestamp, out double avgGapB, out int heatB, out double zincB);
            result.AvgGapB = avgGapB;
            result.ThermalEventsB = heatB;
            result.ZincSatB = zincB;

            return result;
        }

        private void CalculateLookbackMetrics(List<LogRecord> allLogs, long targetLabTs, out double avgGap, out int heatEvents, out double zincSaturation)
        {
            long lookbackTs = targetLabTs - (74 * 24 * 3600); // 74 Days prior
            var windowLogs = allLogs.Where(l => l.Timestamp >= lookbackTs && l.Timestamp < targetLabTs).ToList();

            if (windowLogs.Count < 2)
            {
                avgGap = 0; heatEvents = 0; zincSaturation = 0;
                return;
            }

            var releaseLogs = windowLogs.Where(l => l.Volume != "None" && l.Volume != "N/A").ToList();
            
            // 1. Average Gap
            if (releaseLogs.Count >= 2)
            {
                var gaps = new List<double>();
                for (int i = 1; i < releaseLogs.Count; i++)
                {
                    gaps.Add((releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0);
                }
                avgGap = gaps.Average();
            }
            else
            {
                avgGap = 0;
            }

            // 2. Thermal Events (Level 2 or 3)
            heatEvents = windowLogs.Count(l => l.HeatFlag >= 2);

            // 3. Zinc Saturation %
            int zincCount = windowLogs.Count(l => l.Supplements.Contains("\"zinc\":1"));
            zincSaturation = windowLogs.Count > 0 ? (double)zincCount / windowLogs.Count : 0.0;
        }
    }
}
