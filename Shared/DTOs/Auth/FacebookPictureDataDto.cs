using System.Text.Json.Serialization;

namespace Shared.DTOs.Auth
{
    public class FacebookPictureDataDto
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }
    }
}
