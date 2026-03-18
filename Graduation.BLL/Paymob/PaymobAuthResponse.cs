using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobAuthResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
