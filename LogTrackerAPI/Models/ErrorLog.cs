namespace LogTrackerAPI.Models
{
    public class ErrorLog
    {
        public int ErrorID { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorSource { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public DateTime LogDate { get; set; }
    }
}