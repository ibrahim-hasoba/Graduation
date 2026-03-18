using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobPaymentKeyResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
