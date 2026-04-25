using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Application.Interfaces; // YENİ: Yapay Zeka Servisi için eklendi
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Persistence
{
    public static class DataSeeder
    {
        // YENİ: IFaceRecognitionService parametresi eklendi
        public static async Task SeedAsync(SmartAttendanceDbContext context, IFaceRecognitionService faceRecognitionService)
        {
            var passHash = BCrypt.Net.BCrypt.HashPassword("123456");

            // ==================================================================================
            // ⭐ YENİ YAPAY ZEKA: FOTOĞRAFLARDAN 512 BOYUTLU VEKTÖR ÇIKARMA
            // ==================================================================================
            string mazlumFaceVectorJson = null;
            string ibrahimFaceVectorJson = null;

            // DOSYA YOLUNU GARANTİYE ALALIM:
            // Uygulamanın ana dizinini bulur (wwwroot'un olduğu yer)
            string baseDir = AppContext.BaseDirectory.Split(new[] { "\\bin", "/bin" }, StringSplitOptions.None)[0];

            string mazlumPath = Path.Combine(baseDir, "wwwroot", "img", "mazlumAydemir.jpeg");
            string ibrahimPath = Path.Combine(baseDir, "wwwroot", "img", "ibrahim.jpeg");

            // MAZLUM TEST
            if (File.Exists(mazlumPath))
            {
                var bytes = await File.ReadAllBytesAsync(mazlumPath);
                mazlumFaceVectorJson = await faceRecognitionService.GenerateFaceEncodingAsync(bytes);
                if (mazlumFaceVectorJson == null) Console.WriteLine("❌ HATA: Mazlum'un yüzü analiz edilemedi!");
            }
            else { Console.WriteLine($"❌ HATA: Mazlum dosyası bulunamadı! Aranan yol: {mazlumPath}"); }

            // İBRAHİM TEST
            if (File.Exists(ibrahimPath))
            {
                var bytes = await File.ReadAllBytesAsync(ibrahimPath);
                ibrahimFaceVectorJson = await faceRecognitionService.GenerateFaceEncodingAsync(bytes);
                if (ibrahimFaceVectorJson == null) Console.WriteLine("❌ HATA: Ibrahim'un yüzü analiz edilemedi!");
            }
            else { Console.WriteLine($"❌ HATA: Ibrahim dosyası bulunamadı! Aranan yol: {ibrahimPath}"); }

            // ==================================================================================
            // 1. KULLANICILAR (Eğer boşsa ekler)
            // ==================================================================================
            if (!await context.Users.AnyAsync())
            {
                var users = new List<User>
                {
                    // Admin
                    new User { FullName = "Sistem Admin", Email = "admin@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Admin },
                    
                    // Hocalar
                    new User { FullName = "Mehmet Demir", Email = "mehmet@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Ahmet Özseven", Email = "ahmet.ozseven@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Elif Bozkurt", Email = "elif.bozkurt@smart.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },

                    // Öğrenciler (Yüz verileri ve Profil URL'leri ile)
                    // 🔥 Yeni ArcFace vektörleri buraya yazılıyor
                    new User { FullName = "Mazlum Aydemir", Email = "mazlum@std.smart.edu.tr", SchoolNumber="23002741", PasswordHash = passHash, Role = UserRole.Student, FaceEncoding = mazlumFaceVectorJson, ProfilePictureUrl = "/img/mazlumAydemir.jpeg" },
                    new User { FullName = "ibrahim filoğlu", Email = "ibrahim@std.smart.edu.tr", SchoolNumber="23002742", PasswordHash = passHash, Role = UserRole.Student, FaceEncoding = ibrahimFaceVectorJson, ProfilePictureUrl = "/img/ibrahim.jpeg" },

                    new User { FullName = "Ayşe Demir", Email = "ayse@std.smart.edu.tr", SchoolNumber="23002743", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Fatma Şahin", Email = "fatma@std.smart.edu.tr", SchoolNumber="23002744", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Mehmet Can", Email = "mehmet@std.smart.edu.tr", SchoolNumber="23002745", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Zeynep Çelik", Email = "zeynep@std.smart.edu.tr", SchoolNumber="23002746", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Burak Tekin", Email = "burak@std.smart.edu.tr", SchoolNumber="23002747", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Cemre Yıldız", Email = "cemre@std.smart.edu.tr", SchoolNumber="23002748", PasswordHash = passHash, Role = UserRole.Student }
                };
                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 2. SINIF KONUMLARI
            // ==================================================================================
            if (!await context.ClassLocations.AnyAsync())
            {
                var targetLat = 35.149807;
                var targetLon = 33.904789;

                var locations = new List<ClassLocation>
                {
                    new ClassLocation { RoomName = "TEST LAB 1", Latitude = targetLat, Longitude = targetLon },
                    new ClassLocation { RoomName = "Amfi-1", Latitude = targetLat, Longitude = targetLon },
                    new ClassLocation { RoomName = "CL 115", Latitude = targetLat, Longitude = targetLon },
                    new ClassLocation { RoomName = "CL 117", Latitude = targetLat, Longitude = targetLon }
                };
                await context.ClassLocations.AddRangeAsync(locations);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 3. DERSLER 
            // ==================================================================================
            if (!await context.Courses.AnyAsync())
            {
                var yiltan = await context.Users.FirstAsync(u => u.Email == "mehmet@smart.edu.tr");
                var ahmet = await context.Users.FirstAsync(u => u.Email == "ahmet.ozseven@smart.edu.tr");
                var elif = await context.Users.FirstAsync(u => u.Email == "elif.bozkurt@smart.edu.tr");

                var courses = new List<Course>
                {
                    new Course { CourseCode = "CMPE428", CourseName = "Software Engineering", InstructorId = yiltan.Id },
                    new Course { CourseCode = "CMPE419", CourseName = "Mobile App Dev (EN)", InstructorId = yiltan.Id },
                    new Course { CourseCode = "BLGM419", CourseName = "Mobil Uygulama (TR)", InstructorId = yiltan.Id },
                    new Course { CourseCode = "BLGM371", CourseName = "Veritabanı Sistemleri", InstructorId = ahmet.Id },
                    new Course { CourseCode = "CMPE129", CourseName = "Intro. to Programming", InstructorId = ahmet.Id },
                    new Course { CourseCode = "BLGM353", CourseName = "İşletim Sistemleri", InstructorId = elif.Id },
                    new Course { CourseCode = "EKON111", CourseName = "Ekonomiye Giriş", InstructorId = elif.Id }
                };
                await context.Courses.AddRangeAsync(courses);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 4. DERS KAYITLARI 
            // ==================================================================================
            if (!await context.CourseEnrollments.AnyAsync())
            {
                var students = await context.Users.Where(u => u.Role == UserRole.Student).ToListAsync();
                var courses = await context.Courses.ToListAsync();

                var enrollments = new List<CourseEnrollment>();

                foreach (var student in students)
                {
                    enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "EKON111").Id });

                    if (student.FullName.Length % 2 == 0)
                    {
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "CMPE419").Id });
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "CMPE428").Id });
                    }
                    else
                    {
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "BLGM419").Id });
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "BLGM371").Id });
                    }

                    if (student.FullName.Contains("a") || student.FullName.Contains("e") || student.FullName.Contains("i"))
                    {
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = courses.First(c => c.CourseCode == "BLGM353").Id });
                    }
                }

                await context.CourseEnrollments.AddRangeAsync(enrollments);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 5. DERS PROGRAMI (ESKİLERİ TEMİZLER, GÜNCEL/GECE SAATLERİNİ YAZAR)
            // ==================================================================================
            var oldSchedules = await context.CourseSchedules.ToListAsync();
            if (oldSchedules.Any())
            {
                context.CourseSchedules.RemoveRange(oldSchedules);
                await context.SaveChangesAsync();
            }

            var cList = await context.Courses.ToListAsync();
            var lList = await context.ClassLocations.ToListAsync();

            var schedules = new List<CourseSchedule>();

            var timeSlots = new List<(TimeSpan Start, TimeSpan End)>
            {
                (new TimeSpan(8, 30, 0), new TimeSpan(9, 20, 0)),
                (new TimeSpan(9, 30, 0), new TimeSpan(10, 20, 0)),
                (new TimeSpan(10, 30, 0), new TimeSpan(11, 20, 0)),
                (new TimeSpan(11, 30, 0), new TimeSpan(12, 20, 0)),
                (new TimeSpan(13, 30, 0), new TimeSpan(14, 20, 0)),
                (new TimeSpan(14, 30, 0), new TimeSpan(15, 20, 0)),
                (new TimeSpan(15, 30, 0), new TimeSpan(16, 20, 0)),
                (new TimeSpan(16, 30, 0), new TimeSpan(17, 20, 0)),
                (new TimeSpan(17, 30, 0), new TimeSpan(18, 20, 0)),
                (new TimeSpan(18, 30, 0), new TimeSpan(19, 20, 0)),
                (new TimeSpan(19, 30, 0), new TimeSpan(20, 20, 0)),
                (new TimeSpan(20, 30, 0), new TimeSpan(21, 20, 0)),
                (new TimeSpan(21, 30, 0), new TimeSpan(22, 20, 0)),
                (new TimeSpan(22, 30, 0), new TimeSpan(23, 20, 0))
            };

            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                for (int i = 0; i < timeSlots.Count; i++)
                {
                    var slot = timeSlots[i];
                    var course = cList[i % cList.Count];
                    var location = lList[i % lList.Count];

                    schedules.Add(new CourseSchedule
                    {
                        CourseId = course.Id,
                        ClassLocationId = location.Id,
                        DayOfWeek = day,
                        StartTime = slot.Start,
                        EndTime = slot.End
                    });

                    if (course.CourseCode == "CMPE419")
                    {
                        var blgm419 = cList.First(x => x.CourseCode == "BLGM419");
                        schedules.Add(new CourseSchedule
                        {
                            CourseId = blgm419.Id,
                            ClassLocationId = location.Id,
                            DayOfWeek = day,
                            StartTime = slot.Start,
                            EndTime = slot.End
                        });
                    }
                }
            }

            await context.CourseSchedules.AddRangeAsync(schedules);
            await context.SaveChangesAsync();
        }
    }
}