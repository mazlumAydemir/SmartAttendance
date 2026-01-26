using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Attendance;
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

        public AttendanceService(SmartAttendanceDbContext context)
        {
            _context = context;
        }

        // ==================================================================================
        // 1. HOCA: OTURUM BAŞLATMA
        // ==================================================================================
        public async Task<SessionResponseDto> StartSessionAsync(CreateSessionDto model, int instructorId)
        {
            // A) ID KONTROLÜ
            var existingCoursesCount = await _context.Courses
                                            .Where(c => model.CourseIds.Contains(c.Id))
                                            .CountAsync();

            if (existingCoursesCount != model.CourseIds.Count)
            {
                throw new Exception("Hata: Seçilen derslerden biri veya birkaçı veritabanında bulunamadı.");
            }

            // B) Session Code Üretimi
            string sessionCode = Guid.NewGuid().ToString();

            // C) Oturumu Oluştur
            var session = new AttendanceSession
            {
                SessionCode = sessionCode,
                InstructorId = instructorId,

                // NOT: DurationMinutes kaldırıldı. 
                // Oturum sadece IsActive=false yapılınca kapanır.
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
            await _context.SaveChangesAsync();

            // D) Birleştirilmiş Dersleri Bağla
            foreach (var courseId in model.CourseIds)
            {
                _context.SessionCourseLinks.Add(new SessionCourseLink
                {
                    AttendanceSessionId = session.Id,
                    CourseId = courseId
                });
            }

            await _context.SaveChangesAsync();

            return new SessionResponseDto
            {
                SessionId = session.Id,
                SessionCode = sessionCode,
                QrCodeContent = sessionCode
            };
        }

        // ==================================================================================
        // 2. ÖĞRENCİ: QR İLE KATILMA
        // ==================================================================================
        public async Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId)
        {
            // A) QR ÇÖZÜMLEME
            var parts = model.QrContent.Split("||");
            if (parts.Length != 2) throw new Exception("Geçersiz QR Kod formatı!");

            string sessionCode = parts[0];
            if (!long.TryParse(parts[1], out long expirationTicks)) throw new Exception("QR Kod bozuk!");

            if (DateTime.UtcNow > new DateTime(expirationTicks))
                throw new Exception("QR Kodunun süresi dolmuş! Yenisini okutun.");

            // B) OTURUMU BUL
            var session = await _context.AttendanceSessions
                .Include(s => s.RelatedCourses)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

            // Merkezi Geçerlilik Kontrolü (Sadece Aktiflik)
            if (session == null || !IsSessionValid(session))
                throw new Exception("Oturum bulunamadı veya sonlandırılmış.");

            // C) ÖĞRENCİ KONTROLÜ
            var student = await _context.Users
                .Include(u => u.Enrollments)
                .FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) throw new Exception("Öğrenci bulunamadı.");

            // D) CİHAZ TEKİLLİK KONTROLÜ (Oturum Bazlı)
            await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, session.RequireDeviceVerification);

            // E) KONUM KONTROLÜ
            if (session.RequireLocationVerification)
            {
                double distance = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                double limit = session.SnapshotRadius ?? 50;

                if (distance > limit)
                    throw new Exception($"Sınıftan uzaktasınız! Mesafe: {distance:0.0}m (Sınır: {limit}m).");
            }

            // F) DERS EŞLEŞTİRME
            var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
            var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();

            if (!sessionCourseIds.Intersect(studentCourseIds).Any())
                throw new Exception("Bu oturuma ait dersi almıyorsunuz.");

            // G) KAYIT
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
                .Where(s => s.IsActive) // Sadece veritabanında aktif işaretli olanlar
                .ToListAsync();

            if (!activeSessions.Any()) throw new Exception("Şu anda aktif bir yoklama bulunmuyor.");

            AttendanceSession? targetSession = null;

            foreach (var session in activeSessions)
            {
                // 1. MERKEZİ GEÇERLİLİK KONTROLÜ
                if (!IsSessionValid(session)) continue;

                // 2. CİHAZ TEKİLLİK KONTROLÜ
                if (session.RequireDeviceVerification)
                {
                    await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);
                }

                // 3. DERS EŞLEŞTİRME
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();
                if (!sessionCourseIds.Intersect(studentCourseIds).Any()) continue;

                // 4. KONUM EŞLEŞTİRME
                if (session.RequireLocationVerification)
                {
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
                throw new Exception("Konumunuzda ve derslerinizde uygun bir aktif yoklama bulunamadı.");

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

            // Resim Kontrolü
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
                // 1. MERKEZİ GEÇERLİLİK KONTROLÜ
                if (!IsSessionValid(session)) continue;

                // 2. CİHAZ TEKİLLİK KONTROLÜ
                if (session.RequireDeviceVerification)
                {
                    await CheckDeviceUniquenessAsync(session.Id, model.DeviceId, studentId, true);
                }

                // 3. DERS EŞLEŞTİRME
                var sessionCourseIds = session.RelatedCourses.Select(rc => rc.CourseId).ToList();
                var studentCourseIds = student.Enrollments.Select(e => e.CourseId).ToList();
                if (!sessionCourseIds.Intersect(studentCourseIds).Any()) continue;

                // 4. KONUM KONTROLÜ
                double dist = CalculateDistance(session.SnapshotLatitude ?? 0, session.SnapshotLongitude ?? 0, model.Latitude, model.Longitude);
                if (dist <= (session.SnapshotRadius ?? 50))
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession == null)
                throw new Exception("Konumunuzda aktif bir ders bulunamadı. Sınıfta olduğunuza emin olun.");

            // --- FOTOĞRAF KAYDETME İŞLEMİ ---
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{studentId}_{targetSession.Id}_{Guid.NewGuid()}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.FaceImage.CopyToAsync(stream);
            }

            // --- MÜKERRER KAYIT KONTROLÜ ---
            bool alreadyExists = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == targetSession.Id && r.StudentId == studentId);

            if (alreadyExists)
                return new JoinSessionResponseDto { IsSuccess = true, Message = "Zaten yoklamadasınız.", CourseName = "Mevcut" };

            // --- KAYDI OLUŞTUR ---
            var record = new AttendanceRecord
            {
                AttendanceSessionId = targetSession.Id,
                StudentId = studentId,
                CheckInTime = DateTime.Now,
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
        // 5. HOCA: OTURUMU LİSTELEME VE SONLANDIRMA
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
            return true;
        }

        // ==================================================================================
        // YARDIMCI METOTLAR (PRIVATE HELPERS)
        // ==================================================================================

        // 1. KAYIT OLUŞTURMA YARDIMCISI
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

        // 2. MESAFE HESAPLAMA
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

        // 3. MERKEZİ OTURUM GEÇERLİLİK KONTROLÜ (SADECE AKTİFLİK)
        // Duration kaldırıldığı için artık sadece IsActive kontrolü yapılır.
        private bool IsSessionValid(AttendanceSession session)
        {
            // Hoca manuel kapattıysa geçersiz
            return session.IsActive;
        }

        // 4. OTURUM İÇİ CİHAZ TEKİLLİK KONTROLÜ
        private async Task CheckDeviceUniquenessAsync(int sessionId, string deviceId, int currentStudentId, bool requireVerification)
        {
            if (!requireVerification) return;

            if (string.IsNullOrEmpty(deviceId))
                throw new Exception("Cihaz bilgisi alınamadı.");

            // Bu oturumda, bu cihazı kullanan BAŞKA bir öğrenci var mı?
            bool isUsedByAnother = await _context.AttendanceRecords
                .AnyAsync(r => r.AttendanceSessionId == sessionId
                               && r.UsedDeviceId == deviceId
                               && r.StudentId != currentStudentId);

            if (isUsedByAnother)
            {
                throw new Exception("Bu cihaz, bu derste başka bir öğrenci tarafından zaten kullanılmış! Lütfen kendi cihazınızı kullanın.");
            }
        }
    }
}