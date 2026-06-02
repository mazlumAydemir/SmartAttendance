using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Infrastructure.Persistence;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.BackgroundServices
{
    public class AutoAttendanceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        // ⭐ TEST İÇİN: Worker'ı sık çalıştırıyoruz ki gecikme yaşamayasın.
        // Canlıya alırken bunu TimeSpan.FromMinutes(1) yapabilirsin.
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(20);

        public AutoAttendanceWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine($"[WORKER] AutoAttendanceWorker başlatıldı. Kontrol aralığı: {CheckInterval.TotalSeconds} sn");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<SmartAttendanceDbContext>();

                        // 1. GÖREV: BAŞLAMASI GEREKENLERİ BAŞLAT
                        await CheckAndStartSessions(context);

                        // 2. GÖREV: SÜRESİ DOLANLARI KAPAT
                        await CheckAndStopSessions(context);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WORKER HATA] {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"[WORKER HATA-INNER] {ex.InnerException.Message}");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        // --- GÖREV 1: OTOMATİK BAŞLATMA ---
        private async Task CheckAndStartSessions(SmartAttendanceDbContext context)
        {
            var now = DateTime.Now;
            var today = now.DayOfWeek;
            var currentTime = now.TimeOfDay;

            // 🔍 TEŞHİS LOGU: Worker'ın gördüğü zamanı yazdırıyoruz.
            // Bu satır, "DateTime.Now" senin gerçek duvar saatinle aynı mı diye kontrol etmeni sağlar.
            Console.WriteLine($"[WORKER] Kontrol -> Tarih/Saat: {now:yyyy-MM-dd HH:mm:ss} | Gün: {today}");

            // Zamanı gelmiş, otomatiği açık dersleri bul
            var activeSchedules = await context.CourseSchedules
                .Include(s => s.Course)
                .Include(s => s.ClassLocation)
                .Where(s => s.DayOfWeek == today
                            && s.Course.IsAutoAttendanceEnabled == true
                            && currentTime >= s.StartTime
                            && currentTime <= s.EndTime)
                .ToListAsync();

            Console.WriteLine($"[WORKER] Şu an zamanı gelmiş ve otomatiği açık ders sayısı: {activeSchedules.Count}");

            foreach (var schedule in activeSchedules)
            {
                // "Şu an zaten açık bir oturumu var mı?" kontrolü.
                // Eğer hala aktif bir oturum varsa veya bu ders programı slotu içinde
                // (StartTime'dan sonra) açılmış bir oturum varsa yenisini açma.
                // NOT: Sabit 50 dk yerine, bu dersin BUGÜNKÜ bu slotu için oturum var mı diye bakıyoruz.
                bool sessionExists = await context.AttendanceSessions
                    .Include(s => s.RelatedCourses)
                    .AnyAsync(s => s.RelatedCourses.Any(rc => rc.CourseId == schedule.CourseId)
                                   && s.IsActive == true);

                if (sessionExists)
                {
                    Console.WriteLine($"[WORKER] {schedule.Course.CourseCode} için zaten AKTİF oturum var, atlanıyor.");
                    continue;
                }

                // Bu slot için bugün daha önce (ve kapatılmış) bir oturum açıldıysa tekrar açma.
                // Slotun başlangıcından bu yana açılmış herhangi bir oturum varsa atla.
                var slotStartToday = now.Date.Add(schedule.StartTime);
                bool alreadyOpenedThisSlot = await context.AttendanceSessions
                    .AnyAsync(s => s.RelatedCourses.Any(rc => rc.CourseId == schedule.CourseId)
                                   && s.StartTime >= slotStartToday
                                   && s.StartTime <= now);

                if (alreadyOpenedThisSlot)
                {
                    Console.WriteLine($"[WORKER] {schedule.Course.CourseCode} bu slotta zaten açılmış (kapanmış olabilir), atlanıyor.");
                    continue;
                }

                // Yoksa BAŞLAT
                var settings = schedule.Course;
                string sessionCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

                var newSession = new AttendanceSession
                {
                    SessionCode = sessionCode,
                    InstructorId = settings.InstructorId,
                    StartTime = DateTime.Now,
                    IsActive = true,
                    EndTime = now.Date.Add(schedule.EndTime), // ⭐ Bitiş, ders programındaki bitiş saati olsun
                    Method = settings.DefaultMethod,
                    RequireFaceVerification = (settings.DefaultMethod == AttendanceMethod.CrowdScan),
                    RequireDeviceVerification = true,
                    RequireLocationVerification = true,
                    SnapshotLatitude = schedule.ClassLocation?.Latitude ?? 0,
                    SnapshotLongitude = schedule.ClassLocation?.Longitude ?? 0,
                    SnapshotRadius = settings.DefaultRadiusMeters
                };

                context.AttendanceSessions.Add(newSession);
                await context.SaveChangesAsync();

                context.SessionCourseLinks.Add(new SessionCourseLink
                {
                    AttendanceSessionId = newSession.Id,
                    CourseId = settings.Id
                });
                await context.SaveChangesAsync();

                Console.WriteLine($"[OTOMATİK BAŞLATILDI] ✅ {settings.CourseCode} | Yöntem: {settings.DefaultMethod} | Bitiş: {newSession.EndTime:HH:mm} | ID: {newSession.Id}");
            }
        }

        // --- GÖREV 2: OTOMATİK KAPATMA ---
        private async Task CheckAndStopSessions(SmartAttendanceDbContext context)
        {
            var now = DateTime.Now;

            var expiredSessions = await context.AttendanceSessions
                .Where(s => s.IsActive && s.EndTime != null && s.EndTime <= now)
                .ToListAsync();

            if (expiredSessions.Any())
            {
                foreach (var session in expiredSessions)
                {
                    session.IsActive = false;
                    Console.WriteLine($"[OTOMATİK KAPATILDI] ⛔ Oturum ID: {session.Id} | Planlanan Bitiş: {session.EndTime:HH:mm}");
                }

                await context.SaveChangesAsync();
            }
        }
    }
}