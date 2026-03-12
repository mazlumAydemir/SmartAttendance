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

        public AutoAttendanceWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Her turda (dakikada bir) veritabanı bağlantısı açıyoruz
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<SmartAttendanceDbContext>();

                        // 1. GÖREV: BAŞLAMASI GEREKENLERİ BAŞLAT
                        await CheckAndStartSessions(context);

                        // 2. GÖREV: SÜRESİ DOLANLARI KAPAT (YENİ EKLENDİ)
                        await CheckAndStopSessions(context);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AutoAttendance Worker Hatası: {ex.Message}");
                }

                // 1 Dakika bekle
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        // --- GÖREV 1: OTOMATİK BAŞLATMA ---
        // --- GÖREV 1: OTOMATİK BAŞLATMA ---
        private async Task CheckAndStartSessions(SmartAttendanceDbContext context)
        {
            var now = DateTime.Now;
            var today = now.DayOfWeek;
            var currentTime = now.TimeOfDay;

            // Zamanı gelmiş, otomatiği açık dersleri bul
            var activeSchedules = await context.CourseSchedules
                .Include(s => s.Course)
                .Include(s => s.ClassLocation)
                .Where(s => s.DayOfWeek == today
                            && s.Course.IsAutoAttendanceEnabled == true
                            && currentTime >= s.StartTime
                            && currentTime <= s.EndTime)
                .ToListAsync();

            foreach (var schedule in activeSchedules)
            {
                // DÜZELTME: Günde 1 kuralı yerine "Şu an zaten açık bir oturumu var mı?" kontrolü yapıyoruz.
                // Eğer son 50 dakika içinde bu derse ait bir oturum açıldıysa veya hala aktifse yenisini açma.
                bool sessionExists = await context.AttendanceSessions
                    .Include(s => s.RelatedCourses)
                    .AnyAsync(s => s.RelatedCourses.Any(rc => rc.CourseId == schedule.CourseId)
                                   && (s.IsActive == true || s.StartTime >= now.AddMinutes(-50)));

                if (sessionExists) continue; // Zaten varsa atla

                // Yoksa BAŞLAT
                var settings = schedule.Course;
                string sessionCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(); // Ekrana daha güzel sığması için kısa kod yaptık

                var newSession = new AttendanceSession
                {
                    SessionCode = sessionCode,
                    InstructorId = settings.InstructorId,
                    StartTime = DateTime.Now,
                    IsActive = true,
                    EndTime = DateTime.Now.AddMinutes(settings.DefaultDurationMinutes),
                    Method = settings.DefaultMethod,
                    RequireFaceVerification = (settings.DefaultMethod == AttendanceMethod.FaceScan),
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

                Console.WriteLine($"[OTOMATİK BAŞLATILDI] {settings.CourseCode} - Süre: {settings.DefaultDurationMinutes} dk - ID: {newSession.Id}");
            }
        }

        // --- GÖREV 2: OTOMATİK KAPATMA (YENİ) ---
        private async Task CheckAndStopSessions(SmartAttendanceDbContext context)
        {
            var now = DateTime.Now;

            // Açık olan (IsActive=true) VE Bitiş saati gelmiş/geçmiş (EndTime <= Now) oturumları bul
            var expiredSessions = await context.AttendanceSessions
                .Where(s => s.IsActive && s.EndTime != null && s.EndTime <= now)
                .ToListAsync();

            if (expiredSessions.Any())
            {
                foreach (var session in expiredSessions)
                {
                    session.IsActive = false; // KAPAT
                    Console.WriteLine($"[OTOMATİK KAPATILDI] Oturum ID: {session.Id} - Planlanan Bitiş: {session.EndTime}");
                }

                await context.SaveChangesAsync();
            }
        }
    }
}