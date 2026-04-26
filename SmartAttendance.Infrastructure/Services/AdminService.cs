using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Application.DTOs.Admin;
using SmartAttendance.Application.Interfaces;
using SmartAttendance.Domain.Entities;
using SmartAttendance.Domain.Enums;
using SmartAttendance.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartAttendance.Infrastructure.Services
{
    public class AdminService : IAdminService
    {
        private readonly SmartAttendanceDbContext _context;
        private readonly IFaceRecognitionService _faceRecognitionService; // 🔥 YENİ: Yapay Zeka Servisi Eklendi

        // 🔥 YENİ: Constructor güncellendi
        public AdminService(SmartAttendanceDbContext context, IFaceRecognitionService faceRecognitionService)
        {
            _context = context;
            _faceRecognitionService = faceRecognitionService;
        }

        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
        {
            var totalStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student);
            var activeStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student && u.IsActive);
            var inactiveStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student && !u.IsActive);
            var totalTeachers = await _context.Users.CountAsync(u => u.Role == UserRole.Instructor);
            var totalCourses = await _context.Courses.CountAsync(c => !c.IsDeleted);
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
                    GradeLevel = "1. Sınıf",
                    ProfilePictureUrl = u.ProfilePictureUrl
                })
                .ToListAsync();
        }

        public async Task<List<CourseListDto>> GetAllCoursesAsync()
        {
            var courses = await _context.Courses
                .Where(c => c.IsDeleted == false)
                .Select(c => new CourseListDto
                {
                    Id = c.Id,
                    CourseName = c.CourseName ?? "İsimsiz Ders",
                    CourseCode = c.CourseCode ?? "KOD-YOK",
                    StudentCount = c.Enrollments != null ? c.Enrollments.Count : 0,
                    IsActive = !c.IsDeleted,

                    // 🔥 YENİ: Hocanın adını çekiyoruz, hoca yoksa "Atanmadı" yazacak
                    InstructorName = c.Instructor != null ? c.Instructor.FullName : "Eğitmen Atanmadı"
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
                DepartmentId = dto.DepartmentId,
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

            teacher.IsActive = !teacher.IsActive;
            await _context.SaveChangesAsync();
            return teacher.IsActive;
        }

        // 🔥 DİKKAT: Dönüş tipi Task<bool> yerine Task<int> oldu! (Yeni ID'yi döndürüyoruz)
        public async Task<int> CreateStudentAsync(CreateStudentDto dto)
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == dto.Email || u.SchoolNumber == dto.SchoolNumber);
            if (exists) throw new System.Exception("Bu e-posta veya okul numarası zaten sistemde kayıtlı!");

            var student = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                SchoolNumber = dto.SchoolNumber,
                DepartmentId = dto.DepartmentId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Student,
                IsActive = true
            };

            await _context.Users.AddAsync(student);
            await _context.SaveChangesAsync();

            // 🔥 YENİ: Öğrencinin ID'sini geri dönüyoruz ki React hemen yüzünü kaydedebilsin!
            return student.Id;
        }

        // 🔥 YENİ: YÜZ KAYDETME İŞLEMİ (Controller'dan buraya alındı)
        public async Task<bool> RegisterStudentFaceAsync(int studentId, IFormFile faceImage)
        {
            if (faceImage == null || faceImage.Length == 0)
                throw new Exception("Geçerli bir yüz fotoğrafı bulunamadı.");

            var student = await _context.Users.FindAsync(studentId);
            if (student == null)
                throw new Exception("Öğrenci bulunamadı.");

            // 1. Resmi Byte Dizisine Çevir
            using var ms = new MemoryStream();
            await faceImage.CopyToAsync(ms);
            byte[] imageBytes = ms.ToArray();

            // 2. YAPAY ZEKA SERVİSİNİ ÇAĞIR (ArcFace vektörünü oluştur)
            string faceVectorJson = await _faceRecognitionService.GenerateFaceEncodingAsync(imageBytes);

            if (string.IsNullOrEmpty(faceVectorJson))
                throw new Exception("Yapay zeka fotoğrafta net bir yüz tespit edemedi.");

            // 3. FİZİKSEL DOSYAYI KAYDET (DataSeeder ile uyumlu olması için wwwroot/img klasörüne)
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{student.SchoolNumber}_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await faceImage.CopyToAsync(fileStream);
            }

            // 4. VERİTABANINI GÜNCELLE
            student.FaceEncoding = faceVectorJson;
            student.ProfilePictureUrl = $"/img/{uniqueFileName}";

            _context.Users.Update(student);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ToggleStudentStatusAsync(int studentId)
        {
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.Role == UserRole.Student);
            if (student == null) throw new System.Exception("Öğrenci bulunamadı!");

            student.IsActive = !student.IsActive;
            await _context.SaveChangesAsync();
            return student.IsActive;
        }

        public async Task<bool> CreateCourseAsync(CreateCourseDto dto)
        {
            var course = new Course
            {
                CourseCode = dto.CourseCode,
                CourseName = dto.CourseName,
                InstructorId = dto.InstructorId,
                DepartmentId = dto.DepartmentId,
                IsDeleted = false
            };

            await _context.Courses.AddAsync(course);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<InstructorLookupDto>> GetInstructorsLookupAsync()
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.Instructor && u.IsActive)
                .Select(u => new InstructorLookupDto { Id = u.Id, Name = u.FullName })
                .ToListAsync();
        }

        public async Task<List<CourseStudentSelectionDto>> GetStudentsForCourseAssignmentAsync(int courseId)
        {
            var allActiveStudents = await _context.Users
                .Where(u => u.Role == UserRole.Student && u.IsActive)
                .ToListAsync();

            var enrolledStudentIds = await _context.CourseEnrollments
                .Where(ce => ce.CourseId == courseId)
                .Select(ce => ce.StudentId)
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

        public async Task<bool> AssignStudentsToCourseAsync(int courseId, List<int> studentIds)
        {
            var existingEnrollments = await _context.CourseEnrollments.Where(e => e.CourseId == courseId).ToListAsync();
            _context.CourseEnrollments.RemoveRange(existingEnrollments);
            await _context.SaveChangesAsync();

            var newEnrollments = studentIds.Select(studentId => new CourseEnrollment
            {
                CourseId = courseId,
                StudentId = studentId
            }).ToList();

            await _context.CourseEnrollments.AddRangeAsync(newEnrollments);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<ClassLocationLookupDto>> GetClassLocationsLookupAsync()
        {
            return await _context.ClassLocations
                .Select(c => new ClassLocationLookupDto { Id = c.Id, Name = c.RoomName })
                .ToListAsync();
        }

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