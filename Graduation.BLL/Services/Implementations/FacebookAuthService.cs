using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Shared.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Graduation.BLL.Services.Implementations
{
    public class FacebookAuthService : IFacebookAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _appId;
        private readonly string _appSecret;

        public FacebookAuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _appId = configuration["FacebookAuth:AppId"] ?? throw new InvalidOperationException("Facebook AppId not configured");
            _appSecret = configuration["FacebookAuth:AppSecret"] ?? throw new InvalidOperationException("Facebook AppSecret not configured");
        }

        public async Task<FacebookUserDataDto?> GetUserInfoAsync(string accessToken)
        {
            try
            {
                var url = $"https://graph.facebook.com/me?fields=id,name,email,picture.width(200).height(200)&access_token={accessToken}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Facebook API Error: {errorContent}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();

                var userInfo = JsonSerializer.Deserialize<FacebookUserDataDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return userInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Facebook user info: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ValidateAccessTokenAsync(string accessToken)
        {
            try
            {
                // Use app access token to validate user access token
                var url = $"https://graph.facebook.com/debug_token?input_token={accessToken}&access_token={_appId}|{_appSecret}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return false;

                var content = await response.Content.ReadAsStringAsync();

                var validationResult = JsonSerializer.Deserialize<FacebookTokenValidationDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return validationResult?.Data?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating Facebook token: {ex.Message}");
                return false;
            }
        }
    }
}
