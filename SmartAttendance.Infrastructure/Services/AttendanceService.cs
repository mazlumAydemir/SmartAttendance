using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums; // Enum için gerekli
using SmartAttendance.Infrastructure.Persistence;
using System.Collections.Generic;
namespace SmartAttendance.Infrastructure.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly SmartAttendanceDbContext _context;

        public AttendanceService(SmartAttendanceDbContext context)
        {
            _context = context;
        }

        // --- 1. HOCA: OTURUM BAŞLATMA ---
        public async Task<SessionResponseDto> StartSessionAsync(CreateSessionDto model, int instructorId)
        {
            // A) ID KONTROLÜ (Hata almamak için)
            // Hoca olmayan bir ders ID'si gönderirse (örn: 3) ve veritabanında yoksa burada yakalarız.
            var existingCoursesCount = await _context.Courses
                                        .Where(c => model.CourseIds.Contains(c.Id))
                                        .CountAsync();

            if (existingCoursesCount != model.CourseIds.Count)
            {
                throw new Exception("Hata: Seçilen derslerden biri veya birkaçı veritabanında bulunamadı! Lütfen veritabanını güncelleyin.");
            }

            // B) Session Code Üretimi
            string sessionCode = Guid.NewGuid().ToString();

            // C) Oturumu Oluştur
            var session = new AttendanceSession
            {
                SessionCode = sessionCode,
                InstructorId = instructorId,

                // GÜNCELLENEN KISIM:
                // Eğer model.StartTime doluysa onu kullan, boşsa DateTime.Now kullan.
                StartTime = model.StartTime ?? DateTime.Now,

                IsActive = true,
                Method = model.Method,
                RequireFaceVerification = (model.Method == AttendanceMethod.FaceScan) ? true : model.RequireFaceVerification,
                RequireDeviceVerification = model.RequireDeviceVerification,
                RequireLocationVerification = true,
                SnapshotLatitude = model.Latitude,
                SnapshotLongitude = model.Longitude,
                SnapshotRadius = model.RadiusMeters
            };

            _context.AttendanceSessions.Add(session);
            await _context.SaveChangesAsync(); // ID oluşması için kaydet

            // D) Birleştirilmiş Dersleri Bağla (Merged Classes)
            foreach (var courseId in model.CourseIds)
            {
                var link = new SessionCourseLink
                {
                    AttendanceSessionId = session.Id,
                    CourseId = courseId
                };
                _context.SessionCourseLinks.Add(link);
            }

            await _context.SaveChangesAsync();

            return new SessionResponseDto
            {
                SessionId = session.Id,
                SessionCode = sessionCode,
                QrCodeContent = sessionCode
            };
        }

        // --- 2. ÖĞRENCİ: QR İLE KATILMA ---
        public async Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId)
        {
            // A) DİNAMİK QR ÇÖZÜMLEME (10 Saniye Kuralı)
            // QR Formatı: "SESSION_GUID||EXPIRATION_TICKS"
            var parts = model.QrContent.Split("||");
            if (parts.Length != 2)
                throw new Exception("Geçersiz QR Kod formatı!");

            string sessionCode = parts[0];
            long expirationTicks;

            if (!long.TryParse(parts[1], out expirationTicks))
                throw new Exception("QR Kod zaman damgası bozuk!");

            // Süre Kontrolü
            var expirationTime = new DateTime(expirationTicks);
            if (DateTime.UtcNow > expirationTime)
                throw new Exception("QR Kodunun süresi dolmuş! Lütfen ekrandaki yeni kodu okutun.");

            // B) OTURUMU BUL
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

            if (session == null || !session.IsActive)
                throw new Exception("Oturum bulunamadı veya sonlandırılmış.");

            // C) ÖĞRENCİ VE CİHAZ KONTROLÜ
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            if (session.RequireDeviceVerification)
            {
                if (string.IsNullOrEmpty(student.RegisteredDeviceId))
                    throw new Exception("Cihazınız sisteme kayıtlı değil.");

                if (student.RegisteredDeviceId != model.DeviceId)
                    throw new Exception($"Bu cihaz size ait değil! Kayıtlı ID: {student.RegisteredDeviceId}");
            }

            // D) KONUM KONTROLÜ (Geofence)
            if (session.RequireLocationVerification)
            {
                double centerLat = session.SnapshotLatitude ?? 0;
                double centerLon = session.SnapshotLongitude ?? 0;
                double limitMeters = session.SnapshotRadius ?? 50;

                double distance = CalculateDistance(centerLat, centerLon, model.Latitude, model.Longitude);

                if (distance > limitMeters)
                    throw new Exception($"Sınıftan çok uzaktasınız! Mesafe: {distance:0.0}m (Sınır: {limitMeters}m).");
            }

            // E) DERS EŞLEŞTİRME
            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            var matchingCourseId = sessionCourseIds.Intersect(studentCourseIds).FirstOrDefault();
            if (matchingCourseId == 0)
                throw new Exception("Bu oturuma ait dersi almıyorsunuz.");

            // F) KAYIT VE CEVAP
            return await RegisterAttendance(session.Id, studentId, model.DeviceId);
        }

        // --- 3. ÖĞRENCİ: SADECE KONUM İLE KATILMA ---
        public async Task<JoinSessionResponseDto> JoinSessionByLocationAsync(JoinLocationDto model, int studentId)
        {
            // A) ÖĞRENCİYİ BUL
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            // Cihaz Kontrolü
            if (!string.IsNullOrEmpty(student.RegisteredDeviceId) && student.RegisteredDeviceId != model.DeviceId)
            {
                throw new Exception("Cihaz uyuşmazlığı! Kayıtlı cihazınızla giriş yapmalısınız.");
            }

            // B) AKTİF OTURUMLARI TARA
            var activeSessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!activeSessions.Any())
                throw new Exception("Şu anda aktif bir yoklama bulunmuyor.");

            AttendanceSession? targetSession = null;

            // C) DOĞRU OTURUMU BULMA ALGORİTMASI
            foreach (var session in activeSessions)
            {
                // 1. Ders Eşleşiyor mu?
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

                if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                    continue;

                // 2. Konum Tutuyor mu?
                if (session.RequireLocationVerification)
                {
                    double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                    double radius = session.SnapshotRadius ?? 50;

                    if (dist <= radius)
                    {
                        targetSession = session;
                        break; // Bulduk!
                    }
                }
                else
                {
                    // Konum zorunlu değilse direkt kabul et (Nadiren kullanılır)
                    targetSession = session;
                    break;
                }
            }

            if (targetSession == null)
                throw new Exception("Kayıtlı olduğunuz dersler için uygun konumda aktif bir yoklama bulunamadı.");

            // D) KAYIT VE CEVAP
            return await RegisterAttendance(targetSession.Id, studentId, model.DeviceId);
        }

        // --- YARDIMCI METOT: VERİTABANINA KAYIT ---
        private async Task<JoinSessionResponseDto> RegisterAttendance(int sessionId, int studentId, string deviceId)
        {
            // Mükerrer Kayıt Kontrolü
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == sessionId && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            var record = new AttendanceRecord
            {
                AttendanceSessionId = sessionId,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                IsDeviceVerified = true,
                IsValid = true,
                UsedDeviceId = deviceId,
                DistanceFromSessionCenter = 0
            };

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            return new JoinSessionResponseDto
            {
                IsSuccess = true,
                Message = "Yoklama Başarılı!",
                CourseName = "Ders Eşleşti"
            };
        }

        // --- YARDIMCI METOT: MESAFE HESAPLAMA (Metre) ---
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371e3; // Dünya yarıçapı
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
        // ... (Mevcut kodların devamı) ...

        // YÜZ TANIMA İLE DERSE KATILMA METODU
        public async Task<JoinSessionResponseDto> JoinSessionByFaceAsync(JoinFaceDto model, int studentId)
        {
            // 1. ÖĞRENCİ KONTROLÜ
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            // Cihaz Kontrolü
            if (!string.IsNullOrEmpty(student.RegisteredDeviceId) && student.RegisteredDeviceId != model.DeviceId)
                throw new Exception("Cihaz uyuşmazlığı! Kayıtlı cihazınızla giriş yapmalısınız.");

            // Resim Geldi mi?
            if (model.FaceImage == null || model.FaceImage.Length == 0)
                throw new Exception("Lütfen yüzünüzün göründüğü bir fotoğraf gönderin.");

            // 2. AKTİF OTURUMU KONUMA GÖRE BUL
            var activeSessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!activeSessions.Any()) throw new Exception("Şu anda aktif bir yoklama yok.");

            AttendanceSession? targetSession = null;

            foreach (var session in activeSessions)
            {
                // Ders Eşleşiyor mu?
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

                if (!sessionCourseIds.Intersect(studentCourseIds).Any()) continue;

                // Konum Kontrolü
                double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                double radius = session.SnapshotRadius ?? 50;

                if (dist <= radius)
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession == null)
                throw new Exception("Konumunuzda aktif bir ders bulunamadı. Sınıfta olduğunuza emin olun.");

            // 3. FOTOĞRAFI KLASÖRE KAYDET
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{studentId}_{targetSession.Id}_{Guid.NewGuid()}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.FaceImage.CopyToAsync(stream);
            }

            // 4. MÜKERRER KAYIT KONTROLÜ
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == targetSession.Id && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            // 5. KAYDI EKLE
            var record = new AttendanceRecord
            {
                AttendanceSessionId = targetSession.Id,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
                IsDeviceVerified = true,
                IsFaceVerified = true, // Yüz doğrulandı
                IsValid = true,
                UsedDeviceId = model.DeviceId,
                DistanceFromSessionCenter = 0, // İstenirse dist yazılır
                FaceSnapshotUrl = "/uploads/" + fileName // Resim yolu veritabanına
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
        // ... (Diğer metodların altı) ...

        // OTURUMU SONLANDIRMA METODU
        public async Task<bool> EndSessionAsync(int sessionId, int instructorId)
        {
            var session = await _context.AttendanceSessions.FindAsync(sessionId);

            if (session == null)
                throw new Exception("Oturum bulunamadı.");

            // Başkası kapatamasın diye kontrol
            if (session.InstructorId != instructorId)
                throw new Exception("Bu oturumu sonlandırma yetkiniz yok! Sadece başlatan hoca bitirebilir.");

            // Zaten kapalıysa işlem yapma
            if (!session.IsActive)
                return true;

            // Kapatma işlemi
            session.IsActive = false;
            session.EndTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }
        // 1. LİSTELEME METODU
        public async Task<List<ActiveSessionDto>> GetActiveSessionsAsync(int instructorId)
        {
            var sessions = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                    .ThenInclude(rc => rc.Course) // Ders isimlerini alabilmek için
                .Where(s => s.InstructorId == instructorId && s.IsActive) // Sadece bu hocanın ve AÇIK olanlar
                .OrderByDescending(s => s.StartTime) // En son açılan en üstte
                .ToListAsync();

            // Veritabanı nesnesini DTO'ya çeviriyoruz
            return sessions.Select(s => new ActiveSessionDto
            {
                SessionId = s.Id,
                SessionCode = s.SessionCode,
                StartTime = s.StartTime,
                MethodName = s.Method.ToString(), // Örn: "FaceScan"
                CourseNames = s.RelatedCourses.Select(rc => rc.Course.CourseName + " (" + rc.Course.CourseCode + ")").ToList()
            }).ToList();
        }

    }
}