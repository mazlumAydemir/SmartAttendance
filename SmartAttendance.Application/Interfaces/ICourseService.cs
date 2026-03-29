using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartAttendance.Application.DTOs.Course;

namespace SmartAttendance.Application.Interfaces
{
    public interface ICourseService
    {
        // Öğrencinin aldığı derslere göre programını getirir
        Task<List<TimetableItemDto>> GetStudentTimetableAsync(int studentId);

        // Hocanın verdiği derslere göre programını getirir
        Task<List<TimetableItemDto>> GetInstructorTimetableAsync(int instructorId);
       
    }
}