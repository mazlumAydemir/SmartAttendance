using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Application.DTOs.Admin;
using System.Threading.Tasks;

namespace SmartAttendance.Application.Interfaces
{
    public interface IAdminService
    {
        Task<AdminDashboardStatsDto> GetDashboardStatsAsync();
        Task<TeacherStatsDto> GetTeacherStatsAsync();
        Task<List<TeacherListDto>> GetAllTeachersAsync();

        Task<StudentStatsDto> GetStudentStatsAsync();
        Task<List<StudentListDto>> GetAllStudentsAsync();
        Task<List<CourseListDto>> GetAllCoursesAsync();
        Task<bool> CreateTeacherAsync(CreateTeacherDto dto);

        Task<bool> ToggleTeacherStatusAsync(int teacherId);
        Task<List<FacultyLookupDto>> GetFacultiesLookupAsync();
        Task<List<DepartmentLookupDto>> GetDepartmentsLookupAsync();

        Task<bool> CreateStudentAsync(CreateStudentDto dto);
        Task<bool> ToggleStudentStatusAsync(int studentId);
        // Ders Ekleme
        Task<bool> CreateCourseAsync(CreateCourseDto dto);

        // Ders Atama İşlemleri İçin
        Task<List<InstructorLookupDto>> GetInstructorsLookupAsync();
        Task<List<CourseStudentSelectionDto>> GetStudentsForCourseAssignmentAsync(int courseId);
        Task<bool> AssignStudentsToCourseAsync(int courseId, List<int> studentIds);
        Task<List<ClassLocationLookupDto>> GetClassLocationsLookupAsync();
        Task<bool> AddCourseScheduleAsync(CreateCourseScheduleDto dto);
        Task<List<CourseScheduleListDto>> GetCourseSchedulesAsync(int courseId);
        Task<List<ClassLocationListDto>> GetAllClassLocationsAsync();
        Task<bool> CreateClassLocationAsync(CreateClassLocationDto dto);
    }
}