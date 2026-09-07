using System;

namespace DadPlanner2.Models
{
    public class LogRecord
    {
        public long Id { get; set; }
        public long Timestamp { get; set; }
        public string Mode { get; set; } = "Maintenance";
        public string Volume { get; set; } = "Normal";
        public int HeatFlag { get; set; }
        public string Supplements { get; set; } = "{}";
        public double ClinicalVol { get; set; }
        public int Concentration { get; set; }
        public int Motility { get; set; }
        public int ProgMotility { get; set; }
        public int Morphology { get; set; }
        public double PhLevel { get; set; }
        public bool HasPdf { get; set; }

        public string DisplayDate => DateTimeOffset.FromUnixTimeSeconds(Timestamp).ToLocalTime().ToString("MMM dd HH:mm");

        // Dynamically builds the metrics string for the DataGrid
        public string MetricsDisplay 
        {
            get 
            {
                string s = $"Vol:{Volume}";
                if (HeatFlag > 0) s += $" | Temp: L{HeatFlag}";
                if (Supplements.Contains("\"zinc\":1")) s += " | Zn";
                if (Supplements.Contains("\"maca\":1")) s += " | Ma";
                if (Supplements.Contains("\"vitD\":1")) s += " | D3";
                if (Supplements.Contains("\"vitC\":1")) s += " | C";
                if (Concentration > 0) s += $" | Lab: {ClinicalVol}mL, {Concentration}M, {Motility}%";
                return s;
            }
        }
    }
}
