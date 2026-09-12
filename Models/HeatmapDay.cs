namespace DadPlanner2.Models
{
    public class HeatmapDay
    {
        public int Level { get; set; }
        public string ColorHex { get; set; } = "#252526";
        public string DateText { get; set; } = "";
        public string MainInfoText { get; set; } = "";
        public string ModesText { get; set; } = "";
        public bool HasData => Level > 0;
    }
}
