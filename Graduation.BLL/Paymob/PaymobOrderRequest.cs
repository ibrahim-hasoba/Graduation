using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobOrderRequest
    {
        [JsonPropertyName("auth_token")]
        public string AuthToken { get; set; } = string.Empty;

        [JsonPropertyName("delivery_needed")]
        public string DeliveryNeeded { get; set; } = "false";

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "EGP";

        [JsonPropertyName("items")]
        public List<object> Items { get; set; } = new();

        [JsonPropertyName("merchant_order_id")]
        public string MerchantOrderId { get; set; } = string.Empty;
    }
}
