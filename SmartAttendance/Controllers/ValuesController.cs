using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Application.Interfaces;
using System.Security.Claims;

namespace SmartAttendance.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseController : ControllerBase
    {
        private readonly ICourseService _courseService;

        public CourseController(ICourseService courseService)
        {
            _courseService = courseService;
        }

        // GET: api/Course/my-timetable
        // Giriş yapmış kişinin token'ındaki ID'yi okur ve programını getirir.
        [HttpGet("my-timetable")]
        [Authorize] // Sadece giriş yapmış kullanıcılar çağırabilir
        public async Task<IActionResult> GetMyTimetable()
        {
            // Token'dan ID ve Rol okuma
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleString = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int userId = int.Parse(userIdString);
            var result = new List<SmartAttendance.Application.DTOs.Course.TimetableItemDto>();

            if (roleString == "Student")
            {
                result = await _courseService.GetStudentTimetableAsync(userId);
            }
            else if (roleString == "Instructor")
            {
                result = await _courseService.GetInstructorTimetableAsync(userId);
            }

            return Ok(result);
        }
    }
}