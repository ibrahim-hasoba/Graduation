using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Admin
{
    public class TopProductDto
    {
        public int Id { get; set; }
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int TotalSales { get; set; }
        public decimal Revenue { get; set; }
        public string VendorName { get; set; } = string.Empty;
    }
}
