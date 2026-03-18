using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobPaymentKeyRequest
    {
        [JsonPropertyName("auth_token")]
        public string AuthToken { get; set; } = string.Empty;

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("expiration")]
        public int Expiration { get; set; } = 3600;

        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }

        [JsonPropertyName("billing_data")]
        public PaymobBillingData BillingData { get; set; } = new();

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "EGP";

        [JsonPropertyName("integration_id")]
        public int IntegrationId { get; set; }
    }
}
