namespace DadPlanner2.Models
{
    public class HeatmapDay
    {
        public string DateStr { get; set; } = "";
        public int Level { get; set; }
        public string Tooltip { get; set; } = "";
        
        // Ensure this is an auto-property with get; and set;
        public string ColorHex { get; set; } = "#252526"; 
    }
}