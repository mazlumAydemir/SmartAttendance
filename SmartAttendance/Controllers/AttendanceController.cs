using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.DTOs.Course;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Infrastructure.Hubs;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartAttendance.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        // 1. BAĞIMLILIKLARIN TANIMLANMASI (Private Fields)
        private readonly IAttendanceService _attendanceService;
        private readonly IFaceRecognitionService _faceRecognitionService;
        private readonly SmartAttendanceDbContext _context;
        private readonly IHubContext<AttendanceHub> _hubContext;

        // 2. CONSTRUCTOR (Yapıcı Metot - Hatalı kısım burasıydı, düzeltildi)
        public AttendanceController(
            IAttendanceService attendanceService,
            IFaceRecognitionService faceRecognitionService,
            SmartAttendanceDbContext context,
            IHubContext<AttendanceHub> hubContext)
        {
            _attendanceService = attendanceService;
            _faceRecognitionService = faceRecognitionService;
            _context = context;
            _hubContext = hubContext;
        }

        // ======================================================================
        // HOCA: OTURUM BAŞLATMA
        // ======================================================================
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

        // ======================================================================
        // ÖĞRENCİ: QR KOD İLE KATIL
        // ======================================================================
        [HttpPost("join-qr")]
        [Authorize(Roles = "Student")]
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

        // ======================================================================
        // ÖĞRENCİ: KONUM İLE KATIL
        // ======================================================================
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

     
        // ======================================================================
        // HOCA: AÇIK OTURUMLARIMI GETİR
        // ======================================================================
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

        // ======================================================================
        // HOCA: OTURUMU KAPAT
        // ======================================================================
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

        // ======================================================================
        // HOCA: VERDİĞİM DERSLERİ GETİR
        // ======================================================================
        [HttpGet("my-courses")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetMyCourses()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int instructorId = int.Parse(userIdString);
            var result = await _attendanceService.GetInstructorCoursesAsync(instructorId);
            return Ok(result);
        }

        // ======================================================================
        // HOCA: OTURUMDAKİ ÖĞRENCİ LİSTESİNİ VE DURUMLARINI GÖR
        // ======================================================================
        [HttpGet("session-records/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetSessionRecords(int sessionId)
        {
            var result = await _attendanceService.GetSessionAttendanceAsync(sessionId);
            return Ok(result);
        }

        // ======================================================================
        // HOCA: ÖĞRENCİ DURUMUNU GÜNCELLE (Var/Yok/İzinli)
        // ======================================================================
        [HttpPost("update-status")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> UpdateStatus([FromBody] ManualAttendanceDto model)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AttendanceStatus), model.Status))
                {
                    return BadRequest(new { message = "Geçersiz durum kodu! (1=Var, 2=Yok, 3=İzinli)" });
                }

                await _attendanceService.UpdateAttendanceStatusAsync(model);
                return Ok(new { message = "Öğrenci durumu başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ======================================================================
        // HOCA: TÜM SINIF LİSTESİNİ GÖR (GELEN VE GELMEYENLER)
        // ======================================================================
        [HttpGet("full-class-list/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetFullClassList(int sessionId)
        {
            var result = await _attendanceService.GetSessionStudentListAsync(sessionId);
            return Ok(result);
        }

        // ======================================================================
        // YARDIMCI: BU DERSİ KİMLER ALIYOR?
        // ======================================================================
        [HttpGet("course-students/{courseId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetCourseStudents(int courseId)
        {
            var result = await _attendanceService.GetEnrolledStudentsByCourseAsync(courseId);
            return Ok(result);
        }

        // ======================================================================
        // HOCA: BENİM DERSLERİMİ ALAN TÜM ÖĞRENCİLER
        // ======================================================================
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

        // ======================================================================
        // HOCA: SEÇTİĞİM DERSİ KİMLER ALIYOR?
        // ======================================================================
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

        // ======================================================================
        // HOCA: DERS AYARLARINI KAYDET VE GETİR
        // ======================================================================
        [HttpGet("instructor/course-settings/{courseId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetCourseSettings(int courseId)
        {
            var instructorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.GetCourseSettingsAsync(courseId, instructorId);
            return Ok(result);
        }

        [HttpPut("instructor/course-settings/update")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> UpdateCourseSettings([FromBody] CourseSettingsDto model)
        {
            var instructorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.UpdateCourseSettingsAsync(model, instructorId);
            return Ok(new { message = "Ders ayarları başarıyla güncellendi." });
        }

        // ======================================================================
        // ÖĞRENCİ: KATILMADIĞI AKTİF DERSLER (KONUM)
        // ======================================================================
        [HttpGet("student/active-sessions/location")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetActiveLocationSessions()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.GetStudentActiveSessionsByMethodAsync(userId, AttendanceMethod.Location);
            return Ok(result);
        }

        // ======================================================================
        // ÖĞRENCİ: KATILMADIĞI AKTİF DERSLER (QR)
        // ======================================================================
        [HttpGet("student/active-sessions/qr")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetActiveQrSessions()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.GetStudentActiveSessionsByMethodAsync(userId, AttendanceMethod.QrCode);
            return Ok(result);
        }


        // ======================================================================
        // HOCA: GEÇMİŞ YOKLAMALARI LİSTELE
        // ======================================================================
        [HttpGet("instructor/history/sessions")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetHistorySessions()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int instructorId = int.Parse(userIdString);
            var result = await _attendanceService.GetPastSessionsAsync(instructorId);
            return Ok(result);
        }

        // ======================================================================
        // HOCA: GEÇMİŞ BİR YOKLAMANIN DETAYLARI
        // ======================================================================
        [HttpGet("instructor/history/session-details/{sessionId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetHistorySessionDetails(int sessionId)
        {
            try
            {
                var result = await _attendanceService.GetSessionStudentListAsync(sessionId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ======================================================================
        // HOCA: DERSİN GENEL İSTATİSTİKLERİ
        // ======================================================================
        [HttpGet("instructor/course-stats/{courseId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetCourseStats(int courseId)
        {
            var result = await _attendanceService.GetCourseStudentStatsAsync(courseId);
            return Ok(result);
        }

        // ======================================================================
        // ÖĞRENCİ: ALDIĞIM DERSLER VE GEÇMİŞ
        // ======================================================================
        [HttpGet("student/my-courses")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudentCourses()
        {
            var studentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.GetStudentCoursesAsync(studentId);
            return Ok(result);
        }

        [HttpGet("student/history/{courseId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudentHistory(int courseId)
        {
            var studentId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var result = await _attendanceService.GetStudentCourseHistoryAsync(studentId, courseId);
            return Ok(result);
        }

        // ======================================================================
        // ⭐ HOCA: YAPAY ZEKA İLE SINIF TARAMASI (PANORAMİK) ⭐
        // ======================================================================
        [HttpPost("instructor/scan-crowd")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> ScanCrowd([FromForm] int sessionId, IFormFile frame)
        {
            try
            {
                // 1. Yapay Zeka'ya fotoğrafı ver, o sana tanıdığı öğrenci ID'lerini versin
                var recognizedIds = await _faceRecognitionService.IdentifyStudentsInCrowdAsync(sessionId, frame);

                if (!recognizedIds.Any())
                    return Ok(new { recognizedNames = new List<string>() });

                var recognizedNames = new List<string>();

                // 2. Tanınan Öğrencileri Yoklamada "VAR" olarak işaretle
                foreach (var studentId in recognizedIds)
                {
                    var recordExists = await _context.AttendanceRecords
                        .AnyAsync(r => r.AttendanceSessionId == sessionId && r.StudentId == studentId);

                    if (!recordExists)
                    {
                        var student = await _context.Users.FindAsync(studentId);
                        if (student != null)
                        {
                            var newRecord = new AttendanceRecord
                            {
                                AttendanceSessionId = sessionId,
                                StudentId = studentId,
                                Status = AttendanceStatus.Present,
                                CheckInTime = DateTime.Now,
                                Description = "Panoramik Sınıf Taraması (AI)",
                                IsDeviceVerified = true,
                                IsFaceVerified = true,
                                IsValid = true,
                                UsedDeviceId = "InstructorCamera",
                                DistanceFromSessionCenter = 0
                            };

                            _context.AttendanceRecords.Add(newRecord);
                            recognizedNames.Add(student.FullName);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 3. React tarafına Canlı Bildirim (SignalR) Gönder ki hoca ekranında görsün
                if (recognizedNames.Any())
                {
                    await _hubContext.Clients.Group(sessionId.ToString()).SendAsync("CrowdScanUpdate", recognizedNames);
                }

                // Sadece yeni eklenen isimleri React'e geri dön
                return Ok(new { recognizedNames = recognizedNames });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Sınıf tarama hatası: {ex.Message}" });
            }
        }
    }
}