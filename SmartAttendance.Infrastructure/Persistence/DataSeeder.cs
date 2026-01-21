using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;

namespace SmartAttendance.Infrastructure.Persistence
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(SmartAttendanceDbContext context)
        {
            // Eğer veritabanında kullanıcı varsa hiçbir şey yapma
            if (await context.Users.AnyAsync()) return;

            // Tüm test kullanıcıları için şifre: "123456"
            // Şifreyi hash'leyerek kaydediyoruz
            var commonPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var users = new List<User>
            {
                // 1. ADMİN
                new User
                {
                    FullName = "Sistem Yöneticisi",
                    Email = "admin@smart.edu.tr",
                    PasswordHash = commonPasswordHash,
                    Role = UserRole.Admin,
                    SchoolNumber = "ADM001"
                },

                // 2. ÖĞRETMEN (Yıltan Hoca)
                new User
                {
                    FullName = "Yıltan Bitirim",
                    Email = "yiltan@smart.edu.tr",
                    PasswordHash = commonPasswordHash,
                    Role = UserRole.Instructor,
                    SchoolNumber = "INS001"
                },

                // 3. ÖĞRENCİ 1 (Bilgisayar Müh. - Cihazı Tanımlı)
                new User
                {
                    FullName = "Ahmet Yılmaz",
                    Email = "ahmet@std.smart.edu.tr",
                    PasswordHash = commonPasswordHash,
                    Role = UserRole.Student,
                    SchoolNumber = "2020001",
                    RegisteredDeviceId = "device_ahmet_001" // Bu ID ile login olmalı
                },

                // 4. ÖĞRENCİ 2 (Yazılım Müh.)
                new User
                {
                    FullName = "Ayşe Demir",
                    Email = "ayse@std.smart.edu.tr",
                    PasswordHash = commonPasswordHash,
                    Role = UserRole.Student,
                    SchoolNumber = "2020002",
                    RegisteredDeviceId = "device_ayse_002"
                }
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
        }
    }
}