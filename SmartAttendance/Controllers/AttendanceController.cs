using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Application.DTOs.Attendance;
using SmartAttendance.Application.Interfaces;
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
    }
}