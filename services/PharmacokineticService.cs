using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore.Defaults;
using DadPlanner2.Models;

namespace DadPlanner2.Services
{
    public class PharmacokineticService
    {
        // Half-lives in hours
        private const double MacaHalfLifeHours = 20.0;
        private const double ZincHalfLifeHours = 288.0; // 12 Days
        private const double VitDHalfLifeHours = 432.0; // 18 Days
        private const double VitCHalfLifeHours = 24.0;

        public Dictionary<string, List<ObservablePoint>> CalculateCurves(IEnumerable<LogRecord> logs, double startTimestamp, double endTimestamp, double stepHours = 12.0)
        {
            var logList = logs.OrderBy(l => l.Timestamp).ToList();
            var results = new Dictionary<string, List<ObservablePoint>>
            {
                { "Maca", new List<ObservablePoint>() },
                { "Zinc", new List<ObservablePoint>() },
                { "VitD", new List<ObservablePoint>() },
                { "VitC", new List<ObservablePoint>() }
            };

            if (logList.Count == 0) return results;

            // Extract dosing timestamps where each supplement was active
            var macaDoses = logList.Where(l => l.Supplements.Contains("\"maca\":1")).Select(l => (double)l.Timestamp).ToList();
            var zincDoses = logList.Where(l => l.Supplements.Contains("\"zinc\":1")).Select(l => (double)l.Timestamp).ToList();
            var vitDDoses = logList.Where(l => l.Supplements.Contains("\"vitD\":1")).Select(l => (double)l.Timestamp).ToList();
            var vitCDoses = logList.Where(l => l.Supplements.Contains("\"vitC\":1")).Select(l => (double)l.Timestamp).ToList();

            double stepSeconds = stepHours * 3600.0;
            double kMaca = Math.Log(2) / (MacaHalfLifeHours * 3600.0);
            double kZinc = Math.Log(2) / (ZincHalfLifeHours * 3600.0);
            double kVitD = Math.Log(2) / (VitDHalfLifeHours * 3600.0);
            double kVitC = Math.Log(2) / (VitCHalfLifeHours * 3600.0);

            for (double t = startTimestamp; t <= endTimestamp; t += stepSeconds)
            {
                results["Maca"].Add(new ObservablePoint(t, CalculateConcentration(t, macaDoses, kMaca)));
                results["Zinc"].Add(new ObservablePoint(t, CalculateConcentration(t, zincDoses, kZinc)));
                results["VitD"].Add(new ObservablePoint(t, CalculateConcentration(t, vitDDoses, kVitD)));
                results["VitC"].Add(new ObservablePoint(t, CalculateConcentration(t, vitCDoses, kVitC)));
            }

            return results;
        }

        private double CalculateConcentration(double currentTime, List<double> doses, double k)
        {
            double concentration = 0.0;
            foreach (var doseTime in doses)
            {
                if (doseTime <= currentTime)
                {
                    double deltaSeconds = currentTime - doseTime;
                    concentration += Math.Exp(-k * deltaSeconds);
                }
            }
            return Math.Round(concentration, 3);
        }
    }
}
