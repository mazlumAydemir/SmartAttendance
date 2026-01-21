using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;

namespace SmartAttendance.Infrastructure.Persistence
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(SmartAttendanceDbContext context)
        {
            // 1. Kullanıcılar (Zaten varsa ekleme)
            if (!await context.Users.AnyAsync())
            {
                var passHash = BCrypt.Net.BCrypt.HashPassword("123456");
                var users = new List<User>
                {
                    new User { FullName = "Sistem Admin", Email = "admin@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Admin },
                    new User { FullName = "Yıltan Bitirim", Email = "yiltan@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Ahmet Yılmaz", Email = "ahmet@std.smart.edu.tr", PasswordHash = passHash, Role = UserRole.Student, RegisteredDeviceId = "device_ahmet_001" },
                    new User { FullName = "Ayşe Demir", Email = "ayse@std.smart.edu.tr", PasswordHash = passHash, Role = UserRole.Student, RegisteredDeviceId = "device_ayse_002" }
                };
                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
            }

            // 2. Sınıf Konumları
            if (!await context.ClassLocations.AnyAsync())
            {
                var locations = new List<ClassLocation>
                {
                    new ClassLocation { RoomName = "CMPE 128", Latitude = 35.145, Longitude = 33.905 },
                    new ClassLocation { RoomName = "Amfi-1", Latitude = 35.146, Longitude = 33.906 }
                };
                await context.ClassLocations.AddRangeAsync(locations);
                await context.SaveChangesAsync();
            }

            // 3. Dersler
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

            // 4. Ders Kayıtları (Ahmet CMPE428 alsın, Ayşe ikisini de alsın)
            if (!await context.CourseEnrollments.AnyAsync())
            {
                var ahmet = await context.Users.FirstAsync(u => u.Email.StartsWith("ahmet"));
                var ayse = await context.Users.FirstAsync(u => u.Email.StartsWith("ayse"));
                var c428 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE428");
                var c419 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE419");

                await context.CourseEnrollments.AddRangeAsync(new List<CourseEnrollment>
                {
                    new CourseEnrollment { StudentId = ahmet.Id, CourseId = c428.Id },
                    new CourseEnrollment { StudentId = ayse.Id, CourseId = c428.Id },
                    new CourseEnrollment { StudentId = ayse.Id, CourseId = c419.Id }
                });
                await context.SaveChangesAsync();
            }

            // 5. DERS PROGRAMI (TIMETABLE) - İŞTE 6 SAAT KURALI BURADA
            if (!await context.CourseSchedules.AnyAsync())
            {
                var c428 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE428");
                var c419 = await context.Courses.FirstAsync(c => c.CourseCode == "CMPE419");
                var room1 = await context.ClassLocations.FirstAsync(l => l.RoomName == "CMPE 128");

                var schedules = new List<CourseSchedule>();

                // --- CMPE 428 PROGRAMI (6 SAAT) ---
                // Pazartesi: 08:30, 09:30, 10:30 (3 Saat)
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(8, 30, 0), EndTime = new TimeSpan(9, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 20, 0) });

                // Çarşamba: 14:30, 15:30, 16:30 (3 Saat)
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(14, 30, 0), EndTime = new TimeSpan(15, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(15, 30, 0), EndTime = new TimeSpan(16, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c428.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(16, 30, 0), EndTime = new TimeSpan(17, 20, 0) });


                // --- CMPE 419 PROGRAMI (6 SAAT) ---
                // Salı: 08:30 - 11:20 (3 Saat)
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(8, 30, 0), EndTime = new TimeSpan(9, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 20, 0) });

                // Perşembe: 13:30 - 16:20 (3 Saat)
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(13, 30, 0), EndTime = new TimeSpan(14, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(14, 30, 0), EndTime = new TimeSpan(15, 20, 0) });
                schedules.Add(new CourseSchedule { CourseId = c419.Id, ClassLocationId = room1.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(15, 30, 0), EndTime = new TimeSpan(16, 20, 0) });

                await context.CourseSchedules.AddRangeAsync(schedules);
                await context.SaveChangesAsync();
            }
        }
    }
}