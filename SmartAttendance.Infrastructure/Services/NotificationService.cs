using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
using SmartAttendance.Application.Interfaces;

namespace SmartAttendance.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        public async Task<bool> SendMulticastNotificationAsync(List<string> deviceTokens, string title, string body)
        {
            if (deviceTokens == null || !deviceTokens.Any())
            {
                Console.WriteLine("[BİLDİRİM] Token listesi boş, gönderim iptal edildi.");
                return false;
            }

            Console.WriteLine($"[BİLDİRİM] Gönderim başlıyor. Hedef token sayısı: {deviceTokens.Count}");

            var chunks = deviceTokens
                .Select((token, index) => new { token, index })
                .GroupBy(x => x.index / 500)
                .Select(g => g.Select(x => x.token).ToList())
                .ToList();

            int totalSuccess = 0;
            int totalFailure = 0;

            foreach (var chunk in chunks)
            {
                var message = new MulticastMessage()
                {
                    Tokens = chunk,

                    // Sadece Data gönder - Notification bloğu YOK
                    // Böylece tarayıcı native bildirimi atlar,
                    // Service Worker'ın onBackgroundMessage'ı her zaman çalışır
                    // ve firebase-messaging-sw.js içindeki icon/badge ayarları aktif olur
                    Data = new Dictionary<string, string>()
            {
                { "title", title },
                { "body", body }
            },

                    // Web'e özel bildirim ayarları (icon burada da tanımlanabilir)
                    
                };

                try
                {
                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);

                    totalSuccess += response.SuccessCount;
                    totalFailure += response.FailureCount;

                    Console.WriteLine($"[BİLDİRİM] Chunk sonucu → Başarılı: {response.SuccessCount}, Hatalı: {response.FailureCount}");

                    for (int i = 0; i < response.Responses.Count; i++)
                    {
                        var tokenPreview = chunk[i].Length > 30 ? chunk[i].Substring(0, 30) + "..." : chunk[i];

                        if (response.Responses[i].IsSuccess)
                        {
                            Console.WriteLine($"[BİLDİRİM ✓] Token[{i}] başarılı. MessageId: {response.Responses[i].MessageId}");
                        }
                        else
                        {
                            var errorCode = response.Responses[i].Exception?.MessagingErrorCode;
                            var errorMsg = response.Responses[i].Exception?.Message;

                            Console.WriteLine($"[BİLDİRİM ✗] Token[{i}]: {tokenPreview}");
                            Console.WriteLine($"[BİLDİRİM ✗] Hata Kodu : {errorCode}");
                            Console.WriteLine($"[BİLDİRİM ✗] Hata Mesajı: {errorMsg}");

                            if (errorCode == MessagingErrorCode.Unregistered ||
                                errorCode == MessagingErrorCode.InvalidArgument)
                            {
                                Console.WriteLine($"[BİLDİRİM ✗] Bu token geçersiz/süresi dolmuş, DB'den temizlenmeli: {tokenPreview}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BİLDİRİM HATA] Chunk gönderiminde beklenmedik hata: {ex.Message}");
                    Console.WriteLine($"[BİLDİRİM HATA] StackTrace: {ex.StackTrace}");
                }
            }

            Console.WriteLine($"[BİLDİRİM] Tüm gönderim tamamlandı. Toplam Başarılı: {totalSuccess}, Toplam Hatalı: {totalFailure}");
            return totalSuccess > 0;
        }
    }
}