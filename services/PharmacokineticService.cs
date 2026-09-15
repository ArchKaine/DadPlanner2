using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DadPlanner2.Models;
using LiveChartsCore.Defaults;

namespace DadPlanner2.Services
{
    public class PharmacokineticService
    {
        // Half-lives in days
        private const double ZincHalfLifeDays = 12.0;
        private const double MacaHalfLifeDays = 20.0 / 24.0; // 20 hours
        private const double VitDHalfLifeDays = 18.0;
        private const double VitCHalfLifeDays = 24.0 / 24.0; // 24 hours
        private const double TadalafilHalfLifeDays = 17.5 / 24.0; // 17.5 hours

        public Dictionary<string, List<ObservablePoint>> CalculateCurves(List<LogRecord> logs, double startTs, double endTs)
        {
            var zincPoints = new List<ObservablePoint>();
            var macaPoints = new List<ObservablePoint>();
            var vitDPoints = new List<ObservablePoint>();
            var vitCPoints = new List<ObservablePoint>();
            var tadalafilPoints = new List<ObservablePoint>();

            // Extract all supplement ingestion events sorted chronologically
            // Tadalafil is now captured as an INT representing the dose in milligrams.
            var events = new List<(long Timestamp, bool Zinc, bool Maca, bool VitD, bool VitC, int TadalafilDose)>();

            foreach (var log in logs.OrderBy(l => l.Timestamp))
            {
                try
                {
                    using var doc = JsonDocument.Parse(log.Supplements);
                    var root = doc.RootElement;

                    bool z = root.TryGetProperty("zinc", out var zProp) && zProp.GetInt32() == 1;
                    bool m = root.TryGetProperty("maca", out var mProp) && mProp.GetInt32() == 1;
                    bool d = root.TryGetProperty("vitD", out var dProp) && dProp.GetInt32() == 1;
                    bool c = root.TryGetProperty("vitC", out var cProp) && cProp.GetInt32() == 1;
                    
                    int tDose = 0;
                    if (root.TryGetProperty("tadalafil", out var tProp))
                    {
                        tDose = tProp.GetInt32();
                    }

                    if (z || m || d || c || tDose > 0)
                    {
                        events.Add((log.Timestamp, z, m, d, c, tDose));
                    }
                }
                catch
                {
                    // Fallback parsing if legacy string formatting exists
                    bool z = log.Supplements.Contains("\"zinc\":1");
                    bool m = log.Supplements.Contains("\"maca\":1");
                    bool d = log.Supplements.Contains("\"vitD\":1");
                    bool c = log.Supplements.Contains("\"vitC\":1");
                    int tDose = log.Supplements.Contains("\"tadalafil\":1") ? 10 : 0; // Legacy 1.0 bool mapped to 10mg

                    if (z || m || d || c || tDose > 0)
                    {
                        events.Add((log.Timestamp, z, m, d, c, tDose));
                    }
                }
            }

            // Sample every 4 hours instead of every 24 hours to capture sub-day pharmacokinetic spikes
            double step = 4 * 3600.0; 
            for (double t = startTs; t <= endTs; t += step)
            {
                double zincLevel = 0;
                double macaLevel = 0;
                double vitDLevel = 0;
                double vitCLevel = 0;
                double tadalafilLevel = 0;

                foreach (var ev in events)
                {
                    if (ev.Timestamp > t) continue; // Future event relative to sample point

                    double deltaDays = (t - ev.Timestamp) / 86400.0;
                    if (deltaDays < 0) continue;

                    if (ev.Zinc) zincLevel += Math.Exp(-Math.Log(2) * deltaDays / ZincHalfLifeDays);
                    if (ev.Maca) macaLevel += Math.Exp(-Math.Log(2) * deltaDays / MacaHalfLifeDays);
                    if (ev.VitD) vitDLevel += Math.Exp(-Math.Log(2) * deltaDays / VitDHalfLifeDays);
                    if (ev.VitC) vitCLevel += Math.Exp(-Math.Log(2) * deltaDays / VitCHalfLifeDays);
                    
                    if (ev.TadalafilDose > 0) 
                    {
                        // Dose scaling: 10mg equals baseline amplitude (1.0). 20mg equals 2.0 amplitude.
                        double scaleFactor = ev.TadalafilDose / 10.0;
                        tadalafilLevel += scaleFactor * Math.Exp(-Math.Log(2) * deltaDays / TadalafilHalfLifeDays);
                    }
                }

                zincPoints.Add(new ObservablePoint(t, Math.Round(zincLevel, 3)));
                macaPoints.Add(new ObservablePoint(t, Math.Round(macaLevel, 3)));
                vitDPoints.Add(new ObservablePoint(t, Math.Round(vitDLevel, 3)));
                vitCPoints.Add(new ObservablePoint(t, Math.Round(vitCLevel, 3)));
                tadalafilPoints.Add(new ObservablePoint(t, Math.Round(tadalafilLevel, 3)));
            }

            return new Dictionary<string, List<ObservablePoint>>
            {
                { "Zinc", zincPoints },
                { "Maca", macaPoints },
                { "VitD", vitDPoints },
                { "VitC", vitCPoints },
                { "Tadalafil", tadalafilPoints }
            };
        }
    }
}
