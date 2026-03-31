using FirebaseAdmin.Messaging;
using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Graduation.BLL.Services.Implementations
{
    public class FirebaseService : IFirebaseService
    {
        private readonly FirebaseMessaging _messaging;
        private readonly ILogger<FirebaseService> _logger;

        public FirebaseService(FirebaseMessaging messaging, ILogger<FirebaseService> logger)
        {
            _messaging = messaging;
            _logger = logger;
        }

        public async Task SendPushNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null)
        {
            if (string.IsNullOrEmpty(fcmToken))
            {
                _logger.LogWarning("Attempted to send a notification to a null or empty FCM token.");
                return;
            }

            var message = new Message()
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data ?? new Dictionary<string, string>()
            };

            try
            {
                _logger.LogInformation("Sending FCM notification to token: {Token}", fcmToken);
                string response = await _messaging.SendAsync(message);
                _logger.LogInformation("Successfully sent message: {Response}", response);
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex, "Firebase Messaging Error for token {Token}. Error Code: {ErrorCode}", fcmToken, ex.MessagingErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while sending FCM notification to {Token}", fcmToken);
            }
        }
    }
}