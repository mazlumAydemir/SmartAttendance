using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartAttendance.Application.DTOs.Admin
{
    public class CreateCourseDto
    {
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public int InstructorId { get; set; }
        public int DepartmentId { get; set; }
    }
}