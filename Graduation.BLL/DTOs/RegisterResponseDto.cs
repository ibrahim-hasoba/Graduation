namespace Graduation.BLL.DTOs
{
    public class RegisterResponseDto
    {
        public bool IsSuccessfulRegistration { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }
}
