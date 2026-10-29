namespace LogTrackerAPI.Models
{
    public class HourlyErrorMatrix
    {
        public int HourOfDate { get; set; }
        public int NullReference { get; set; }
        public int SqlException { get; set; }
        public int DivideByZeroException { get; set; } // "Exception" eki eklendi
        public int TimeoutException { get; set; }      // "Exception" eki eklendi
    }
}