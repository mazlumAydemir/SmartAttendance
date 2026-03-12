using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.Interfaces
{
    public interface INotificationService
    {
        Task<bool> SendMulticastNotificationAsync(List<string> deviceTokens, string title, string body); 
    }
}
