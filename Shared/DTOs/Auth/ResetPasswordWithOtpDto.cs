using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    namespace Shared.DTOs.Auth
    {
        public class ResetPasswordWithOtpDto
        {
            public string? Email { get; set; }
            public string? Code { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmPassword { get; set; }
        }
    }
