using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.DTOs.Admin
{
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }
}
