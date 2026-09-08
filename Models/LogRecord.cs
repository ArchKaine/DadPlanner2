using System;

namespace DadPlanner2.Models
{
    public enum VolumeConfidence
    {
        Observed,
        Estimated,
        Unknown
    }

    public class LogRecord
    {
        public long Id { get; set; }
        public long Timestamp { get; set; }
        public string Mode { get; set; } = "Maintenance";
        public string Volume { get; set; } = "Normal";
        public int ReleaseCount { get; set; } = 1;
        public VolumeConfidence VolumeConfidence { get; set; } = VolumeConfidence.Unknown;
        public int HeatFlag { get; set; } = 0;
        public string Supplements { get; set; } = "{}";
        public int Concentration { get; set; } = 0;
        public int Motility { get; set; } = 0;
        public int Morphology { get; set; } = 0;
        public double ClinicalVol { get; set; } = 0.0;
        public int ProgMotility { get; set; } = 0;
        public double PhLevel { get; set; } = 0.0;
        public bool HasPdf { get; set; }

        public string DisplayDate => DateTimeOffset.FromUnixTimeSeconds(Timestamp).ToLocalTime().ToString("MMM dd yyyy, HH:mm");

        public string MetricsDisplay
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                
                parts.Add($"Vol: {Volume}");
                parts.Add($"Releases: {ReleaseCount} ({VolumeConfidence})");
                
                if (HeatFlag > 0) 
                    parts.Add($"Heat: L{HeatFlag}");
                
                var supps = new System.Collections.Generic.List<string>();
                if (Supplements.Contains("\"zinc\":1")) supps.Add("Zn");
                if (Supplements.Contains("\"maca\":1")) supps.Add("Ma");
                if (Supplements.Contains("\"vitD\":1")) supps.Add("D3");
                if (Supplements.Contains("\"vitC\":1")) supps.Add("C");
                
                if (supps.Count > 0) 
                    parts.Add($"Supps: [{string.Join("] [", supps)}]");
                
                return string.Join("  |  ", parts);
            }
        }
    }
}
