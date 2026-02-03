using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.DTOs.Course;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartAttendance.Application.Interfaces
{
    public interface IAttendanceService
    {
        // ==================================================================================
        // OTURUM YÖNETİMİ (BAŞLATMA / BİTİRME)
        // ==================================================================================

        // 1. Oturum Başlat
        Task<SessionResponseDto> StartSessionAsync(CreateSessionDto model, int instructorId);

        // 2. Aktif Oturumları Listele
        Task<List<ActiveSessionDto>> GetActiveSessionsAsync(int instructorId);

        // 3. Oturumu Sonlandır
        Task<bool> EndSessionAsync(int sessionId, int instructorId);

        // ==================================================================================
        // ÖĞRENCİ KATILIM METOTLARI
        // ==================================================================================

        // 4. QR ile katılma
        Task<JoinSessionResponseDto> JoinSessionAsync(JoinSessionDto model, int studentId);

        // 5. Konum ile katılma
        Task<JoinSessionResponseDto> JoinSessionByLocationAsync(JoinLocationDto model, int studentId);

        // 6. Yüz ile katılma
        Task<JoinSessionResponseDto> JoinSessionByFaceAsync(JoinFaceDto model, int studentId);

        // ==================================================================================
        // HOCA: LİSTELEME VE YÖNETİM METOTLARI
        // ==================================================================================

        // 7. Hocanın Derslerini Getir
        Task<List<CourseDto>> GetInstructorCoursesAsync(int instructorId);

        // 8. Bir Oturumdaki Mevcut Kayıtları Getir (Sadece Giriş Yapanlar)
        Task<List<SessionAttendanceDto>> GetSessionAttendanceAsync(int sessionId);

        // 9. Tam Sınıf Listesi (Gelenler + Gelmeyenler + Durumları)
        Task<List<SessionAttendanceDto>> GetSessionStudentListAsync(int sessionId);

        // 10. Manuel Durum Güncelleme (Var/Yok/İzinli Yazma)
        Task<bool> UpdateAttendanceStatusAsync(ManualAttendanceDto model);

        // 11. Belirli bir dersi alan öğrenciler (Admin/Genel kullanım)
        Task<List<EnrolledStudentDto>> GetEnrolledStudentsByCourseAsync(int courseId);

        // 12. Hocanın tüm öğrencilerinin tekil listesi
        Task<List<EnrolledStudentDto>> GetMyStudentsAsync(int instructorId);

        // 13. Hocanın belirli bir dersini alan öğrencileri listele (Güvenlikli)
        Task<List<EnrolledStudentDto>> GetStudentsByCourseIdAsync(int courseId, int instructorId);

        // ==================================================================================
        // OTOMATİK YOKLAMA AYARLARI
        // ==================================================================================

        // 14. Dersin Otomatik Yoklama Ayarlarını Güncelle
        Task<bool> UpdateCourseSettingsAsync(CourseSettingsDto model, int instructorId);
    }
}