namespace Graduation.BLL.Services.Interfaces
{
    public enum FcmSendResult
    {
        Success,
        InvalidToken,
        Error
    }

    public interface IFirebaseService
    {
        Task<FcmSendResult> SendPushNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null);
    }
}
