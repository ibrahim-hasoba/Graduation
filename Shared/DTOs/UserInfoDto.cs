namespace Shared.DTOs
{
    public class UserInfoDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
        public bool HasAddress { get; set; }
    }
}
