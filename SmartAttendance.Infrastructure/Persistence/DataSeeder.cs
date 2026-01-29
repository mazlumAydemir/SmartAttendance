using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq; // Enum döngüsü için gerekli
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Persistence
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(SmartAttendanceDbContext context)
        {
            // 1. KULLANICILAR
            if (!await context.Users.AnyAsync())
            {
                var passHash = BCrypt.Net.BCrypt.HashPassword("123456");
                var users = new List<User>
                {
                    new User { FullName = "Sistem Admin", Email = "admin@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Admin },
                    new User { FullName = "Yıltan Bitirim", Email = "yiltan@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Ahmet Yılmaz", Email = "ahmet@std.smart.edu.tr", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Ayşe Demir", Email = "ayse@std.smart.edu.tr", PasswordHash = passHash, Role = UserRole.Student }
                };
                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
            }

            // 2. SINIF KONUMLARI (TEST İÇİN MANUEL AYARLANABİLİR)
            if (!await context.ClassLocations.AnyAsync())
            {
                var locations = new List<ClassLocation>
                {
                    // TEST ODASI: Swagger'da veya Postman'de bu koordinatları kullanacaksın.
                    new ClassLocation
                    {
                        RoomName = "TEST LAB 1",
                        Latitude = 35.145,  // <-- TEST KOORDİNATI (ENLEM)
                        Longitude = 33.905  // <-- TEST KOORDİNATI (BOYLAM)
                    },
                    new ClassLocation
                    {
                        RoomName = "Amfi-1",
                        Latitude = 35.146,
                        Longitude = 33.906
                    }
                };
                await context.ClassLocations.AddRangeAsync(locations);
                await context.SaveChangesAsync();
            }

            // 3. DERSLER
            if (!await context.Courses.AnyAsync())
            {
                var instructor = await context.Users.FirstOrDefaultAsync(u => u.Email == "yiltan@smart.edu.tr");
                var courses = new List<Course>
                {
                    new Course { CourseCode = "CMPE428", CourseName = "Software Engineering", InstructorId = instructor.Id },
                    new Course { CourseCode = "CMPE419", CourseName = "Mobile App Development", InstructorId = instructor.Id }
                };
                await context.Courses.AddRangeAsync(courses);
                await context.SaveChangesAsync();
            }

            // 4. DERS KAYITLARI (Enrollments)
            if (!await context.CourseEnrollments.AnyAsync())
            {
                var ahmet = await context.Users.FirstAsync(u => u.Email.StartsWith("ahmet"));
                var ayse = await context.Users.FirstAsync(u => u.Email.StartsWith("ayse"));

                var cmpe428 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE428");
                var cmpe419 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE419");

                await context.CourseEnrollments.AddRangeAsync(new List<CourseEnrollment>
                {
                    new CourseEnrollment { StudentId = ahmet.Id, CourseId = cmpe428.Id }, // Ahmet -> CMPE428
                    new CourseEnrollment { StudentId = ayse.Id, CourseId = cmpe428.Id },  // Ayşe  -> CMPE428
                    new CourseEnrollment { StudentId = ayse.Id, CourseId = cmpe419.Id }   // Ayşe  -> CMPE419
                });
                await context.SaveChangesAsync();
            }

            // 5. DERS PROGRAMI (SCHEDULE) - TEST İÇİN HER GÜN, HER SAAT
            if (!await context.CourseSchedules.AnyAsync())
            {
                var c428 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE428");

                // TEST LAB 1'i çekiyoruz
                var testRoom = await context.ClassLocations.FirstAsync(l => l.RoomName == "TEST LAB 1");

                var schedules = new List<CourseSchedule>();

                // BUGÜN HANGİ GÜN OLURSA OLSUN DERS OLSUN DİYE:
                // Haftanın 7 günü için de CMPE428 dersini "TEST LAB 1"e koyuyoruz.
                // Saat 00:00'dan 23:59'a kadar.
                foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                {
                    schedules.Add(new CourseSchedule
                    {
                        CourseId = c428.Id,
                        ClassLocationId = testRoom.Id,
                        DayOfWeek = day,
                        StartTime = new TimeSpan(0, 0, 0),   // Gün Başı
                        EndTime = new TimeSpan(23, 59, 59)   // Gün Sonu
                    });
                }

                await context.CourseSchedules.AddRangeAsync(schedules);
                await context.SaveChangesAsync();
            }
        }
    }
}