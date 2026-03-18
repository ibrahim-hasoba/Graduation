using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobCallbackOrder
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("merchant_order_id")]
        public string MerchantOrderId { get; set; } = string.Empty;

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }
    }
}
