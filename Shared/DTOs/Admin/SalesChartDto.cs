using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Admin
{
    public class SalesChartDto
    {
        public List<ChartDataPoint> Daily { get; set; } = new();
        public List<ChartDataPoint> Monthly { get; set; } = new();
    }
}
