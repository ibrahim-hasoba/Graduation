using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.DAL.Entities
{
    public class UserAddress
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;  
        public string Nickname { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public double? Latitude { get; set; }        
        public double? Longitude { get; set; }
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public AppUser User { get; set; } = null!;
    }

    
}
