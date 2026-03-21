using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Admin;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly SmartAttendanceDbContext _context;

        public AdminService(SmartAttendanceDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
        {
            // GERÇEK VERİTABANI SAYIMLARI
            var totalStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student);

            // Aktif/Pasif Öğrenciler (User tablosuna IsActive eklemiştik)
            var activeStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student && u.IsActive);
            var inactiveStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student && !u.IsActive);

            var totalTeachers = await _context.Users.CountAsync(u => u.Role == UserRole.Instructor);
            var totalCourses = await _context.Courses.CountAsync(c => !c.IsDeleted);

            // ARTIK 4 VE 19 STATİK DEĞİL, DİREKT TABLODAN ÇEKİLİYOR!
            var totalFaculties = await _context.Faculties.CountAsync();
            var totalDepartments = await _context.Departments.CountAsync();

            return new AdminDashboardStatsDto
            {
                TotalFaculties = totalFaculties,
                TotalDepartments = totalDepartments,
                TotalCourses = totalCourses,
                TotalTeachers = totalTeachers,
                TotalStudents = totalStudents,
                ActiveStudents = activeStudents,
                InactiveStudents = inactiveStudents
            };
        }
        public async Task<TeacherStatsDto> GetTeacherStatsAsync()
        {
            var total = await _context.Users.CountAsync(u => u.Role == UserRole.Instructor);
            var active = await _context.Users.CountAsync(u => u.Role == UserRole.Instructor && u.IsActive);

            return new TeacherStatsDto
            {
                TotalTeachers = total,
                ActiveTeachers = active
            };
        }

        public async Task<List<TeacherListDto>> GetAllTeachersAsync()
        {
            // Öğretmenleri çekerken, ilişkili oldukları Bölüm, Fakülte ve Verdikleri Dersleri de (Include) getiriyoruz.
            var teachers = await _context.Users
                .Include(u => u.Department)
                    .ThenInclude(d => d.Faculty)
                .Include(u => u.GivenCourses)
                .Where(u => u.Role == UserRole.Instructor)
                .Select(u => new TeacherListDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    SchoolNumber = u.SchoolNumber ?? "Sicil No Yok",
                    IsActive = u.IsActive,
                    // Eğer bölüm atanmamışsa null hatası almamak için kontrol ediyoruz
                    DepartmentName = u.Department != null ? u.Department.Name : "Bölüm Atanmamış",
                    FacultyName = (u.Department != null && u.Department.Faculty != null) ? u.Department.Faculty.Name : "Fakülte Atanmamış",
                    CourseCount = u.GivenCourses.Count()
                })
                .ToListAsync();

            return teachers;
        }
        public async Task<StudentStatsDto> GetStudentStatsAsync()
        {
            var total = await _context.Users.CountAsync(u => u.Role == UserRole.Student);
            var active = await _context.Users.CountAsync(u => u.Role == UserRole.Student && u.IsActive);

            return new StudentStatsDto
            {
                TotalStudents = total,
                ActiveStudents = active
            };
        }

        // 2. LİSTELEME (URL'yi React'a Gönderme)
        public async Task<List<StudentListDto>> GetAllStudentsAsync()
        {
            return await _context.Users
                .Include(u => u.Department)
                .Where(u => u.Role == UserRole.Student)
                .Select(u => new StudentListDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    SchoolNumber = u.SchoolNumber ?? "Numara Yok",
                    IsActive = u.IsActive,
                    DepartmentName = u.Department != null ? u.Department.Name : "Bölüm Atanmamış",
                    GradeLevel = "1. Sınıf", // Şimdilik statik
                    ProfilePictureUrl = u.ProfilePictureUrl // <-- YENİ
                })
                .ToListAsync();
        }

        public async Task<List<CourseListDto>> GetAllCoursesAsync()
        {
            var courses = await _context.Courses
                // .Include(c => c.Enrollments) -> Select kullandığımız için Include'a gerek yok, EF Core kendi halleder
                .Where(c => c.IsDeleted == false) // !c.IsDeleted yerine doğrudan false eşleşmesi (Daha net SQL çevirisi)
                .Select(c => new CourseListDto
                {
                    Id = c.Id,
                    CourseName = c.CourseName ?? "İsimsiz Ders",
                    CourseCode = c.CourseCode ?? "KOD-YOK",
                    // Öğrenci listesi null ise 0 yaz, değilse sayısını al (Hata önleyici kalkan)
                    StudentCount = c.Enrollments != null ? c.Enrollments.Count : 0,
                    IsActive = !c.IsDeleted
                })
                .ToListAsync();

            return courses;
        }
        public async Task<bool> CreateTeacherAsync(CreateTeacherDto dto)
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (exists) throw new System.Exception("Bu e-posta adresi zaten sistemde kayıtlı!");

            var teacher = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                SchoolNumber = dto.SchoolNumber,
                DepartmentId = dto.DepartmentId, // <-- ARTIK VERİTABANINA BÖLÜMÜ DE KAYDEDİYORUZ
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Instructor,
                IsActive = true
            };

            await _context.Users.AddAsync(teacher);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<FacultyLookupDto>> GetFacultiesLookupAsync()
        {
            return await _context.Faculties
                .Select(f => new FacultyLookupDto { Id = f.Id, Name = f.Name })
                .ToListAsync();
        }

        public async Task<List<DepartmentLookupDto>> GetDepartmentsLookupAsync()
        {
            return await _context.Departments
                .Select(d => new DepartmentLookupDto { Id = d.Id, Name = d.Name, FacultyId = d.FacultyId })
                .ToListAsync();
        }
        public async Task<bool> ToggleTeacherStatusAsync(int teacherId)
        {
            var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.Instructor);

            if (teacher == null)
                throw new System.Exception("Öğretmen bulunamadı!");

            // Durumu tam tersine çevir (True ise False, False ise True yap)
            teacher.IsActive = !teacher.IsActive;

            await _context.SaveChangesAsync();

            // Yeni durumu geri döndür
            return teacher.IsActive;
        }
        // 1. ÖĞRENCİ EKLEME (Resmi Klasöre Kaydetme)
        public async Task<bool> CreateStudentAsync(CreateStudentDto dto)
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == dto.Email || u.SchoolNumber == dto.SchoolNumber);
            if (exists) throw new System.Exception("Bu e-posta veya okul numarası zaten sistemde kayıtlı!");

            string profilePicUrl = null;

            // EĞER RESİM YÜKLENDİYSE:
            if (dto.ProfileImage != null && dto.ProfileImage.Length > 0)
            {
                // wwwroot/uploads/profiles klasörüne kaydet
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Resmin adını benzersiz yap (çakışmasın diye)
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + dto.ProfileImage.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.ProfileImage.CopyToAsync(fileStream);
                }

                // Veritabanına kaydedilecek URL (örn: /uploads/profiles/resim.jpg)
                profilePicUrl = "/uploads/profiles/" + uniqueFileName;
            }

            var student = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                SchoolNumber = dto.SchoolNumber,
                DepartmentId = dto.DepartmentId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Student,
                IsActive = true,
                ProfilePictureUrl = profilePicUrl // <-- YENİ
            };

            await _context.Users.AddAsync(student);
            await _context.SaveChangesAsync();
            return true;
        }

    

        public async Task<bool> ToggleStudentStatusAsync(int studentId)
        {
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == UserRole.Student);
            if (student == null) throw new System.Exception("Öğrenci bulunamadı!");

            student.IsActive = !student.IsActive; // Durumu tersine çevir
            await _context.SaveChangesAsync();
            return student.IsActive;
        }
        // 1. Yeni Ders Ekleme
        public async Task<bool> CreateCourseAsync(CreateCourseDto dto)
        {
            var course = new Course
            {
                CourseCode = dto.CourseCode,
                CourseName = dto.CourseName,
                InstructorId = dto.InstructorId,
                DepartmentId = dto.DepartmentId,
                IsDeleted = false // HATA VEREN "IsActive" BURADAN KALDIRILDI!
            };

            await _context.Courses.AddAsync(course);
            await _context.SaveChangesAsync();
            return true;
        }

        // 2. Aktif Hocaları Getir (Dropdown için)
        public async Task<List<InstructorLookupDto>> GetInstructorsLookupAsync() // DÖNÜŞ TİPİ DÜZELTİLDİ!
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.Instructor && u.IsActive)
                .Select(u => new InstructorLookupDto { Id = u.Id, Name = u.FullName })
                .ToListAsync();
        }

        // 3. Bir ders için tüm öğrencileri getir (Kayıtlı olanları işaretle)
        public async Task<List<CourseStudentSelectionDto>> GetStudentsForCourseAssignmentAsync(int courseId)
        {
            var allActiveStudents = await _context.Users
                .Where(u => u.Role == UserRole.Student && u.IsActive)
                .ToListAsync();

            // UserId YERİNE "StudentId" KULLANILDI!
            var enrolledStudentIds = await _context.CourseEnrollments
                .Where(ce => ce.CourseId == courseId)
                .Select(ce => ce.StudentId) // <--- EĞER HALA KIZARSA BURASI SENİN ENTITY'NDE "AppUserId" veya "UserId" OLABİLİR.
                .ToListAsync();

            var result = allActiveStudents.Select(s => new CourseStudentSelectionDto
            {
                UserId = s.Id,
                FullName = s.FullName,
                SchoolNumber = s.SchoolNumber ?? "",
                IsEnrolled = enrolledStudentIds.Contains(s.Id)
            }).ToList();

            return result;
        }

        // 4. Öğrencileri Derse Kaydet
        public async Task<bool> AssignStudentsToCourseAsync(int courseId, List<int> studentIds)
        {
            // Eski kayıtların hepsini temizle
            var existingEnrollments = await _context.CourseEnrollments.Where(e => e.CourseId == courseId).ToListAsync();
            _context.CourseEnrollments.RemoveRange(existingEnrollments);
            await _context.SaveChangesAsync();

            // Tiklenen öğrencileri yeni kayıt olarak ekle
            // UserId YERİNE "StudentId" KULLANILDI!
            var newEnrollments = studentIds.Select(studentId => new CourseEnrollment
            {
                CourseId = courseId,
                StudentId = studentId // <--- EĞER HALA KIZARSA BURASI SENİN ENTITY'NDE "AppUserId" veya "UserId" OLABİLİR.
            }).ToList();

            await _context.CourseEnrollments.AddRangeAsync(newEnrollments);
            await _context.SaveChangesAsync();

            return true;
        }
        // Sınıfları/Amfileri Getir (Dropdown için)
        public async Task<List<ClassLocationLookupDto>> GetClassLocationsLookupAsync()
        {
            return await _context.ClassLocations
                .Select(c => new ClassLocationLookupDto { Id = c.Id, Name = c.RoomName })
                .ToListAsync();
        }

        // Ders Programını (Takvimi) Ekle
        public async Task<bool> AddCourseScheduleAsync(CreateCourseScheduleDto dto)
        {
            var schedule = new CourseSchedule
            {
                CourseId = dto.CourseId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                ClassLocationId = dto.ClassLocationId
            };

            await _context.CourseSchedules.AddAsync(schedule);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CourseScheduleListDto>> GetCourseSchedulesAsync(int courseId)
        {
            return await _context.CourseSchedules
                .Include(cs => cs.ClassLocation)
                .Where(cs => cs.CourseId == courseId)
                .Select(cs => new CourseScheduleListDto
                {
                    Id = cs.Id,
                    DayOfWeek = (int)cs.DayOfWeek,
                    StartTime = cs.StartTime,
                    EndTime = cs.EndTime,
                    LocationName = cs.ClassLocation.RoomName
                }).ToListAsync();
        }
        public async Task<List<ClassLocationListDto>> GetAllClassLocationsAsync()
        {
            return await _context.ClassLocations
                .Select(c => new ClassLocationListDto
                {
                    Id = c.Id,
                    RoomName = c.RoomName,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    FixedRadiusMeters = c.FixedRadiusMeters
                })
                .ToListAsync();
        }

        public async Task<bool> CreateClassLocationAsync(CreateClassLocationDto dto)
        {
            var location = new ClassLocation
            {
                RoomName = dto.RoomName,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                FixedRadiusMeters = dto.FixedRadiusMeters
            };

            await _context.ClassLocations.AddAsync(location);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}