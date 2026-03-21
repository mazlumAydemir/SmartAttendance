using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Application.DTOs.Admin;
using SmartAttendance.Application.Interfaces;
using System.Threading.Tasks;

namespace SmartAttendance.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] // Sadece sistem yöneticisi girebilir!
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _adminService.GetDashboardStatsAsync();
            return Ok(stats);
        }
        // ... (GetDashboardStats metodu duruyor)

        [HttpGet("teachers/stats")]
        public async Task<IActionResult> GetTeacherStats()
        {
            var stats = await _adminService.GetTeacherStatsAsync();
            return Ok(stats);
        }

        [HttpGet("teachers")]
        public async Task<IActionResult> GetAllTeachers()
        {
            var teachers = await _adminService.GetAllTeachersAsync();
            return Ok(teachers);
        }
        [HttpGet("students/stats")]
        public async Task<IActionResult> GetStudentStats()
        {
            var stats = await _adminService.GetStudentStatsAsync();
            return Ok(stats);
        }

        [HttpGet("students")]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _adminService.GetAllStudentsAsync();
            return Ok(students);
        }
        [HttpGet("courses")] // <-- React'ın bulamadığı adres burası!
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _adminService.GetAllCoursesAsync();
            return Ok(courses);
        }
        [HttpGet("export/students")]
        public async Task<IActionResult> ExportStudentsCsv()
        {
            var students = await _adminService.GetAllStudentsAsync();

            var builder = new System.Text.StringBuilder();

            // 1. DÜZELTME: Virgül (,) yerine Noktalı Virgül (;) kullanıyoruz. 
            // Böylece Excel sütunları kusursuz ayıracak.
            builder.AppendLine("Öğrenci ID;Ad Soyad;Okul Numarası;Bölüm;Sınıf;Durum");

            foreach (var s in students)
            {
                var durum = s.IsActive ? "Aktif" : "Pasif";
                builder.AppendLine($"{s.Id};{s.FullName};{s.SchoolNumber};{s.DepartmentName};{s.GradeLevel};{durum}");
            }

            // 2. DÜZELTME: Excel'in Türkçe karakterleri tanıması için UTF-8 BOM damgası ekliyoruz
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());

            // BOM ile asıl dosya verisini birleştir
            byte[] finalBytes = new byte[bom.Length + fileBytes.Length];
            System.Buffer.BlockCopy(bom, 0, finalBytes, 0, bom.Length);
            System.Buffer.BlockCopy(fileBytes, 0, finalBytes, bom.Length, fileBytes.Length);

            return File(finalBytes, "text/csv", "Ogrenciler.csv");
        }
        [HttpPost("teachers")]
        public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherDto dto)
        {
            try
            {
                await _adminService.CreateTeacherAsync(dto);
                return Ok(new { message = "Öğretmen başarıyla eklendi." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("faculties-lookup")]
        public async Task<IActionResult> GetFaculties()
        {
            return Ok(await _adminService.GetFacultiesLookupAsync());
        }

        [HttpGet("departments-lookup")]
        public async Task<IActionResult> GetDepartments()
        {
            return Ok(await _adminService.GetDepartmentsLookupAsync());
        }
        [HttpPut("teachers/{id}/toggle-status")]
        public async Task<IActionResult> ToggleTeacherStatus(int id)
        {
            try
            {
                var newStatus = await _adminService.ToggleTeacherStatusAsync(id);
                return Ok(new { message = "Öğretmen durumu güncellendi.", isActive = newStatus });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPost("students")]
        public async Task<IActionResult> CreateStudent([FromForm] CreateStudentDto dto)
        {
            try
            {
                await _adminService.CreateStudentAsync(dto);
                return Ok(new { message = "Öğrenci başarıyla eklendi." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("students/{id}/toggle-status")]
        public async Task<IActionResult> ToggleStudentStatus(int id)
        {
            try
            {
                var newStatus = await _adminService.ToggleStudentStatusAsync(id);
                return Ok(new { message = "Öğrenci durumu güncellendi.", isActive = newStatus });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPost("courses")]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
        {
            await _adminService.CreateCourseAsync(dto);
            return Ok(new { message = "Ders eklendi." });
        }

        [HttpGet("instructors-lookup")]
        public async Task<IActionResult> GetInstructors()
        {
            return Ok(await _adminService.GetInstructorsLookupAsync());
        }

        [HttpGet("courses/{courseId}/students")]
        public async Task<IActionResult> GetStudentsForCourse(int courseId)
        {
            return Ok(await _adminService.GetStudentsForCourseAssignmentAsync(courseId));
        }

        [HttpPost("courses/{courseId}/assign-students")]
        public async Task<IActionResult> AssignStudents(int courseId, [FromBody] List<int> studentIds)
        {
            await _adminService.AssignStudentsToCourseAsync(courseId, studentIds);
            return Ok(new { message = "Öğrenciler derse atandı." });
        }
        [HttpGet("class-locations-lookup")]
        public async Task<IActionResult> GetClassLocations()
        {
            return Ok(await _adminService.GetClassLocationsLookupAsync());
        }

        [HttpPost("courses/schedule")]
        public async Task<IActionResult> AddCourseSchedule([FromBody] CreateCourseScheduleDto dto)
        {
            try
            {
                await _adminService.AddCourseScheduleAsync(dto);
                return Ok(new { message = "Ders programı başarıyla eklendi." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpGet("courses/{courseId}/schedules")]
        public async Task<IActionResult> GetCourseSchedules(int courseId)
        {
            return Ok(await _adminService.GetCourseSchedulesAsync(courseId));
        }
        [HttpGet("class-locations")]
        public async Task<IActionResult> GetAllClassLocations()
        {
            return Ok(await _adminService.GetAllClassLocationsAsync());
        }

        [HttpPost("class-locations")]
        public async Task<IActionResult> CreateClassLocation([FromBody] CreateClassLocationDto dto)
        {
            try
            {
                await _adminService.CreateClassLocationAsync(dto);
                return Ok(new { message = "Lokasyon başarıyla eklendi." });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }



}