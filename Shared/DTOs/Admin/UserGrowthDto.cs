using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.DTOs.Admin
{
    public class UserGrowthDto
    {
        public string Month { get; set; } = string.Empty;
        public int NewUsers { get; set; }
    }
}
