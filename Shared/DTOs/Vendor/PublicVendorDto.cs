using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs.Vendor
{
    public class PublicVendorDto
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreNameAr { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
}
