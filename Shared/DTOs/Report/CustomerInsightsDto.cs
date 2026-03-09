namespace Shared.DTOs.Report
{
    public class CustomerInsightsDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int RepeatCustomers { get; set; }
        public double CustomerRetentionRate { get; set; }
    }
}