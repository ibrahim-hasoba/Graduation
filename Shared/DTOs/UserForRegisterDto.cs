using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs
{
    public class UserForRegisterDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string? Email { get; set; }
        [Required(ErrorMessage = "Password is required.")]
        public string? Password { get; set; }
        [Compare("Password" , ErrorMessage ="The Password and Confirmation do not match")]
        public string? ConfirmPassword { get; set; }

    }
}
