using System.Text.Json.Serialization;

namespace Shared.DTOs.Auth
{
    public class FacebookPictureDto
    {
        [JsonPropertyName("data")]
        public FacebookPictureDataDto? Data { get; set; }
    }
}
