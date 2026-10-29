namespace LogTrackerAPI.Models
{
    public class PerformanceMetric
    {
        public int MetricID { get; set; }
        public string EndpointPath { get; set; } = string.Empty;
        public int ResponseTimeMS { get; set; }
        public DateTime LogDate { get; set; }
    }
}