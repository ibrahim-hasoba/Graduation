namespace Shared.DTOs
{
    public class LoginResponseDto
    {
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Token { get; set; }
    }
}
