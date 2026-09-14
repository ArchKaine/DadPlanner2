namespace DadPlanner2.Models
{
    public class PredictionMetrics
    {
        public bool IsValid { get; set; }
        public double MeanGapHours { get; set; }
        public double StdDevHours { get; set; }
        public long ExpectedTargetTimestamp { get; set; }
        public long WindowStartTimestamp { get; set; }
        public long WindowEndTimestamp { get; set; }
    }
}
