using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Auth;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Infrastructure.Services;
using System.Security.Claims;

namespace SmartAttendance.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var result = await _authService.LoginAsync(loginDto);
                return Ok(result); // Token ve kullanıcı bilgisi döner
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("update-fcm-token")]
        [Authorize]
        public async Task<IActionResult> UpdateFcmToken([FromBody] FcmTokenDto request)
        {
            // 1. Token'dan User ID'yi al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            // 2. Service katmanına işi devret
            var success = await _authService.UpdateFcmTokenAsync(int.Parse(userIdClaim), request.Token);

            // 3. Sonuca göre cevap dön
            if (!success) return NotFound(new { message = "Kullanıcı bulunamadı." });

            return Ok(new { message = "FCM Token başarıyla kaydedildi." });
        }

        [Authorize] // En azından bir token gerektirir
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Liste alınırken bir hata oluştu: " + ex.Message });
            }
        }
    }
}