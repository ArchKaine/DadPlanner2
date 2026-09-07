namespace DadPlanner2.Models
{
    public class HeatmapDay
    {
        public string DateStr { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Tooltip { get; set; } = string.Empty;
        
        // Maps the activity level to the CSS colors from the HTML
        public string ColorHex => Level switch
        {
            3 => "#42a5f5", // High
            2 => "#005999", // Normal
            1 => "#0a304e", // Low
            _ => "#252526"  // Dry/Empty
        };
    }
}
