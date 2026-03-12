using Microsoft.AspNetCore.SignalR; // BUNU EKLEMEYİ UNUTMA
using SmartAttendance.Infrastructure.Hubs; // BUNU EKLEMEYİ UNUTMA
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.DTOs.Course;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly SmartAttendanceDbContext _context;
        private readonly INotificationService _notificationService;

        // 1. SIGNALR HUB BAĞLANTISI
        private readonly IHubContext<AttendanceHub> _hubContext;

        public AttendanceService(
            SmartAttendanceDbContext context,
            INotificationService notificationService,
            IHubContext<AttendanceHub> hubContext) // CONSTRUCTOR'A EKLENDİ
        {
            _context = context;
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        // ... (Diğer kısımlar aynı)

        // ==================================================================================
        // 1. HOCA: OTURUM BAŞLATMA (OTOMATİK SINIF KONUMU İLE)
        // ==================================================================================
        public async Task<SessionResponseDto> StartSessionAsync(CreateSessionDto model, int instructorId)
        {
            var existingCoursesCount = await _context.Courses
                                            .Where(c => model.CourseIds.Contains(c.Id))
                                            .CountAsync();

            if (existingCoursesCount != model.CourseIds.Count)
                throw new Exception("Hata: Seçilen derslerden biri veya birkaçı veritabanında bulunamadı.");

            // Sınıf Konumu Bulma
            DateTime sessionTime = model.StartTime ?? DateTime.Now;
            DayOfWeek today = sessionTime.DayOfWeek;
            TimeSpan timeNow = sessionTime.TimeOfDay;

            var schedule = await _context.CourseSchedules
        .Include(s => s.ClassLocation)
        .Where(s => model.CourseIds.Contains(s.CourseId))
        .FirstOrDefaultAsync();

            double targetLat = 0;
            double targetLon = 0;

            if (schedule != null && schedule.ClassLocation != null)
            {
                targetLat = schedule.ClassLocation.Latitude;
                targetLon = schedule.ClassLocation.Longitude;
            }

            string sessionCode = Guid.NewGuid().ToString();

            var session = new AttendanceSession
            {
                SessionCode = sessionCode,
                InstructorId = instructorId,
                StartTime = sessionTime,
                IsActive = true,
                Method = model.Method,
                RequireFaceVerification = (model.Method == AttendanceMethod.FaceScan) ? true : model.RequireFaceVerification,
                RequireDeviceVerification = model.RequireDeviceVerification,
                RequireLocationVerification = true,
                SnapshotLatitude = targetLat,
                SnapshotLongitude = targetLon,
                SnapshotRadius = model.RadiusMeters
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync();

            foreach (var courseId in model.CourseIds)
            {
                _context.SessionCourseLinks.Add(new SessionCourseLink
                {
                    AttendanceSessionId = session.Id,
                    CourseId = courseId
                });
            }
            await _context.SaveChangesAsync();

            // =========================================================================
            // 🚀 FIREBASE BİLDİRİM GÖNDERME AŞAMASI (KİŞİSELLEŞTİRİLMİŞ DERS)
            // =========================================================================
            try
            {
                foreach (var courseId in model.CourseIds)
                {
                    var currentCourse = await _context.Courses.FindAsync(courseId);
                    if (currentCourse == null) continue;

                    var studentTokens = await _context.CourseEnrollments
                        .Include(ce => ce.Student)
                        .Where(ce => ce.CourseId == courseId && !string.IsNullOrEmpty(ce.Student.FcmToken))
                        .Select(ce => ce.Student.FcmToken)
                        .Distinct()
                        .ToListAsync();

                    if (studentTokens.Any())
                    {
                        string title = "Yeni Yoklama Başladı!";
                        string body = $"{currentCourse.CourseCode} dersi için yoklama aktiftir. Lütfen sisteme girip katılımınızı onaylayın.";

                        Console.WriteLine($"[BİLDİRİM TEST] {currentCourse.CourseCode} dersi için {studentTokens.Count} öğrenciye bildirim gönderiliyor...");
                        await _notificationService.SendMulticastNotificationAsync(studentTokens, title, body);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BİLDİRİM TEST] Bildirim aşamasında beklenmeyen HATA: {ex.Message}");
            }

            // =========================================================
            // 🚀 SIGNALR CANLI BİLDİRİMİ: YOKLAMA BAŞLADI
            // =========================================================
            try
            {
                await _hubContext.Clients.All.SendAsync("SessionStarted");
                Console.WriteLine($"[SignalR] Yeni oturum başladı anonsu yapıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR Hatası] {ex.Message}");
            }

            return new SessionResponseDto
            {
                SessionId = session.Id,
                SessionCode = sessionCode,
                QrCodeContent = sessionCode
            };
        }
        /*
        // ==================================================================================
        // 2. ÖĞRENCİ: QR İLE KATILMA
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId)
        {
            var parts = model.QrContent.Split("||");
            if (parts.Length != 2) throw new Exception("Geçersiz QR Kod formatı!");

            string sessionCode = parts[0];
            if (!long.TryParse(parts[1], out long expirationTicks)) throw new Exception("QR Kod bozuk!");

            if (DateTime.UtcNow > new DateTime(expirationTicks))
                throw new Exception("QR Kodunun süresi dolmuş!");

            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

            if (session == null || !IsSessionValid(session))
                throw new Exception("Oturum bulunamadı veya sonlandırılmış.");

            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, session.RequireDeviceVerification);

            if (session.RequireLocationVerification)
            {
                double distance = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                if (distance > (session.SnapshotRadius ?? 50))
                    throw new Exception($"Sınıftan uzaktasınız! Mesafe: {distance:0.0}m.");
            }

            // --- DÜZELTİLDİ: int türünde HasValue kullanılmaz ---
            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                throw new Exception("Bu oturuma ait dersi almıyorsunuz.");

            return await RegisterAttendance(session.Id, studentId, model.DeviceId);
        }

        // ==================================================================================
        // 3. ÖĞRENCİ: KONUM İLE KATILMA
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionByLocationAsync(JoinLocationDto model, int studentId)
        {
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            var activeSessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!activeSessions.Any()) throw new Exception("Şu anda aktif bir yoklama yok.");

            AttendanceSession? targetSession = null;

            foreach (var session in activeSessions)
            {
                if (!IsSessionValid(session)) continue;

                if (session.RequireDeviceVerification)
                    await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);

                // --- DÜZELTİLDİ: HasValue ve Value kaldırıldı ---
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

                if (!sessionCourseIds.Intersect(studentCourseIds).Any()) continue;

                if (session.RequireLocationVerification)
                {
                    // Eğer veritabanında bu alanlar null ise (?? 0) ve (?? 50) devreye girer.
                    double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                    if (dist <= (session.SnapshotRadius ?? 50))
                    {
                        targetSession = session;
                        break;
                    }
                }
                else
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession == null)
                throw new Exception("Konumunuzda uygun bir aktif yoklama bulunamadı.");

            return await RegisterAttendance(targetSession.Id, studentId, model.DeviceId);
        }

        // ==================================================================================
        // 4. ÖĞRENCİ: YÜZ TANIMA İLE KATILMA
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionByFaceAsync(JoinFaceDto model, int studentId)
        {
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            if (model.FaceImage == null || model.FaceImage.Length == 0)
                throw new Exception("Lütfen yüzünüzün göründüğü bir fotoğraf yükleyin.");

            var activeSessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!activeSessions.Any()) throw new Exception("Şu anda aktif bir yoklama yok.");

            AttendanceSession? targetSession = null;

            foreach (var session in activeSessions)
            {
                if (!IsSessionValid(session)) continue;

                if (session.RequireDeviceVerification)
                    await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);

                // --- DÜZELTİLDİ: HasValue ve Value kaldırıldı ---
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

                if (!sessionCourseIds.Intersect(studentCourseIds).Any()) continue;

                double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                if (dist <= (session.SnapshotRadius ?? 50))
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession == null)
                throw new Exception("Konumunuzda aktif bir ders bulunamadı.");

            // FOTOĞRAF KAYDETME
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{studentId}_{targetSession.Id}_{Guid.NewGuid()}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.FaceImage.CopyToAsync(stream);
            }

            // MÜKERRER KAYIT KONTROLÜ
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == targetSession.Id && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            // KAYIT (OTOMATİK 'PRESENT')
            var record = new AttendanceRecord
            {
                AttendanceSessionId = targetSession.Id,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                Status = AttendanceStatus.Present,
                Description = "Yüz Tanıma ile Giriş",
                IsDeviceVerified = true,
                IsFaceVerified = true,
                IsValid = true,
                UsedDeviceId = model.DeviceId,
                DistanceFromSessionCenter = 0,
                FaceSnapshotUrl = "/uploads/" + fileName
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return new JoinSessionResponseDto
            {
                IsSuccess = true,
                Message = "Yüz Doğrulama Başarılı!",
                CourseName = "Ders Eşleşti"
            };
        }
       */
        // ==================================================================================
        // 2. ÖĞRENCİ: QR İLE KATILMA (ID + QR MATCH KONTROLÜ + METHOD CHECK)
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId)
        {
            // --- 1. QR KOD FORMAT VE SÜRE KONTROLÜ ---
            var parts = model.QrContent.Split("||");
            if (parts.Length != 2)
                throw new Exception("Geçersiz QR Kod formatı! Lütfen kodu tekrar okutun.");

            string qrSessionCode = parts[0];
            if (!long.TryParse(parts[1], out long expirationTicks))
                throw new Exception("QR Kod verisi bozuk.");

            // DateTime.UtcNow kullanıyoruz, saati evrensel saate çevirip kontrol ediyoruz
            if (DateTime.UtcNow.Ticks > expirationTicks)
                throw new Exception("QR Kodun süresi dolmuş! Lütfen tahtadaki yeni kodu okutun.");

            // --- 2. OTURUMU VE DERSİ BULMA ---
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == model.SessionId);

            if (session == null) throw new Exception("Oturum bulunamadı.");
            if (!session.IsActive) throw new Exception("Bu yoklama oturumu hoca tarafından kapatılmış.");

            // --- 3. QR KOD EŞLEŞMESİ (Güvenlik) ---
            // Telefondan gelen kod ile veritabanındaki kod uyuşuyor mu?
            if (session.SessionCode != qrSessionCode)
                throw new Exception("Yanlış dersin QR kodunu okuttunuz! Kodlar eşleşmiyor.");

            // --- 4. CİHAZ ID KONTROLÜ (Device Fingerprint) ---
            // Eğer hoca cihaz kontrolünü açtıysa (Varsayılan: True)
            if (session.RequireDeviceVerification)
            {
                // Bu cihaz daha önce başka bir öğrenci adına kullanılmış mı?
                var deviceUsedByOther = await _context.AttendanceRecords
                    .AnyAsync(r => r.AttendanceSessionId == session.Id &&
                                   r.UsedDeviceId == model.DeviceId &&
                                   r.StudentId != studentId);

                if (deviceUsedByOther)
                    throw new Exception("Dikkat! Bu cihaz ile başka bir arkadaşınız yoklama vermiş. Kopya girişimi engellendi.");
            }

            // --- 5. KONUM KONTROLÜ (GPS) ---
            // Hoca konum zorunluluğu koyduysa
            if (session.RequireLocationVerification)
            {
                // Koordinat kontrolü (0 gelirse hata ver)
                if (model.Latitude == 0 || model.Longitude == 0)
                    throw new Exception("Konum verisi alınamadı. Lütfen telefonunuzun GPS özelliğini açıp sayfayı yenileyin.");

                // Mesafeyi Hesapla
                double distance = CalculateDistance(
                    session.SnapshotLatitude ?? 0,
                    session.SnapshotLongitude ?? 0,
                    model.Latitude,
                    model.Longitude
                );

                // Hoca'nın belirlediği yarıçap (Varsayılan 50m)
                double limit = session.SnapshotRadius ?? 50;

                if (distance > limit)
                {
                    throw new Exception($"Sınıftan çok uzaktasınız! Tespit edilen mesafe: {distance:0.0} metre. (İzin verilen: {limit}m)");
                }
            }

            // --- 6. DERS KAYDI KONTROLÜ ---
            // Öğrenci bu dersi alıyor mu?
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                throw new Exception("Bu dersi almıyorsunuz, yoklamaya katılamazsınız.");

            // --- 7. MÜKERRER KAYIT ---
            bool alreadyJoined = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == session.Id && r.StudentId == studentId);

            if (alreadyJoined)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız." };

            // --- 8. KAYIT ---
            var record = new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                Status = SmartAttendance.Domain.Enums.AttendanceStatus.Present,
                
                UsedDeviceId = model.DeviceId,
                IsDeviceVerified = true,
                DistanceFromSessionCenter = session.RequireLocationVerification
                    ? CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude)
                    : 0
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return new JoinSessionResponseDto { IsSuccess = true, Message = "Yoklamaya Başarıyla Katıldınız!" };
        }
        // ==================================================================================
        // 3. ÖĞRENCİ: KONUM İLE KATILMA (ID İLE + METHOD CHECK)
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionByLocationAsync(JoinLocationDto model, int studentId)
        {
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            // 1. OTURUMU GETİR
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == model.SessionId);

            if (session == null) throw new Exception("Seçilen oturum bulunamadı.");
            if (!IsSessionValid(session)) throw new Exception("Bu oturum aktif değil.");

            // 🔥 GÜVENLİK KİLİDİ: YÖNTEM KONTROLÜ 🔥
            if (session.Method != AttendanceMethod.Location)
            {
                throw new Exception("Bu ders Konum yöntemiyle açılmamış! Lütfen QR Kod veya Yüz Tanıma deneyin.");
            }

            // 2. DERS KAYDI KONTROLÜ
            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                throw new Exception("Bu derse kayıtlı değilsiniz.");

            // 3. CİHAZ KONTROLÜ
            if (session.RequireDeviceVerification)
                await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);

            // 4. KONUM KONTROLÜ
            if (session.RequireLocationVerification)
            {
                double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                double limit = session.SnapshotRadius ?? 50;

                if (dist > limit)
                    throw new Exception($"Sınıftan uzaktasınız! Mesafe: {dist:0.0}m (Sınır: {limit}m)");
            }

            // 5. KAYIT
            return await RegisterAttendance(session.Id, studentId, model.DeviceId);
        }
        // ==================================================================================
        // 4. ÖĞRENCİ: YÜZ TANIMA İLE KATILMA (ID İLE + METHOD CHECK)
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionByFaceAsync(JoinFaceDto model, int studentId)
        {
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            if (model.FaceImage == null || model.FaceImage.Length == 0)
                throw new Exception("Lütfen yüzünüzün göründüğü bir fotoğraf yükleyin.");

            // 1. OTURUMU GETİR
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == model.SessionId);

            if (session == null) throw new Exception("Seçilen oturum bulunamadı.");
            if (!IsSessionValid(session)) throw new Exception("Bu oturum aktif değil.");

            // 🔥 GÜVENLİK KİLİDİ: YÖNTEM KONTROLÜ 🔥
            if (session.Method != AttendanceMethod.FaceScan)
            {
                throw new Exception("Bu ders Yüz Tanıma yöntemiyle açılmamış! Lütfen QR Kod veya Konum deneyin.");
            }

            // 2. DERS KAYDI KONTROLÜ
            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                throw new Exception("Bu derse kayıtlı değilsiniz.");

            // 3. CİHAZ KONTROLÜ
            if (session.RequireDeviceVerification)
                await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);

            // 4. KONUM KONTROLÜ (Güvenlik için)
            double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
            double limit = session.SnapshotRadius ?? 50;

            if (dist > limit)
                throw new Exception($"Sınıftan uzaktasınız! Mesafe: {dist:0.0}m (Sınır: {limit}m)");

            // 5. FOTOĞRAF KAYDETME
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{studentId}_{session.Id}_{Guid.NewGuid()}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.FaceImage.CopyToAsync(stream);
            }

            // 6. MÜKERRER KAYIT KONTROLÜ
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == session.Id && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            // 7. KAYIT OLUŞTURMA
            var record = new AttendanceRecord
            {
                AttendanceSessionId = session.Id,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                Status = SmartAttendance.Domain.Enums.AttendanceStatus.Present,
                Description = "Yüz Tanıma ile Giriş",
                IsDeviceVerified = true,
                IsFaceVerified = true,
                IsValid = true,
                UsedDeviceId = model.DeviceId,
                DistanceFromSessionCenter = 0,
                FaceSnapshotUrl = "/uploads/" + fileName
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return new JoinSessionResponseDto
            {
                IsSuccess = true,
                Message = "Yüz Doğrulama Başarılı!",
                CourseName = "Ders Eşleşti"
            };
        }
        // ==================================================================================
        // 5. HOCA: OTURUM YÖNETİMİ
        // ==================================================================================
        public async Task<List<ActiveSessionDto>> GetActiveSessionsAsync(int instructorId)
        {
            var sessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses).ThenInclude(rc => rc.Course)
                .Where(s => s.InstructorId == instructorId && s.IsActive)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            return sessions.Select(s => new ActiveSessionDto
            {
                SessionId = s.Id,
                SessionCode = s.SessionCode,
                StartTime = s.StartTime,
                MethodName = s.Method.ToString(),
                CourseNames = s.RelatedCourses.Select(rc => rc.Course.CourseName + " (" + rc.Course.CourseCode + ")").ToList()
            }).ToList();
        }

        public async Task<bool> EndSessionAsync(int sessionId, int instructorId)
        {
            var session = await _context.AttendanceSessions.FindAsync(sessionId);
            if (session == null) throw new Exception("Oturum bulunamadı.");

            if (session.InstructorId != instructorId)
                throw new Exception("Bu oturumu sonlandırma yetkiniz yok.");

            if (!session.IsActive) return true;

            session.IsActive = false;
            session.EndTime = DateTime.Now;

            await _context.SaveChangesAsync();

            // =========================================================
            // 🚀 SIGNALR CANLI BİLDİRİMİ: YOKLAMA BİTTİ
            // =========================================================
            try
            {
                await _hubContext.Clients.Group(sessionId.ToString()).SendAsync("SessionEnded", sessionId);
                await _hubContext.Clients.All.SendAsync("SessionEndedGlobal", sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR Hatası] {ex.Message}");
            }

            return true;
        }

        public async Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorId)
        {
            var courses = await _context.Courses
                .Where(c => c.InstructorId == instructorId)
                .Select(c => new CourseDto
                {
                    Id = c.Id,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName
                })
                .ToListAsync();
            return courses;
        }

        // ==================================================================================
        // 6. HOCA: MANUEL DURUM GÜNCELLEME
        // ==================================================================================
        public async Task<bool> UpdateAttendanceStatusAsync(ManualAttendanceDto model)
        {
            var sessionExists = await _context.AttendanceSessions.AnyAsync(s => s.Id == model.SessionId);
            if (!sessionExists) throw new Exception($"Hata: {model.SessionId} ID'li bir oturum bulunamadı.");

            var studentExists = await _context.Users.AnyAsync(u => u.Id == model.StudentId);
            if (!studentExists) throw new Exception($"Hata: {model.StudentId} ID'li bir öğrenci bulunamadı.");

            var record = await _context.AttendanceRecords
                .FirstOrDefaultAsync(r => r.AttendanceSessionId == model.SessionId && r.StudentId == model.StudentId);

            if (record != null)
            {
                record.Status = model.Status;
                record.Description = model.Description;
                record.CheckInTime = DateTime.Now;
            }
            else
            {
                record = new AttendanceRecord
                {
                    AttendanceSessionId = model.SessionId,
                    StudentId = model.StudentId,
                    Status = model.Status,
                    Description = model.Description,
                    CheckInTime = DateTime.Now,
                    IsDeviceVerified = false,
                    IsFaceVerified = false,
                    IsValid = true,
                    UsedDeviceId = "Manuel",
                    DistanceFromSessionCenter = 0
                };
                _context.AttendanceRecords.Add(record);
            }

            try
            {
                await _context.SaveChangesAsync();

                // =========================================================
                // 🚀 SIGNALR CANLI BİLDİRİMİ: MANUEL GÜNCELLEME
                // =========================================================
                try
                {
                    await _hubContext.Clients.Group(model.SessionId.ToString()).SendAsync("StudentAttended", new
                    {
                        StudentId = model.StudentId,
                        Status = model.Status.ToString()
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR Hatası] {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Veritabanı hatası: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        // ==================================================================================
        // 9. HOCA: BİR OTURUMDAKİ YOKLAMA LİSTESİNİ GÖR
        // ==================================================================================
        public async Task<List<SessionAttendanceDto>> GetSessionAttendanceAsync(int sessionId)
        {
            var records = await _context.AttendanceRecords
                .Include(r => r.Student)
                .Where(r => r.AttendanceSessionId == sessionId)
                .OrderBy(r => r.Student.FullName)
                .Select(r => new SessionAttendanceDto
                {
                    StudentId = r.StudentId,
                    StudentName = r.Student.FullName,
                    SchoolNumber = r.Student.SchoolNumber ?? "-",
                    Status = r.Status.ToString(),
                    Description = r.Description,
                    CheckInTime = r.CheckInTime
                })
                .ToListAsync();

            return records;
        }

        // ==================================================================================
        // 10. HOCA İÇİN: DERSİ ALAN TÜM ÖĞRENCİLER VE GÜNCEL DURUMLARI
        // ==================================================================================
        public async Task<List<SessionAttendanceDto>> GetSessionStudentListAsync(int sessionId)
        {
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) throw new Exception("Oturum bulunamadı.");

            var courseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();

            var enrolledStudents = await _context.CourseEnrollments
                .Include(ce => ce.Student)
                .Where(ce => courseIds.Contains(ce.CourseId))
                .Select(ce => ce.Student)
                .Distinct()
                .ToListAsync();

            var existingRecords = await _context.AttendanceRecords
                .Where(r => r.AttendanceSessionId == sessionId)
                .ToListAsync();

            var fullList = new List<SessionAttendanceDto>();

            foreach (var student in enrolledStudents)
            {
                var record = existingRecords.FirstOrDefault(r => r.StudentId == student.Id);
                fullList.Add(new SessionAttendanceDto
                {
                    StudentId = student.Id,
                    StudentName = student.FullName,
                    SchoolNumber = student.SchoolNumber ?? "No Number",
                    Status = record != null ? record.Status.ToString() : "NotMarked",
                    Description = record?.Description ?? "-",
                    CheckInTime = record?.CheckInTime ?? DateTime.MinValue
                });
            }

            return fullList.OrderBy(x => x.StudentName).ToList();
        }

        // ==================================================================================
        // 11. YARDIMCI: BİR DERSİ ALAN ÖĞRENCİLERİ LİSTELE
        // ==================================================================================
        public async Task<List<EnrolledStudentDto>> GetEnrolledStudentsByCourseAsync(int courseId)
        {
            var students = await _context.CourseEnrollments
                .Where(ce => ce.CourseId == courseId)
                .Include(ce => ce.Student)
                .Select(ce => new EnrolledStudentDto
                {
                    Id = ce.Student.Id,
                    FullName = ce.Student.FullName,
                    SchoolNumber = ce.Student.SchoolNumber ?? "-",
                    Email = ce.Student.Email
                })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            return students;
        }

        // ==================================================================================
        // 12. HOCA: BENİM ÖĞRENCİLERİMİ LİSTELE
        // ==================================================================================
        public async Task<List<EnrolledStudentDto>> GetMyStudentsAsync(int instructorId)
        {
            var students = await _context.CourseEnrollments
                .Include(ce => ce.Course)
                .Include(ce => ce.Student)
                .Where(ce => ce.Course.InstructorId == instructorId)
                .Select(ce => ce.Student)
                .Distinct()
                .Select(s => new EnrolledStudentDto
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    SchoolNumber = s.SchoolNumber ?? "-",
                    Email = s.Email
                })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            return students;
        }

        // ==================================================================================
        // 13. HOCA: BELİRLİ BİR DERSİN ÖĞRENCİ LİSTESİ
        // ==================================================================================
        public async Task<List<EnrolledStudentDto>> GetStudentsByCourseIdAsync(int courseId, int instructorId)
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);

            if (course == null)
                throw new Exception("Bu ders bulunamadı veya size ait değil.");

            var students = await _context.CourseEnrollments
                .Where(ce => ce.CourseId == courseId)
                .Include(ce => ce.Student)
                .Select(ce => new EnrolledStudentDto
                {
                    Id = ce.Student.Id,
                    FullName = ce.Student.FullName,
                    SchoolNumber = ce.Student.SchoolNumber ?? "-",
                    Email = ce.Student.Email
                })
                .OrderBy(s => s.FullName)
                .ToListAsync();

            return students;
        }

        // ==================================================================================
        // 14. HOCA: DERSİN OTOMATİK YOKLAMA AYARLARINI GÜNCELLE
        // ==================================================================================
        public async Task<bool> UpdateCourseSettingsAsync(CourseSettingsDto model, int instructorId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == model.CourseId);

            if (course == null) throw new Exception("Ders bulunamadı.");
            if (course.InstructorId != instructorId) throw new Exception("Bu dersin ayarlarını değiştirme yetkiniz yok.");

            course.IsAutoAttendanceEnabled = model.IsAutoAttendanceEnabled;
            course.DefaultMethod = model.DefaultMethod;
            course.DefaultDurationMinutes = model.DefaultDurationMinutes;
            course.DefaultRadiusMeters = model.DefaultRadiusMeters;

            await _context.SaveChangesAsync();
            return true;
        }

        // ==================================================================================
        // YARDIMCI ÖZEL METOTLAR
        // ==================================================================================
        private async Task<JoinSessionResponseDto> RegisterAttendance(int sessionId, int studentId, string deviceId)
        {
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == sessionId && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            var record = new AttendanceRecord
            {
                AttendanceSessionId = sessionId,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                Status = AttendanceStatus.Present,
                Description = "Otomatik Giriş",
                IsDeviceVerified = true,
                IsValid = true,
                UsedDeviceId = deviceId,
                DistanceFromSessionCenter = 0
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            // =========================================================
            // 🚀 SIGNALR CANLI BİLDİRİMİ: ÖĞRENCİ KATILDI
            // =========================================================
            try
            {
                await _hubContext.Clients.Group(sessionId.ToString()).SendAsync("StudentAttended", new
                {
                    StudentId = studentId,
                    Status = "Present"
                });
                Console.WriteLine($"[SignalR] {studentId} ID'li öğrenci {sessionId} numaralı oturuma katıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR Hatası] {ex.Message}");
            }

            return new JoinSessionResponseDto
            {
                IsSuccess = true,
                Message = "Yoklama Başarılı!",
                CourseName = "Ders Eşleşti"
            };
        }
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371e3;
            var phi1 = lat1 * Math.PI / 180;
            var phi2 = lat2 * Math.PI / 180;
            var deltaPhi = (lat2 - lat1) * Math.PI / 180;
            var deltaLambda = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                    Math.Cos(phi1) * Math.Cos(phi2) *
                    Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private bool IsSessionValid(AttendanceSession session)
        {
            return session.IsActive;
        }

        private async Task CheckDeviceUniquenessAsync(int sessionId, string deviceId, int currentStudentId, bool requireVerification)
        {
            if (!requireVerification) return;

            if (string.IsNullOrEmpty(deviceId))
                throw new Exception("Cihaz bilgisi alınamadı.");

            bool isUsedByAnother = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == sessionId
                               && r.UsedDeviceId == deviceId
                               && r.StudentId != currentStudentId);

            if (isUsedByAnother)
                throw new Exception("Bu cihaz, bu derste başka bir öğrenci tarafından zaten kullanılmış! Lütfen kendi cihazınızı kullanın.");
        }
        // ==================================================================================
        // ÖĞRENCİ: KATILMADIĞI AKTİF DERSLERİ LİSTELEME (GENEL METOT)
        // ==================================================================================
        public async Task<List<StudentActiveSessionDto>> GetStudentActiveSessionsByMethodAsync(int studentId, AttendanceMethod method)
        {
            // 1. Öğrenciyi ve aldığı dersleri bul
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            var myCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            // 2. Sadece AKTİF olan ve İSTENEN YÖNTEMLE (QR / Location / Face) açılmış oturumları çek
            var activeSessions = await _context.AttendanceSessions
                .Include(s => s.Instructor)
                .Include(s => s.RelatedCourses).ThenInclude(rc => rc.Course)
                .Where(s => s.IsActive && s.Method == method) // <--- Method Filtresi Burada Dinamik
                .ToListAsync();

            // 3. FİLTRELEME: Öğrencinin ZATEN KATILDIĞI oturumları bul
            var joinedSessionIds = await _context.AttendanceRecords
                .Where(r => r.StudentId == studentId)
                .Select(r => r.AttendanceSessionId)
                .ToListAsync();

            var resultList = new List<StudentActiveSessionDto>();

            foreach (var session in activeSessions)
            {
                // A) Eğer öğrenci bu oturuma zaten katıldıysa LİSTEYE EKLEME!
                if (joinedSessionIds.Contains(session.Id))
                {
                    continue; // Atla
                }

                // B) Ders Kesişim Kontrolü: Öğrenci bu dersi alıyor mu?
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();

                if (sessionCourseIds.Intersect(myCourseIds).Any())
                {
                    string courseCodes = string.Join(", ", session.RelatedCourses.Select(rc => rc.Course.CourseCode));
                    string courseNames = string.Join(", ", session.RelatedCourses.Select(rc => rc.Course.CourseName));

                    int remainingMinutes = 0;
                    if (session.EndTime.HasValue)
                    {
                        remainingMinutes = (int)(session.EndTime.Value - DateTime.Now).TotalMinutes;
                        if (remainingMinutes < 0) remainingMinutes = 0;
                    }

                    resultList.Add(new StudentActiveSessionDto
                    {
                        SessionId = session.Id,
                        CourseCode = courseCodes,
                        CourseName = courseNames,
                        InstructorName = session.Instructor.FullName,
                        StartTime = session.StartTime,
                        RemainingTimeMinutes = remainingMinutes
                    });
                }
            }

            return resultList;
        }
        // ==================================================================================
        // HOCA: GEÇMİŞ (KAPALI) OTURUMLARI LİSTELEME (GARANTİ SAYIM YÖNTEMİ)
        // ==================================================================================
        public async Task<List<PastSessionDto>> GetPastSessionsAsync(int instructorId)
        {
            // 1. Önce oturumları çek (Include AttendanceRecords KALDIRILDI, gerek yok)
            var sessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses).ThenInclude(rc => rc.Course)
                .Where(s => s.InstructorId == instructorId && !s.IsActive)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            var result = new List<PastSessionDto>();

            foreach (var s in sessions)
            {
                // 2. Bu oturum hangi derslere bağlı?
                var courseIds = s.RelatedCourses.Select(rc => rc.CourseId).ToList();

                // 3. TOPLAM ÖĞRENCİ SAYISI (Bu dersleri alan tekil öğrenci sayısı)
                var totalStudents = await _context.CourseEnrollments
                    .Where(ce => courseIds.Contains(ce.CourseId))
                    .Select(ce => ce.StudentId)
                    .Distinct()
                    .CountAsync();

                // 4. KATILAN SAYISI (DOĞRUDAN VERİTABANI SORGUSU İLE)
                // Bu yöntem "Include" hatasına düşmez, veritabanından net sayıyı getirir.
                var attendedCount = await _context.AttendanceRecords
                    .Where(r => r.AttendanceSessionId == s.Id &&
                                r.Status == SmartAttendance.Domain.Enums.AttendanceStatus.Present)
                    .CountAsync();

                result.Add(new PastSessionDto
                {
                    SessionId = s.Id,
                    CourseNames = string.Join(", ", s.RelatedCourses.Select(rc => rc.Course.CourseCode)),
                    Method = s.Method.ToString(),
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    AttendedCount = attendedCount,
                    TotalStudents = totalStudents
                });
            }

            return result;
        }

        // dersi alan tüm öğrencileri ve kaç derse geldiklerini hesaplar.

        public async Task<List<CourseStudentStatDto>> GetCourseStudentStatsAsync(int courseId)
        {
            // 1. Bu derse ait bitmiş oturumları bul
            var courseSessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.RelatedCourses.Any(rc => rc.CourseId == courseId) && !s.IsActive)
                .Select(s => s.Id)
                .ToListAsync();

            int totalSessionsCount = courseSessions.Count;

            // 2. Dersi alan öğrencileri bul
            var enrolledStudents = await _context.CourseEnrollments
                .Include(e => e.Student)
                .Where(e => e.CourseId == courseId)
                .Select(e => new { e.Student.Id, e.Student.FullName, e.Student.SchoolNumber })
                .ToListAsync();

            var stats = new List<CourseStudentStatDto>();

            foreach (var student in enrolledStudents)
            {
                // 3. Öğrencinin bu dersin oturumlarından kaçına geldiğini say
                int attendedCount = await _context.AttendanceRecords
                    .CountAsync(r => r.StudentId == student.Id &&
                                     courseSessions.Contains(r.AttendanceSessionId) &&
                                     r.Status == AttendanceStatus.Present);

                stats.Add(new CourseStudentStatDto
                {
                    StudentId = student.Id,
                    StudentName = student.FullName,
                    SchoolNumber = student.SchoolNumber,
                    TotalSessions = totalSessionsCount,
                    AttendedSessions = attendedCount
                });
            }

            return stats.OrderBy(s => s.StudentName).ToList();
        }

        // ==================================================================================
        // 15. HOCA: DERSİN AYARLARINI GETİR (GET)
        // ==================================================================================
        public async Task<CourseSettingsDto> GetCourseSettingsAsync(int courseId, int instructorId)
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) throw new Exception("Ders bulunamadı.");

            // Güvenlik: Sadece dersin hocası ayarları görebilir
            if (course.InstructorId != instructorId)
                throw new Exception("Bu dersin ayarlarını görüntüleme yetkiniz yok.");

            return new CourseSettingsDto
            {
                CourseId = course.Id,
                IsAutoAttendanceEnabled = course.IsAutoAttendanceEnabled,
                DefaultMethod = course.DefaultMethod,
                DefaultDurationMinutes = course.DefaultDurationMinutes,
                DefaultRadiusMeters = course.DefaultRadiusMeters
            };
        }

        // ==================================================================================
        // 16. ÖĞRENCİ: ALDIĞI DERSLERİ LİSTELE
        // ==================================================================================
        public async Task<List<CourseDto>> GetStudentCoursesAsync(int studentId)
        {
            var courses = await _context.CourseEnrollments
                .Include(ce => ce.Course)
                .ThenInclude(c => c.Instructor)
                .Where(ce => ce.StudentId == studentId)
                .Select(ce => new CourseDto
                {
                    Id = ce.Course.Id,
                    CourseCode = ce.Course.CourseCode,
                    CourseName = ce.Course.CourseName,

                    // --- DÜZELTME BURADA ---
                    // Nesnenin tamamını değil, Ad ve Soyadı birleştirip string olarak veriyoruz.
                    InstructorName = ce.Course.Instructor.FullName 
                })
                .ToListAsync();

            return courses;
        }

        // ==================================================================================
        // 17. ÖĞRENCİ: BİR DERSİN YOKLAMA GEÇMİŞİNİ GETİR
        // ==================================================================================
        public async Task<List<StudentAttendanceHistoryDto>> GetStudentCourseHistoryAsync(int studentId, int courseId)
        {
            // 1. Bu derse ait BITMIŞ (IsActive=false) oturumları bul
            var sessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.RelatedCourses.Any(rc => rc.CourseId == courseId) && !s.IsActive)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            // 2. Öğrencinin bu ders kapsamındaki tüm kayıtlarını çek
            // (Tek tek db'ye gitmemek için hepsini hafızaya alıyoruz)
            var studentRecords = await _context.AttendanceRecords
                .Where(r => r.StudentId == studentId)
                .ToListAsync();

            var history = new List<StudentAttendanceHistoryDto>();

            foreach (var session in sessions)
            {
                // Bu oturumda öğrencinin kaydı var mı?
                var record = studentRecords.FirstOrDefault(r => r.AttendanceSessionId == session.Id);

                string status = "Absent"; // Varsayılan: Yok
                string description = "-";

                if (record != null)
                {
                    status = record.Status.ToString(); // Present, Excused, Absent
                    description = record.Description;
                }

                history.Add(new StudentAttendanceHistoryDto
                {
                    SessionId = session.Id,
                    StartTime = session.StartTime,
                    Method = session.Method.ToString(),
                    Status = status,
                    Description = description
                });
            }

            return history;
        }
    }
}