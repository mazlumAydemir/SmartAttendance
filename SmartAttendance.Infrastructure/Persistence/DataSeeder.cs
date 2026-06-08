using Microsoft.EntityFrameworkCore;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Persistence
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(SmartAttendanceDbContext context, IFaceRecognitionService faceRecognitionService)
        {
            var passHash = BCrypt.Net.BCrypt.HashPassword("123456");

            // ==================================================================================
            // YUZ TANIMA: FOTOGRAFLARDAN 512 BOYUTLU VEKTOR CIKARMA
            // ==================================================================================
            string mazlumFaceVectorJson = null;
            string ibrahimFaceVectorJson = null;
            string erenFaceVectorJson = null;

            string baseDir = AppContext.BaseDirectory.Split(new[] { "\\bin", "/bin" }, StringSplitOptions.None)[0];

            string mazlumPath = Path.Combine(baseDir, "wwwroot", "img", "mazlumAydemir.jpeg");
            string ibrahimPath = Path.Combine(baseDir, "wwwroot", "img", "ibrahim.jpeg");
            string erenPath = Path.Combine(baseDir, "wwwroot", "img", "eren.jpeg");

            if (File.Exists(mazlumPath))
            {
                var bytes = await File.ReadAllBytesAsync(mazlumPath);
                mazlumFaceVectorJson = await faceRecognitionService.GenerateFaceEncodingAsync(bytes);
                if (mazlumFaceVectorJson == null) Console.WriteLine("HATA: Mazlum'un yuzu analiz edilemedi!");
            }
            else { Console.WriteLine($"HATA: Mazlum dosyasi bulunamadi! Aranan yol: {mazlumPath}"); }

            // DUZELTME: Eskiden burada yanlislikla mazlumPath kontrol ediliyordu, erenPath olmali.
            if (File.Exists(erenPath))
            {
                var bytes = await File.ReadAllBytesAsync(erenPath);
                erenFaceVectorJson = await faceRecognitionService.GenerateFaceEncodingAsync(bytes);
                if (erenFaceVectorJson == null) Console.WriteLine("HATA: Eren'in yuzu analiz edilemedi!");
            }
            else { Console.WriteLine($"HATA: eren dosyasi bulunamadi! Aranan yol: {erenPath}"); }

            if (File.Exists(ibrahimPath))
            {
                var bytes = await File.ReadAllBytesAsync(ibrahimPath);
                ibrahimFaceVectorJson = await faceRecognitionService.GenerateFaceEncodingAsync(bytes);
                if (ibrahimFaceVectorJson == null) Console.WriteLine("HATA: Ibrahim'in yuzu analiz edilemedi!");
            }
            else { Console.WriteLine($"HATA: Ibrahim dosyasi bulunamadi! Aranan yol: {ibrahimPath}"); }

            // ==================================================================================
            // 1. KULLANICILAR
            // ==================================================================================
            if (!await context.Users.AnyAsync())
            {
                var users = new List<User>
                {
                    new User { FullName = "Sistem Admin", Email = "admin@emu.edu.tr", PasswordHash = passHash, Role = UserRole.Admin },

                    new User { FullName = "Ahmet Ozseven", Email = "mehmet@emu.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Ahmet Ozseven", Email = "ahmet.ozseven@emu.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },
                    new User { FullName = "Elif Bozkurt", Email = "elif.bozkurt@emu.edu.tr", PasswordHash = passHash, Role = UserRole.Instructor },

                    new User { FullName = "Mazlum Aydemir", Email = "mazlum@std.smart.edu.tr", SchoolNumber="23002741", PasswordHash = passHash, Role = UserRole.Student, FaceEncoding = mazlumFaceVectorJson, ProfilePictureUrl = "/img/mazlumAydemir.jpeg" },
                    new User { FullName = "ibrahim filoglu", Email = "ibrahim@emu.edu.tr", SchoolNumber="23002742", PasswordHash = passHash, Role = UserRole.Student, FaceEncoding = ibrahimFaceVectorJson, ProfilePictureUrl = "/img/ibrahim.jpeg" },
                    new User { FullName = "Eren Sakalli", Email = "eren@emu.edu.tr", SchoolNumber="23002752", PasswordHash = passHash, Role = UserRole.Student, FaceEncoding = erenFaceVectorJson, ProfilePictureUrl = "/img/eren.jpeg" },
                    new User { FullName = "Ayse Demir", Email = "ayse@emu.edu.tr", SchoolNumber="23002743", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Fatma Sahin", Email = "fatma@emu.edu.tr", SchoolNumber="23002744", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Mehmet Can", Email = "mehmet@emu.edu.tr", SchoolNumber="23002745", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Zeynep Celik", Email = "zeynep@emu.edu.tr", SchoolNumber="23002746", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Burak Tekin", Email = "burak@emu.edu.tr", SchoolNumber="23002747", PasswordHash = passHash, Role = UserRole.Student },
                    new User { FullName = "Cemre Yildiz", Email = "cemre@emu.edu.tr", SchoolNumber="23002748", PasswordHash = passHash, Role = UserRole.Student }
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
            //    Tum dersler IsAutoAttendanceEnabled = true => worker programdaki saatte acar.
            //    DefaultDurationMinutes = 50 => oturum 50 dk sonra otomatik kapanir.
            // ==================================================================================
            if (!await context.Courses.AnyAsync())
            {
                var mehmet = await context.Users.FirstAsync(u => u.Email == "mehmet@emu.edu.tr");
                var ahmet = await context.Users.FirstAsync(u => u.Email == "ahmet.ozseven@emu.edu.tr");
                var elif = await context.Users.FirstAsync(u => u.Email == "elif.bozkurt@emu.edu.tr");

                var courses = new List<Course>
                {
                    new Course { CourseCode = "CMPE428", CourseName = "Software Engineering",   InstructorId = mehmet.Id, IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.QrCode,   DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    // CMPE419 (EN grup) ve BLGM419 (TR grup): AYNI HOCA, AYNI SAAT, AYNI SINIF - farkli ogrenci gruplari
                    new Course { CourseCode = "CMPE419", CourseName = "Mobile App Dev (EN)",     InstructorId = mehmet.Id, IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.Location, DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    new Course { CourseCode = "BLGM419", CourseName = "Mobil Uygulama (TR)",     InstructorId = mehmet.Id, IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.Location, DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    new Course { CourseCode = "BLGM371", CourseName = "Veritabani Sistemleri",   InstructorId = ahmet.Id,  IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.QrCode,   DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    new Course { CourseCode = "CMPE129", CourseName = "Intro. to Programming",   InstructorId = ahmet.Id,  IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.QrCode,   DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    new Course { CourseCode = "BLGM353", CourseName = "Isletim Sistemleri",      InstructorId = elif.Id,   IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.QrCode,   DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 },
                    new Course { CourseCode = "EKON111", CourseName = "Ekonomiye Giris",         InstructorId = elif.Id,   IsAutoAttendanceEnabled = true, DefaultMethod = AttendanceMethod.QrCode,   DefaultDurationMinutes = 50, DefaultRadiusMeters = 50 }
                };
                await context.Courses.AddRangeAsync(courses);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 4. DERS KAYITLARI
            //    CMPE419 (EN) ve BLGM419 (TR) FARKLI ogrenci gruplari almali.
            //    Ogrencileri ikiye boluyoruz: yari EN grubu, yari TR grubu.
            //    Diger dersleri herkes aliyor (test kolayligi).
            // ==================================================================================
            if (!await context.CourseEnrollments.AnyAsync())
            {
                var students = await context.Users.Where(u => u.Role == UserRole.Student).OrderBy(u => u.Id).ToListAsync();
                var courses = await context.Courses.ToListAsync();

                int Cid(string code) => courses.First(c => c.CourseCode == code).Id;

                var enrollments = new List<CourseEnrollment>();

                // Ortak dersler: herkes alir
                var commonCourses = new[] { "CMPE428", "BLGM371", "CMPE129", "BLGM353", "EKON111" };

                for (int i = 0; i < students.Count; i++)
                {
                    var student = students[i];

                    foreach (var code in commonCourses)
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = Cid(code) });

                    // 419 dersi: ogrencilerin yarisi EN (CMPE419) grubunda, yarisi TR (BLGM419) grubunda
                    if (i % 2 == 0)
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = Cid("CMPE419") }); // EN grubu
                    else
                        enrollments.Add(new CourseEnrollment { StudentId = student.Id, CourseId = Cid("BLGM419") }); // TR grubu
                }

                await context.CourseEnrollments.AddRangeAsync(enrollments);
                await context.SaveChangesAsync();
            }

            // ==================================================================================
            // 5. DERS PROGRAMI  - GERCEKCI HAFTALIK PLAN
            //    Kural: Her ders haftada EN AZ 4, EN FAZLA 6 slot (saat).
            //    CMPE419 ve BLGM419: BIREBIR AYNI gun/saat/sinif (paralel sube).
            //    NOT: Eski programi siler, yeniden kurar.
            // ==================================================================================
            var oldSchedules = await context.CourseSchedules.ToListAsync();
            if (oldSchedules.Any())
            {
                context.CourseSchedules.RemoveRange(oldSchedules);
                await context.SaveChangesAsync();
            }

            var cList = await context.Courses.ToListAsync();
            var lList = await context.ClassLocations.ToListAsync();

            Course C(string code) => cList.First(x => x.CourseCode == code);
            ClassLocation L(int i) => lList[i % lList.Count];

            // Standart 50 dakikalik ders saatleri (gercek universite saatleri gibi)
            var P = new Dictionary<int, (TimeSpan start, TimeSpan end)>
            {
                [1] = (new TimeSpan(8, 30, 0), new TimeSpan(9, 20, 0)),
                [2] = (new TimeSpan(9, 30, 0), new TimeSpan(10, 20, 0)),
                [3] = (new TimeSpan(10, 30, 0), new TimeSpan(11, 20, 0)),
                [4] = (new TimeSpan(11, 30, 0), new TimeSpan(12, 20, 0)),
                [5] = (new TimeSpan(13, 30, 0), new TimeSpan(14, 20, 0)),
                [6] = (new TimeSpan(14, 30, 0), new TimeSpan(15, 20, 0)),
                [7] = (new TimeSpan(15, 30, 0), new TimeSpan(16, 20, 0)),
                [8] = (new TimeSpan(16, 30, 0), new TimeSpan(17, 20, 0))
            };

            var schedules = new List<CourseSchedule>();

            void Add(string courseCode, DayOfWeek day, int period, int locIndex)
            {
                var c = C(courseCode);
                schedules.Add(new CourseSchedule
                {
                    CourseId = c.Id,
                    ClassLocationId = L(locIndex).Id,
                    DayOfWeek = day,
                    StartTime = P[period].start,
                    EndTime = P[period].end
                });
            }

            // CMPE419 ve BLGM419'u her zaman BIRLIKTE ekleyen yardimci (ayni gun/saat/sinif)
            void Add419Pair(DayOfWeek day, int period, int locIndex)
            {
                Add("CMPE419", day, period, locIndex);
                Add("BLGM419", day, period, locIndex);
            }

            // -----------------------------------------------------------------------------
            // HAFTALIK PROGRAM (min 4, max 6 slot/hafta)
            // -----------------------------------------------------------------------------

            // CMPE428 - Software Engineering  => 5 slot/hafta
            Add("CMPE428", DayOfWeek.Monday, 1, 0);
            Add("CMPE428", DayOfWeek.Monday, 2, 0);
            Add("CMPE428", DayOfWeek.Wednesday, 5, 1);
            Add("CMPE428", DayOfWeek.Wednesday, 6, 1);
            Add("CMPE428", DayOfWeek.Friday, 3, 0);

            // CMPE419 (EN) + BLGM419 (TR) PARALEL => her ikisi de 4 slot/hafta, BIREBIR ayni zaman/sinif
            Add419Pair(DayOfWeek.Monday, 5, 2);
            Add419Pair(DayOfWeek.Tuesday, 1, 2);
            Add419Pair(DayOfWeek.Tuesday, 2, 2);
            Add419Pair(DayOfWeek.Thursday, 6, 2);

            // BLGM371 - Veritabani Sistemleri => 6 slot/hafta (maksimum)
            Add("BLGM371", DayOfWeek.Monday, 3, 1);
            Add("BLGM371", DayOfWeek.Monday, 4, 1);
            Add("BLGM371", DayOfWeek.Wednesday, 3, 1);
            Add("BLGM371", DayOfWeek.Wednesday, 4, 1);
            Add("BLGM371", DayOfWeek.Friday, 1, 1);
            Add("BLGM371", DayOfWeek.Friday, 2, 1);

            // CMPE129 - Intro to Programming  => 5 slot/hafta
            Add("CMPE129", DayOfWeek.Monday, 6, 0);
            Add("CMPE129", DayOfWeek.Tuesday, 5, 0);
            Add("CMPE129", DayOfWeek.Thursday, 1, 0);
            Add("CMPE129", DayOfWeek.Thursday, 2, 0);
            Add("CMPE129", DayOfWeek.Friday, 5, 0);

            // BLGM353 - Isletim Sistemleri    => 4 slot/hafta
            Add("BLGM353", DayOfWeek.Tuesday, 6, 1);
            Add("BLGM353", DayOfWeek.Thursday, 3, 1);
            Add("BLGM353", DayOfWeek.Thursday, 4, 1);
            Add("BLGM353", DayOfWeek.Friday, 7, 1);

            // EKON111 - Ekonomiye Giris       => 4 slot/hafta
            Add("EKON111", DayOfWeek.Monday, 7, 3);
            Add("EKON111", DayOfWeek.Tuesday, 7, 3);
            Add("EKON111", DayOfWeek.Thursday, 5, 3);
            Add("EKON111", DayOfWeek.Friday, 8, 3);

            await context.CourseSchedules.AddRangeAsync(schedules);
            await context.SaveChangesAsync();

            // ==================================================================================
            // OZET: Haftalik slot sayilari (min 4, max 6 dogrulamasi)
            // ==================================================================================
            Console.WriteLine("==================================================");
            Console.WriteLine("[SEEDER] Haftalik ders programi kuruldu. Slot sayilari:");
            foreach (var grp in schedules.GroupBy(s => s.CourseId))
            {
                var code = cList.First(c => c.Id == grp.Key).CourseCode;
                Console.WriteLine($"[SEEDER]   {code}: {grp.Count()} slot/hafta");
            }
            Console.WriteLine("[SEEDER] NOT: CMPE419 ve BLGM419 ayni gun/saat/sinifta (paralel sube).");
            Console.WriteLine("[SEEDER]      Worker bu saatte IKI ayri oturum acar (her grup kendi oturumuna katilir).");
            Console.WriteLine($"[SEEDER] Bugun: {DateTime.Now:dddd HH:mm}");
            Console.WriteLine("==================================================");
        }
    }
}