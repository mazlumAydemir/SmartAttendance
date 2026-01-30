using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.DTOs.Course;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Enums;
using System.Security.Claims;

namespace SmartAttendance.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Class başındaki [Authorize] yetkisini kaldırıyoruz, metodlara özel vereceğiz.
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;

        public AttendanceController(IAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        // HOCA İÇİN
        [HttpPost("start")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> StartSession([FromBody] CreateSessionDto model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int instructorId = int.Parse(userIdString);
            try
            {
                var result = await _attendanceService.StartSessionAsync(model, instructorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ÖĞRENCİ İÇİN (YENİ)
        [HttpPost("join-qr")]
        [Authorize(Roles = "Student")] // Sadece öğrenciler
        public async Task<IActionResult> JoinSession([FromBody] JoinSessionDto model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int studentId = int.Parse(userIdString);
            try
            {
                var result = await _attendanceService.JoinSessionAsync(model, studentId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // ÖĞRENCİ İÇİN (SADECE GPS)
        [HttpPost("join-location")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> JoinSessionByLocation([FromBody] JoinLocationDto model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int studentId = int.Parse(userIdString);
            try
            {
                var result = await _attendanceService.JoinSessionByLocationAsync(model, studentId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPost("join-face")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> JoinSessionByFace([FromForm] JoinFaceDto model) // <-- [FromForm] Kritik!
        {
            // Kullanıcı ID'sini token'dan al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // Servise gönder
            var result = await _attendanceService.JoinSessionByFaceAsync(model, userId);
            return Ok(result);
        }

        // ... (StartSession altına ekleyebilirsin) ...

        // HOCA: AÇIK OTURUMLARIMI GETİR
        [HttpGet("my-active-sessions")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetMyActiveSessions()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int instructorId = int.Parse(userIdString);

            var result = await _attendanceService.GetActiveSessionsAsync(instructorId);
            return Ok(result);
        }

        // HOCA: SEÇİLENİ KAPAT
        [HttpPost("end/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> EndSession(int sessionId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int instructorId = int.Parse(userIdString);

            try
            {
                await _attendanceService.EndSessionAsync(sessionId, instructorId);
                return Ok(new { message = "Oturum başarıyla kapatıldı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // HOCA: VERDİĞİM DERSLERİ GETİR
        [HttpGet("my-courses")]
        [Authorize(Roles = "Instructor")] // Sadece Hoca
        public async Task<IActionResult> GetMyCourses()
        {
            // Token'dan Hoca ID'sini al
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int instructorId = int.Parse(userIdString);

            var result = await _attendanceService.GetInstructorCoursesAsync(instructorId);
            return Ok(result);
        }
        // HOCA: OTURUMDAKİ ÖĞRENCİ LİSTESİNİ VE DURUMLARINI GÖR
        [HttpGet("session-records/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetSessionRecords(int sessionId)
        {
            // Güvenlik: Hoca sadece kendi oturumunu görebilmeli (Opsiyonel ama iyi olur)
            // Şimdilik direkt listeyi dönüyoruz.
            var result = await _attendanceService.GetSessionAttendanceAsync(sessionId);
            return Ok(result);
        }

        // HOCA: ÖĞRENCİ DURUMUNU GÜNCELLE (Var/Yok/İzinli)
        [HttpPost("update-status")]
        [Authorize(Roles = "Instructor")] // Sadece Hoca Yapabilir
        public async Task<IActionResult> UpdateStatus([FromBody] ManualAttendanceDto model)
        {
            try
            {
                // Enum kontrolü (Opsiyonel ama iyi olur)
                if (!Enum.IsDefined(typeof(AttendanceStatus), model.Status))
                {
                    return BadRequest(new { message = "Geçersiz durum kodu! (1=Var, 2=Yok, 3=İzinli)" });
                }

                await _attendanceService.UpdateAttendanceStatusAsync(model);

                return Ok(new { message = "Öğrenci durumu başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                // Servisten gelen hatayı (Örn: Öğrenci bulunamadı) ekrana bas
                return BadRequest(new { message = ex.Message });
            }
        }
        // HOCA: TÜM SINIF LİSTESİNİ GÖR (GELEN VE GELMEYENLER)
        [HttpGet("full-class-list/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetFullClassList(int sessionId)
        {
            var result = await _attendanceService.GetSessionStudentListAsync(sessionId);
            return Ok(result);
        }
        // YARDIMCI: BU DERSİ KİMLER ALIYOR?
        [HttpGet("course-students/{courseId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetCourseStudents(int courseId)
        {
            var result = await _attendanceService.GetEnrolledStudentsByCourseAsync(courseId);
            return Ok(result);
        }

        // HOCA: BENİM DERSLERİMİ ALAN TÜM ÖĞRENCİLER
        [HttpGet("my-students")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetMyStudents()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int instructorId = int.Parse(userIdString);

            var result = await _attendanceService.GetMyStudentsAsync(instructorId);
            return Ok(result);
        }
        // HOCA: SEÇTİĞİM DERSİ KİMLER ALIYOR?
        [HttpGet("instructor-course-students/{courseId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetStudentsBySpecificCourse(int courseId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int instructorId = int.Parse(userIdString);

            try
            {
                var result = await _attendanceService.GetStudentsByCourseIdAsync(courseId, instructorId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // HOCA: DERS AYARLARINI KAYDET
        [HttpPost("update-course-settings")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> UpdateCourseSettings([FromBody] CourseSettingsDto model)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            try
            {
                await _attendanceService.UpdateCourseSettingsAsync(model, userId);
                return Ok(new { message = "Ders ayarları güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}