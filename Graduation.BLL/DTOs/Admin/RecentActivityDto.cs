using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.BLL.DTOs.Admin
{
    public class RecentActivityDto
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Link { get; set; }
    }
}
