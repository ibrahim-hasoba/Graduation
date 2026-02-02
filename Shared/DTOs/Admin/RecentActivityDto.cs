using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Admin
{
    public class RecentActivityDto
    {
        public string Type { get; set; } = string.Empty; // Order, User, Vendor, Product
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Link { get; set; }
    }
}
