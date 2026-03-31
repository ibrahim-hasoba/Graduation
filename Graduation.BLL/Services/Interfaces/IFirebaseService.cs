using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IFirebaseService
    {
        Task SendPushNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null);
    }
}
