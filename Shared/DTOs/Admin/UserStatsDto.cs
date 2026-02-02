using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Admin
{
    public class UserStatsDto
    {
        public int TotalUsers { get; set; }
        public int CustomersCount { get; set; }
        public int VendorsCount { get; set; }
        public int AdminsCount { get; set; }
        public int VerifiedUsers { get; set; }
        public int UnverifiedUsers { get; set; }
        public List<UserGrowthDto> MonthlyGrowth { get; set; } = new();
    }
}
