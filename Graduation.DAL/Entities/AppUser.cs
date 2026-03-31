using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Graduation.DAL.Entities
{
    public class AppUser : IdentityUser
    {


        public string? Code { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt {  get; set; } = DateTime.UtcNow;
        public string? ProfilePictureUrl { get; set; }
        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
        public DateTime? WarningEmailSentAt { get; set; }

        public string? FcmToken { get; set; }
    }
}
