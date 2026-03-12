using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SmartAttendance.Infrastructure.Hubs
{
    // Bu sınıf sadece istemciler (React) ile sunucu arasındaki canlı tüneli yönetir.
    // İş mantığı (Database vs.) kesinlikle içermez. Sadece "Postacı"dır.
    public class AttendanceHub : Hub
    {
        /// <summary>
        /// İstemciyi belirli bir yoklama oturumunun "odasına" (Group) dahil eder.
        /// Böylece sadece o odaya atılan mesajları duyar.
        /// </summary>
        public async Task JoinSessionGroup(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

            // Console logları canlıda hataları (veya başarıları) izlemek için hayat kurtarır
            Console.WriteLine($"[SignalR] {Context.ConnectionId} ID'li istemci {sessionId} numaralı odaya katıldı.");
        }

        /// <summary>
        /// İstemciyi yoklama odasından çıkartır (Sayfadan çıkınca vb.)
        /// </summary>
        public async Task LeaveSessionGroup(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            Console.WriteLine($"[SignalR] {Context.ConnectionId} ID'li istemci {sessionId} numaralı odadan ayrıldı.");
        }

        // =================================================================
        // DİNLENME VE LOGLAMA (Bağlantı koptu mu, bağlandı mı anlamak için)
        // =================================================================

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[SignalR] YENİ BAĞLANTI: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] BAĞLANTI KOPTU: {Context.ConnectionId}. Neden: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}