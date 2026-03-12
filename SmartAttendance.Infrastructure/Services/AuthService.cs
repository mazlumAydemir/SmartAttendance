using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartAttendance.Application.DTOs.Auth;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly SmartAttendanceDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(SmartAttendanceDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto model)
        {
            // 1. Kullanıcıyı bul
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
                throw new Exception("Kullanıcı bulunamadı veya şifre hatalı.");

            // 2. Şifreyi kontrol et
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);

            // Eğer BCrypt kullanmıyorsan düz metin kontrolü için:
            // if (user.PasswordHash != model.Password)

            if (!isPasswordValid)
                throw new Exception("Kullanıcı bulunamadı veya şifre hatalı.");

            // --- CİHAZ KONTROLÜ (Step 3) BURADAN KALDIRILDI ---
            // Artık cihaz kontrolünü AttendanceService içinde yapıyoruz.
            // --------------------------------------------------

            // 3. Token Üret
            return GenerateJwtToken(user);
        }

        private AuthResponseDto GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("FullName", user.FullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = creds,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return new AuthResponseDto
            {
                Token = tokenHandler.WriteToken(token),
                Expiration = tokenDescriptor.Expires.Value,
                FullName = user.FullName,
                Role = user.Role.ToString(),
                UserId = user.Id
            };
        }

        public async Task<bool> UpdateFcmTokenAsync(int userId, string token)
        {
            // Veritabanından kullanıcıyı bul
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return false;

            // Token'ı güncelle ve kaydet
            user.FcmToken = token;
            await _context.SaveChangesAsync();

            return true;
        }
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            return await _context.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    FullName = u.FullName, //
                    Email = u.Email, //
                    SchoolNumber = u.SchoolNumber, //
                    Role = u.Role.ToString(), //
                    FcmToken = u.FcmToken //
                })
                .ToListAsync();
        }
    }
}