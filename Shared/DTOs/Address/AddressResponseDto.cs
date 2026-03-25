namespace Shared.DTOs.Address
{
    public class AddressResponseDto
    {
        public int Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
