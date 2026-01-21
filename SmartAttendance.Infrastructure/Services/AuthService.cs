using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartAttendance.Application.DTOs.Auth;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartAttendance.Application.DTOs.Auth;
using SmartAttendance.Application.Interfaces; // Interface referansı
using SmartAttendance.Domain.Entities;
using SmartAttendance.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SmartAttendance.Infrastructure.Services
{
    // DÜZELTME: ": IAuthService" ibaresi eklendi.
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
            if (!isPasswordValid)
                throw new Exception("Kullanıcı bulunamadı veya şifre hatalı.");

            // 3. Cihaz Kontrolü (Sadece öğrenciler için)
            if (user.Role == Domain.Enums.UserRole.Student && !string.IsNullOrEmpty(user.RegisteredDeviceId))
            {
                if (model.DeviceId != user.RegisteredDeviceId)
                    throw new Exception("Kayıtlı cihazınızdan giriş yapmalısınız! Cihaz ID uyuşmuyor.");
            }

            // 4. Token Üret
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
    }
}