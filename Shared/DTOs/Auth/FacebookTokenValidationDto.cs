using System.Text.Json.Serialization;

namespace Shared.DTOs.Auth
{
    public class FacebookTokenValidationDto
    {
        [JsonPropertyName("data")]
        public FacebookTokenDataDto? Data { get; set; }
    }
}
