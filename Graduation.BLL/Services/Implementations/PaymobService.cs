using Graduation.BLL.Paymob;
using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Graduation.BLL.Services.Implementations
{
    public class PaymobService : IPaymobService
    {
        private readonly PaymobSettings _settings;
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public PaymobService(IOptions<PaymobSettings> settings, HttpClient http)
        {
            _settings = settings.Value;
            _http = http;
        }

        public async Task<string> CreatePaymentUrlAsync(
            string orderNumber,
            decimal amount,
            string firstName,
            string lastName,
            string email,
            string phone,
            string city,
            string clientType = "web")
        {
            var authToken = await AuthenticateAsync();
            var amountCents = (int)(amount * 100);
            var paymobOrderId = await RegisterOrderAsync(authToken, amountCents, orderNumber);
            var paymentKey = await GetPaymentKeyAsync(
                authToken, amountCents, paymobOrderId,
                firstName, lastName, email, phone, city);

            // Embed client_type so Paymob passes it back on the GET redirect
            return $"{_settings.IframeBaseUrl}/{_settings.IframeId}" +
                   $"?payment_token={paymentKey}&client_type={clientType}";
        }

        public bool VerifyHmac(Dictionary<string, string> data, string receivedHmac)
        {
            // Exact field order required by Paymob HMAC spec
            var fields = new[]
            {
                "amount_cents", "created_at", "currency", "error_occured",
                "has_parent_transaction", "id", "integration_id",
                "is_3d_secure", "is_auth", "is_capture",
                "is_refund", "is_standalone_payment",
                "is_voided", "order.id",
                "owner", "pending",
                "source_data.pan",
                "source_data.sub_type",
                "source_data.type",
                "success"
            };

            var sb = new StringBuilder();
            foreach (var field in fields)
            {
                if (data.TryGetValue(field, out var value) && !string.IsNullOrEmpty(value))
                    sb.Append(value);
            }

            var computed = ComputeHmacSha512(sb.ToString(), _settings.HmacSecret);
            return string.Equals(computed, receivedHmac, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> AuthenticateAsync()
        {
            var request = new PaymobAuthRequest { ApiKey = _settings.ApiKey };
            var response = await PostAsync<PaymobAuthRequest, PaymobAuthResponse>(
                "/auth/tokens", request);

            if (string.IsNullOrEmpty(response?.Token))
                throw new Exception("Paymob authentication failed — empty token returned");

            return response.Token;
        }

        private async Task<int> RegisterOrderAsync(
            string authToken, int amountCents, string merchantOrderId)
        {
            var request = new PaymobOrderRequest
            {
                AuthToken = authToken,
                AmountCents = amountCents,
                MerchantOrderId = merchantOrderId,
                Currency = "EGP",
                DeliveryNeeded = "false",
                Items = new List<object>()
            };

            var response = await PostAsync<PaymobOrderRequest, PaymobOrderResponse>(
                "/ecommerce/orders", request);

            if (response?.Id == 0)
                throw new Exception("Paymob order registration failed");

            return response!.Id;
        }

        private async Task<string> GetPaymentKeyAsync(
            string authToken,
            int amountCents,
            int paymobOrderId,
            string firstName,
            string lastName,
            string email,
            string phone,
            string city)
        {
            var request = new PaymobPaymentKeyRequest
            {
                AuthToken = authToken,
                AmountCents = amountCents,
                OrderId = paymobOrderId,
                Expiration = 3600,
                Currency = "EGP",
                IntegrationId = int.Parse(_settings.IntegrationId),
                BillingData = new PaymobBillingData
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = phone,
                    City = city,
                    Country = "EG",
                    Apartment = "NA",
                    Floor = "NA",
                    Street = "NA",
                    Building = "NA",
                    PostalCode = "NA",
                    ShippingMethod = "NA",
                    State = "NA"
                }
            };

            var response = await PostAsync<PaymobPaymentKeyRequest, PaymobPaymentKeyResponse>(
                "/acceptance/payment_keys", request);

            if (string.IsNullOrEmpty(response?.Token))
                throw new Exception("Paymob payment key generation failed");

            return response.Token;
        }

        private async Task<TResponse?> PostAsync<TRequest, TResponse>(
            string endpoint, TRequest body)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_settings.BaseUrl}{endpoint}", content);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Paymob API error at {endpoint}: {response.StatusCode} — {raw}");

            return JsonSerializer.Deserialize<TResponse>(raw);
        }

        private static string ComputeHmacSha512(string data, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}